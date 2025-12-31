using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly Dictionary<string, DroneParameter> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TaskCompletionSource<DroneParameter>> _pendingParamWrites = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _parameterListCompletion;
    private readonly TimeSpan _operationTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _parameterDownloadTimeout = TimeSpan.FromSeconds(60);
    private ushort? _expectedParamCount;
    private bool _isParameterDownloadInProgress;
    private bool _isParameterDownloadComplete;
    private int _receivedParameterCount;

    public event EventHandler? ParameterListRequested;
    public event EventHandler<ParameterWriteRequest>? ParameterWriteRequested;
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
                _isParameterDownloadInProgress = false;
                _isParameterDownloadComplete = false;
                _receivedParameterCount = 0;
                _expectedParamCount = null;
            }
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        TaskCompletionSource<bool>? listCompletion;
        bool raiseProgressEvent;
        lock (_sync)
        {
            _parameters.Clear();
            _expectedParamCount = null;
            _parameterListCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            listCompletion = _parameterListCompletion;
            _isParameterDownloadInProgress = true;
            _isParameterDownloadComplete = false;
            _receivedParameterCount = 0;
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
                _expectedParamCount = null;
                _receivedParameterCount = 0;
                _isParameterDownloadInProgress = false;
                _isParameterDownloadComplete = false;
                _parameterListCompletion = null;
            }
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void HandleParamValue(DroneParameter parameter, ushort paramIndex, ushort paramCount)
    {
        TaskCompletionSource<DroneParameter>? pendingWrite = null;
        TaskCompletionSource<bool>? listCompletion = null;
        bool raiseProgress = false;

        lock (_sync)
        {
            _parameters[parameter.Name] = parameter;
            _receivedParameterCount = _parameters.Count;

            if (!_expectedParamCount.HasValue)
            {
                _expectedParamCount = paramCount;
            }
            else if (_expectedParamCount != paramCount)
            {
                _logger.LogWarning("Parameter count changed from {Expected} to {Actual}", _expectedParamCount, paramCount);
                _expectedParamCount = paramCount;
            }

            if (_pendingParamWrites.TryGetValue(parameter.Name, out pendingWrite))
            {
                _pendingParamWrites.Remove(parameter.Name);
            }

            if (_parameterListCompletion != null && _expectedParamCount.HasValue)
            {
                var lastIndex = paramCount == 0 ? (int?)null : paramCount - 1;
                if (_parameters.Count >= _expectedParamCount.Value ||
                    (lastIndex.HasValue && paramIndex >= lastIndex.Value))
                {
                    listCompletion = _parameterListCompletion;
                    _parameterListCompletion = null;
                    _isParameterDownloadInProgress = false;
                    _isParameterDownloadComplete = true;
                }
            }
            raiseProgress = true;
        }

        pendingWrite?.TrySetResult(parameter);
        listCompletion?.TrySetResult(true);
        if (raiseProgress)
        {
            ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
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
            _expectedParamCount = null;
            _receivedParameterCount = 0;
            _isParameterDownloadInProgress = false;
            _isParameterDownloadComplete = false;
            raiseProgress = true;
        }

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
