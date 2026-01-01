using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.UI.ViewModels;

public class FrameClassOption
{
    public int Value { get; }
    public string DisplayName { get; }

    public FrameClassOption(int value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}

public class FrameTypeOption
{
    public int Value { get; }
    public string DisplayName { get; }

    public FrameTypeOption(int value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}

public partial class AirframePageViewModel : ViewModelBase, IDisposable
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    private bool _isSyncingFromParameters;
    private int? _lastFrameTypeValue;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Task _scheduledSync = Task.CompletedTask;
    private readonly object _scheduledSyncGate = new();
    private bool _disposed;
    // Small tolerance used when interpreting cached float parameters as integral values.
    // MAVLink param values are floats; float.Epsilon is too small once values grow, so a 1e-4 window
    // guards against minor transport rounding while still treating parameters as integers.
    private const float ParameterEqualityTolerance = 0.0001f;
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(5);

    private static readonly IReadOnlyList<FrameClassOption> FrameClassCatalog = new List<FrameClassOption>
    {
        new(0, "Plane"),
        new(1, "Quad"),
        new(2, "Hexa"),
        new(3, "Octa"),
        new(4, "OctaQuad"),
        new(5, "Y6"),
        new(6, "Heli"),
        new(7, "Tri"),
        new(8, "SingleCopter"),
        new(9, "CoaxCopter"),
        new(10, "Twin"),
        new(11, "Heli Dual"),
        new(12, "DodecaHexa"),
        new(13, "Y4"),
        new(14, "Deca"),
    };

    private static readonly IReadOnlyList<FrameTypeOption> FrameTypeCatalog = new List<FrameTypeOption>
    {
        new(0, "Plus"),
        new(1, "X"),
        new(2, "V"),
        new(3, "H"),
        new(4, "V-Tail"),
    };

    [ObservableProperty]
    private ObservableCollection<FrameClassOption> _frameClasses = new(FrameClassCatalog);

    [ObservableProperty]
    private ObservableCollection<FrameTypeOption> _frameTypes = new(FrameTypeCatalog);

    [ObservableProperty]
    private FrameClassOption? _selectedFrameClass;

    [ObservableProperty]
    private FrameTypeOption? _selectedFrameType;

    [ObservableProperty]
    private string _statusMessage = "Connect and download parameters to configure frame.";

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isPageEnabled;

    public bool IsInteractionEnabled => IsPageEnabled && !IsApplying;
    public bool IsFrameTypeEnabled => IsInteractionEnabled && FrameTypes.Count > 0;
    // Frame type is only required when we know the vehicle already has a frame type value
    // (from cache) or the operator picked one explicitly. This allows overwriting even if the
    // parameter was missing while still demanding an explicit type when one exists.
    private bool FrameTypeSelectionIsRequired()
    {
        return FrameTypes.Count > 0 && (_lastFrameTypeValue.HasValue || SelectedFrameType != null);
    }

    public bool CanUpdate => CanExecuteUpdate();

    public AirframePageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterUpdated += OnParameterUpdated;

        UpdateAvailability();
    }

    partial void OnSelectedFrameClassChanged(FrameClassOption? value)
    {
        OnPropertyChanged(nameof(CanUpdate));
        var preferredType = _isSyncingFromParameters ? _lastFrameTypeValue : null;
        BuildFrameTypeOptions(preferredType);

        if (_isSyncingFromParameters)
        {
            return;
        }

        SelectedFrameType = null;

        if (IsInteractionEnabled)
        {
            StatusMessage = value != null
                ? $"Frame class selected: {value.DisplayName} ({value.Value})."
                : "Select a frame class.";
        }
    }

    partial void OnSelectedFrameTypeChanged(FrameTypeOption? value)
    {
        OnPropertyChanged(nameof(CanUpdate));

        if (_isSyncingFromParameters)
        {
            return;
        }

        if (IsInteractionEnabled && value != null)
        {
            StatusMessage = $"Frame type selected: {value.DisplayName} ({value.Value}).";
        }
    }

