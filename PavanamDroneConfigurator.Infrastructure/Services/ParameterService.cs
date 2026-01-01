using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly Dictionary<string, DroneParameter> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskCompletionSource<DroneParameter>> _pendingParamWrites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _receivedParamIndices = new();
    private readonly HashSet<int> _missingParamIndices = new();
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _parameterListCompletion;
    private readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _parameterDownloadTimeout = TimeSpan.FromSeconds(60);
    private readonly TimeSpan _paramValueIdleTimeout = TimeSpan.FromSeconds(3);
    private const int _maxParameterRetries = 3;
    private CancellationTokenSource? _parameterDownloadCts;
    private Task? _parameterDownloadMonitorTask;
    private ushort? _expectedParamCount;
    private bool _isParameterDownloadInProgress;
    private bool _isParameterDownloadComplete;
    private int _receivedParameterCount;
    private int _retryAttempts;
    private DateTime _lastParamValueReceived = DateTime.MinValue;

    public event EventHandler? ParameterListRequested;
    public event EventHandler<ParameterWriteRequest>? ParameterWriteRequested;
    public event EventHandler<ParameterReadRequest>? ParameterReadRequested;
    public event EventHandler? ParameterDownloadProgressChanged;

    public ParameterService(ILogger<ParameterService> logger)
    {
        _logger = logger;
    }

    public Task<List<DroneParameter>> GetAllParametersAsync()
    {
        _logger.LogInformation("Getting all cached parameters");
        lock (_sync)
        {
            return Task.FromResult(_parameters.Values.ToList());
        }
    }

    public bool IsParameterDownloadInProgress
    {
        get
        {
            lock (_sync)
            {
                return _isParameterDownloadInProgress;
            }
        }
    }

    public bool IsParameterDownloadComplete
    {
        get
        {
            lock (_sync)
            {
                return _isParameterDownloadComplete;
            }
        }
    }

    public int ReceivedParameterCount
    {
        get
        {
            lock (_sync)
            {
                return _receivedParameterCount;
            }
        }
    }

    public int? ExpectedParameterCount
    {
        get
        {
            lock (_sync)
            {
                return _expectedParamCount.HasValue ? (int?)_expectedParamCount.Value : null;
            }
        }
    }

    public Task<DroneParameter?> GetParameterAsync(string name)
    {
        _logger.LogInformation("Getting parameter: {Name}", name);
        lock (_sync)
        {
            _parameters.TryGetValue(name, out var param);
            return Task.FromResult(param);
        }
    }

    public async Task<bool> SetParameterAsync(string name, float value)
    {
        _logger.LogInformation("Setting parameter {Name} = {Value}", name, value);

        if (ParameterWriteRequested == null)
        {
            _logger.LogWarning("No MAVLink transport subscribed to parameter writes; cannot send PARAM_SET for {Name}", name);
            return false;
        }

        var confirmationSource = new TaskCompletionSource<DroneParameter>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
        {
            _pendingParamWrites[name] = confirmationSource;
        }

        ParameterWriteRequested?.Invoke(this, new ParameterWriteRequest(name, value));

        var completed = await Task.WhenAny(confirmationSource.Task, Task.Delay(_operationTimeout));
        if (completed != confirmationSource.Task)
        {
            _logger.LogWarning("Timed out waiting for PARAM_VALUE confirmation for {Name}", name);
            lock (_sync)
            {
                _pendingParamWrites.Remove(name);
            }
            return false;
        }

        var confirmedParameter = confirmationSource.Task.Result;
        lock (_sync)
        {
            _parameters[confirmedParameter.Name] = confirmedParameter;
        }

        _logger.LogInformation("Parameter {Name} updated from MAVLink confirmation", name);
        return true;
    }

    public async Task RefreshParametersAsync()
    {
        _logger.LogInformation("Requesting full parameter list via MAVLink PARAM_REQUEST_LIST");

        if (ParameterListRequested == null)
        {
            _logger.LogWarning("No MAVLink transport subscribed to parameter list requests; skipping refresh");
            lock (_sync)
            {
                _receivedParamIndices.Clear();
                _missingParamIndices.Clear();
                _isParameterDownloadInProgress = false;
                _isParameterDownloadComplete = false;
                _receivedParameterCount = 0;
                _expectedParamCount = null;
                _retryAttempts = 0;
                _lastParamValueReceived = DateTime.MinValue;
            }
            StopParameterMonitoring();
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        TaskCompletionSource<bool>? listCompletion;
        bool raiseProgressEvent;
        StopParameterMonitoring();
        var monitorCts = new CancellationTokenSource();
        lock (_sync)
        {
            _parameters.Clear();
            _receivedParamIndices.Clear();
            _missingParamIndices.Clear();
            _expectedParamCount = null;
            _parameterListCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            listCompletion = _parameterListCompletion;
            _isParameterDownloadInProgress = true;
            _isParameterDownloadComplete = false;
            _receivedParameterCount = 0;
            _retryAttempts = 0;
            _lastParamValueReceived = DateTime.UtcNow;
            _parameterDownloadCts = monitorCts;
            _parameterDownloadMonitorTask = MonitorParameterDownloadAsync(monitorCts.Token);
            raiseProgressEvent = true;
        }

        if (raiseProgressEvent)
        {
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
        }

        ParameterListRequested?.Invoke(this, EventArgs.Empty);

        if (listCompletion == null)
        {
            return;
        }

        using var downloadTimeoutCts = new CancellationTokenSource();
        var timeoutTask = Task.Delay(_parameterDownloadTimeout, downloadTimeoutCts.Token);
        var completed = await Task.WhenAny(listCompletion.Task, timeoutTask);
        if (completed == listCompletion.Task)
        {
            downloadTimeoutCts.Cancel();
        }
        else
        {
            _logger.LogWarning("Parameter list request timed out before completion");
            lock (_sync)
            {
                _parameters.Clear();
                _receivedParamIndices.Clear();
                _missingParamIndices.Clear();
                _expectedParamCount = null;
                _receivedParameterCount = 0;
                _isParameterDownloadInProgress = false;
                _isParameterDownloadComplete = false;
                _retryAttempts = 0;
                _lastParamValueReceived = DateTime.MinValue;
                _parameterListCompletion = null;
            }
            StopParameterMonitoring();
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void HandleParamValue(DroneParameter parameter, ushort paramIndex, ushort paramCount)
    {
        TaskCompletionSource<DroneParameter>? pendingWrite = null;
        TaskCompletionSource<bool>? listCompletion = null;
        bool raiseProgress = false;
        bool stopMonitor = false;

        lock (_sync)
        {
            _parameters[parameter.Name] = parameter;

            if (!_expectedParamCount.HasValue && paramCount > 0)
            {
                _expectedParamCount = paramCount;
                _missingParamIndices.Clear();
                _missingParamIndices.UnionWith(Enumerable.Range(0, paramCount));
                foreach (var receivedIndex in _receivedParamIndices)
                {
                    _missingParamIndices.Remove(receivedIndex);
                }
            }
            else if (_expectedParamCount.HasValue && paramCount > 0 && _expectedParamCount.Value != paramCount)
            {
                _logger.LogWarning("Parameter count changed from {Expected} to {Actual}", _expectedParamCount, paramCount);
                // Preserve the first advertised (>0) count to avoid oscillating completion criteria.
            }

            var indexWithinRange = !_expectedParamCount.HasValue || paramIndex < _expectedParamCount.Value;
            if (!indexWithinRange && _expectedParamCount.HasValue)
            {
                _logger.LogWarning("Received param_index {ParamIndex} outside expected range 0-{MaxIndex}", paramIndex, _expectedParamCount.Value - 1);
            }

            if (indexWithinRange && _receivedParamIndices.Add(paramIndex))
            {
                if (_expectedParamCount.HasValue)
                {
                    _missingParamIndices.Remove(paramIndex);
                }
            }
            _receivedParameterCount = _receivedParamIndices.Count;
            _lastParamValueReceived = DateTime.UtcNow;
            _retryAttempts = 0;

            if (_pendingParamWrites.TryGetValue(parameter.Name, out pendingWrite))
            {
                _pendingParamWrites.Remove(parameter.Name);
            }

            if (_parameterListCompletion != null && _expectedParamCount.HasValue &&
                _receivedParameterCount >= _expectedParamCount.Value)
            {
                listCompletion = _parameterListCompletion;
                _parameterListCompletion = null;
                _isParameterDownloadInProgress = false;
                _isParameterDownloadComplete = true;
                stopMonitor = true;
            }
            raiseProgress = true;
        }

        pendingWrite?.TrySetResult(parameter);
        listCompletion?.TrySetResult(true);
        if (stopMonitor)
        {
            StopParameterMonitoring();
        }
        if (raiseProgress)
        {
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task MonitorParameterDownloadAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_paramValueIdleTimeout, token);

                List<ushort>? missingIndices = null;
                bool completeDownload = false;
                bool raiseProgress = false;
                bool stopMonitor = false;
                bool skipProcessing = false;

                lock (_sync)
                {
                    if (!_isParameterDownloadInProgress)
                    {
                        skipProcessing = true;
                    }
                    else
                    {
                        var now = DateTime.UtcNow;
                        var timeSinceLastParam = now - _lastParamValueReceived;
                        var hasExpectedCount = _expectedParamCount.HasValue;

                        if (hasExpectedCount && _receivedParameterCount >= _expectedParamCount.Value)
                        {
                            completeDownload = true;
                        }
                        else if (timeSinceLastParam >= _paramValueIdleTimeout)
                        {
                            completeDownload = true;
                        }
                        else if (hasExpectedCount && _missingParamIndices.Count > 0)
                        {
                            if (_retryAttempts < _maxParameterRetries)
                            {
                                missingIndices = new List<ushort>(_missingParamIndices.Count);
                                foreach (var index in _missingParamIndices)
                                {
                                    missingIndices.Add((ushort)index);
                                }
                                _retryAttempts++;
                            }
                        }

                        if (completeDownload)
                        {
                            _isParameterDownloadInProgress = false;
                            _isParameterDownloadComplete = true;
                            _parameterListCompletion?.TrySetResult(true);
                            _parameterListCompletion = null;
                            stopMonitor = true;
                        }

                        raiseProgress = completeDownload || missingIndices != null;
                    }
                }

                if (skipProcessing)
                {
                    continue;
                }

                if (missingIndices != null)
                {
                    foreach (var missingIndex in missingIndices)
                    {
                        ParameterReadRequested?.Invoke(this, new ParameterReadRequest(missingIndex));
                    }
                }

                if (completeDownload && stopMonitor)
                {
                    StopParameterMonitoring();
                }

                if (raiseProgress)
                {
                    ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during teardown
        }
    }

    private void StopParameterMonitoring()
    {
        CancellationTokenSource? cts;
        lock (_sync)
        {
            cts = _parameterDownloadCts;
            _parameterDownloadCts = null;
            _parameterDownloadMonitorTask = null;
        }

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void Reset()
    {
        TaskCompletionSource<bool>? listCompletion;
        List<TaskCompletionSource<DroneParameter>> pendingWrites;
        bool raiseProgress = false;

        lock (_sync)
        {
            listCompletion = _parameterListCompletion;
            _parameterListCompletion = null;
            pendingWrites = _pendingParamWrites.Values.ToList();
            _pendingParamWrites.Clear();
            _parameters.Clear();
            _receivedParamIndices.Clear();
            _missingParamIndices.Clear();
            _expectedParamCount = null;
            _receivedParameterCount = 0;
            _retryAttempts = 0;
            _lastParamValueReceived = DateTime.MinValue;
            _isParameterDownloadInProgress = false;
            _isParameterDownloadComplete = false;
            raiseProgress = true;
        }

        StopParameterMonitoring();
        listCompletion?.TrySetCanceled();
        foreach (var pending in pendingWrites)
        {
            pending.TrySetCanceled();
        }

        if (raiseProgress)
        {
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