    partial void OnIsApplyingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(IsInteractionEnabled));
        OnPropertyChanged(nameof(IsFrameTypeEnabled));
    }

    partial void OnIsPageEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(IsInteractionEnabled));
        OnPropertyChanged(nameof(IsFrameTypeEnabled));
    }

    partial void OnFrameTypesChanged(ObservableCollection<FrameTypeOption> value)
    {
        OnPropertyChanged(nameof(IsFrameTypeEnabled));
        OnPropertyChanged(nameof(CanUpdate));
    }

    [RelayCommand]
    private async Task UpdateFrameAsync()
    {
        if (!IsInteractionEnabled)
        {
            StatusMessage = "Frame updates require connection and downloaded parameters.";
            return;
        }

        if (SelectedFrameClass == null)
        {
            StatusMessage = "Select a frame class before updating.";
            return;
        }

        var frameClassResult = false;
        var frameClassAttempted = false;
        var frameTypeResult = false;
        var frameTypeAttempted = false;

        try
        {
            IsApplying = true;
            StatusMessage = $"Writing FRAME_CLASS = {SelectedFrameClass.Value}...";
            frameClassAttempted = true;
            frameClassResult = await _parameterService.SetParameterAsync("FRAME_CLASS", SelectedFrameClass.Value);

            if (!frameClassResult)
            {
                StatusMessage = "FRAME_CLASS was not confirmed. No changes applied.";
                return;
            }

            if (SelectedFrameType != null)
            {
                StatusMessage = $"FRAME_CLASS confirmed. Writing FRAME_TYPE = {SelectedFrameType.Value}...";
                frameTypeAttempted = true;
                frameTypeResult = await _parameterService.SetParameterAsync("FRAME_TYPE", SelectedFrameType.Value);
            }

            var frameTypeConfirmed = !frameTypeAttempted || frameTypeResult;

            if (frameClassResult && frameTypeConfirmed)
            {
                if (SelectedFrameType != null)
                {
                    _lastFrameTypeValue = SelectedFrameType.Value;
                }
                OnPropertyChanged(nameof(CanUpdate));
                StatusMessage = "Frame parameters updated after confirmation.";
            }
            else
            {
                StatusMessage = frameTypeAttempted
                    ? "FRAME_TYPE was not confirmed. Frame update incomplete."
                    : "FRAME_CLASS was not confirmed. Frame update incomplete.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating frame: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
            // Re-sync from the parameter cache only when a write was attempted and any write failed,
            // so the UI reflects the last confirmed values instead of optimistic selections.
            if (HasAttemptedWrites(frameClassAttempted, frameTypeAttempted) &&
                HasFailedWrites(frameClassResult, frameTypeAttempted, frameTypeResult))
            {
                using var syncCts = new CancellationTokenSource(SyncTimeout);
                await SyncFromParametersAsync(forceStatusUpdate: true, cancellationToken: syncCts.Token);
            }
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(UpdateAvailability);
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        if (!IsFrameParameter(parameterName))
        {
            return;
        }

        ScheduleSyncFromParameters(forceStatusUpdate: true);
    }

    private void UpdateAvailability()
    {
        var connected = _connectionService.IsConnected;
        IsPageEnabled = connected;

        if (!connected)
        {
            StatusMessage = "Connect to a vehicle to edit FRAME_CLASS and FRAME_TYPE.";
            return;
        }

        ScheduleSyncFromParameters(forceStatusUpdate: true);
    }

    private async Task SyncFromParametersAsync(bool forceStatusUpdate, CancellationToken cancellationToken = default)
    {
        try
        {
            await _syncLock.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await ResetSyncingStateAsync();
            return;
        }

        try
        {
            if (!_connectionService.IsConnected)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var frameClassParam = await _parameterService.GetParameterAsync("FRAME_CLASS");
            cancellationToken.ThrowIfCancellationRequested();
            var frameTypeParam = await _parameterService.GetParameterAsync("FRAME_TYPE");

            var frameClassValue = TryParseParameterValue(frameClassParam);
            var frameTypeValue = TryParseParameterValue(frameTypeParam);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _isSyncingFromParameters = true;
            _lastFrameTypeValue = frameTypeValue;

            var selectedClass = EnsureFrameClassOption(frameClassValue);
            SelectedFrameClass = selectedClass;

            BuildFrameTypeOptions(frameTypeValue);
            SelectedFrameType = frameTypeValue.HasValue
                ? FrameTypes.FirstOrDefault(t => t.Value == frameTypeValue.Value)
                : null;

            if (forceStatusUpdate)
            {
                if (frameClassValue.HasValue)
                {
                    var typeText = frameTypeValue.HasValue ? frameTypeValue.Value.ToString() : "unset";
                    StatusMessage = $"FRAME_CLASS={frameClassValue.Value}, FRAME_TYPE={typeText}.";
                }
                else
                {
                    StatusMessage = "Waiting for parameters...";
                }
            }

            _isSyncingFromParameters = false;
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(IsFrameTypeEnabled));
            });
        }
        catch (OperationCanceledException)
        {
            await ResetSyncingStateAsync();
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isSyncingFromParameters = false;
                StatusMessage = $"Unable to sync frame parameters: {ex.Message}";
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void ScheduleSyncFromParameters(bool forceStatusUpdate)
    {
        var syncTask = SyncFromParametersAsync(forceStatusUpdate);
        lock (_scheduledSyncGate)
        {
            _scheduledSync = syncTask;
        }

        syncTask.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                var message = t.Exception.GetBaseException().Message;
                Dispatcher.UIThread.InvokeAsync(() => StatusMessage = $"Unable to sync frame parameters: {message}");
            }
        }, TaskScheduler.Default);
    }

    private Task ResetSyncingStateAsync()
    {
        return Dispatcher.UIThread.InvokeAsync(() => _isSyncingFromParameters = false);
    }

    private static bool IsFrameParameter(string parameterName)
    {
        return parameterName.Equals("FRAME_CLASS", StringComparison.OrdinalIgnoreCase) ||
               parameterName.Equals("FRAME_TYPE", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _parameterService.ParameterUpdated -= OnParameterUpdated;
        Task syncTask;
        lock (_scheduledSyncGate)
        {
            syncTask = _scheduledSync ?? Task.CompletedTask;
        }

        syncTask.ContinueWith(_ => _syncLock.Dispose(), TaskScheduler.Default);
    }

    private void BuildFrameTypeOptions(int? currentTypeValue)
    {
        var options = new List<FrameTypeOption>(FrameTypeCatalog);
        EnsureFrameTypeOption(currentTypeValue, options);

        FrameTypes.Clear();
        foreach (var option in options)
        {
            FrameTypes.Add(option);
        }

        OnPropertyChanged(nameof(IsFrameTypeEnabled));
        OnPropertyChanged(nameof(CanUpdate));
    }

    private FrameClassOption? EnsureFrameClassOption(int? frameClassValue)
    {
        if (!frameClassValue.HasValue)
        {
            return null;
        }

        var existing = FrameClasses.FirstOrDefault(c => c.Value == frameClassValue.Value);
        if (existing != null)
        {
            return existing;
        }

        var option = new FrameClassOption(frameClassValue.Value, $"Unknown ({frameClassValue.Value})");
        FrameClasses.Add(option);
        return option;
    }

    private FrameTypeOption? EnsureFrameTypeOption(int? frameTypeValue, List<FrameTypeOption> options)
    {
        if (!frameTypeValue.HasValue)
        {
            return null;
        }

        var existing = options.FirstOrDefault(t => t.Value == frameTypeValue.Value);
        if (existing != null)
        {
            return existing;
        }

        var option = new FrameTypeOption(frameTypeValue.Value, $"Unknown ({frameTypeValue.Value})");
        options.Add(option);
        return option;
    }

    private bool CanExecuteUpdate()
    {
        // Interaction must be enabled, a frame class chosen, and if a frame type is required
        // (known from cache or explicitly picked) it must also be selected.
        return IsInteractionEnabled &&
               SelectedFrameClass != null &&
               (!FrameTypeSelectionIsRequired() || SelectedFrameType != null);
    }

    private static bool HasAttemptedWrites(bool frameClassAttempted, bool frameTypeAttempted)
    {
        return frameClassAttempted || frameTypeAttempted;
    }

    private static bool HasFailedWrites(bool frameClassResult, bool frameTypeAttempted, bool frameTypeResult)
    {
        return !frameClassResult || (frameTypeAttempted && !frameTypeResult);
    }

    private static int? TryParseParameterValue(DroneParameter? parameter)
    {
        if (parameter == null)
        {
            return null;
        }

        var parsed = (int)Math.Round(parameter.Value, MidpointRounding.AwayFromZero);
        return Math.Abs(parameter.Value - parsed) < ParameterEqualityTolerance ? parsed : null;
    }
}
