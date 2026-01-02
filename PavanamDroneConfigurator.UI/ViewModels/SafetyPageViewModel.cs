using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PavanamDroneConfigurator.Core.Interfaces;

namespace PavanamDroneConfigurator.UI.ViewModels;

public sealed partial class SafetyPageViewModel : ViewModelBase, IDisposable
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private bool _isSyncing;
    private bool _disposed;

    private int? _fenceEnableCached;

    private const string ArmingParam = "ARMING_CHECK";
    private const string BattLowParam = "BATT_FS_LOW_ACT";
    private const string BattCriticalParam = "BATT_FS_CRT_ACT";
    private const string RcFailsafeParam = "FS_THR_ENABLE";
    private const string FenceEnableParam = "FENCE_ENABLE";
    private const string FenceActionParam = "FENCE_ACTION";

    private const int ArmingAllBit = 1;
    private const int ArmingBarometerBit = 2;
    private const int ArmingCompassBit = 4;
    private const int ArmingGpsBit = 8;
    private const int ArmingInsBit = 16;
    private const int ArmingRcBit = 64;
    private const int ArmingAccelerometerBit = 128;

    private static readonly ReadOnlyCollection<SafetyOption> BatteryActions = new(
    [
        new SafetyOption(0, "Disabled"),
        new SafetyOption(1, "Land"),
        new SafetyOption(2, "RTL"),
        new SafetyOption(3, "SmartRTL")
    ]);

    private static readonly ReadOnlyCollection<SafetyOption> RcFailsafeActions = new(
    [
        new SafetyOption(0, "Disabled"),
        new SafetyOption(1, "Always RTL"),
        new SafetyOption(2, "Continue Mission"),
        new SafetyOption(4, "SmartRTL")
    ]);

    private static readonly ReadOnlyCollection<SafetyOption> FenceActions = new(
    [
        new SafetyOption(0, "None"),
        new SafetyOption(1, "Land"),
        new SafetyOption(2, "RTL")
    ]);

    public IReadOnlyList<SafetyOption> BatteryActionOptions => BatteryActions;
    public IReadOnlyList<SafetyOption> RcFailsafeOptions => RcFailsafeActions;
    public IReadOnlyList<SafetyOption> FenceActionOptions => FenceActions;

    [ObservableProperty] private bool _isPageEnabled;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Downloading parameters...";

    [ObservableProperty] private bool _accelerometerCheck;
    [ObservableProperty] private bool _compassCheck;
    [ObservableProperty] private bool _gpsCheck;
    [ObservableProperty] private bool _barometerCheck;
    [ObservableProperty] private bool _rcCheck;
    [ObservableProperty] private bool _insCheck;

    [ObservableProperty] private SafetyOption? _selectedBattLowAction;
    [ObservableProperty] private SafetyOption? _selectedBattCriticalAction;
    [ObservableProperty] private SafetyOption? _selectedRcFailsafeAction;
    [ObservableProperty] private SafetyOption? _selectedFenceAction;

    [ObservableProperty] private bool _fenceEnabled;

    public SafetyPageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;
        _parameterService.ParameterUpdated += OnParameterUpdated;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        InitializeState();
    }

    private void InitializeState()
    {
        IsPageEnabled = _parameterService.IsParameterDownloadComplete && _connectionService.IsConnected;
        StatusMessage = IsPageEnabled ? "Safety parameters loaded." : "Downloading parameters...";

        if (IsPageEnabled)
        {
            RunSafe(SyncAllFromCacheAsync);
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsPageEnabled = connected && _parameterService.IsParameterDownloadComplete;
            if (IsPageEnabled)
            {
                StatusMessage = "Safety parameters loaded.";
                RunSafe(SyncAllFromCacheAsync);
            }
            else
            {
                StatusMessage = connected ? "Downloading parameters..." : "Disconnected - parameters unavailable";
            }
        });
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_parameterService.IsParameterDownloadComplete && _connectionService.IsConnected)
            {
                IsPageEnabled = true;
                StatusMessage = "Safety parameters loaded.";
                RunSafe(SyncAllFromCacheAsync);
            }
            else if (_parameterService.IsParameterDownloadInProgress)
            {
                var expected = _parameterService.ExpectedParameterCount?.ToString() ?? "?";
                StatusMessage = $"Downloading parameters... {_parameterService.ReceivedParameterCount}/{expected}";
                IsPageEnabled = false;
            }
        });
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (parameterName.Equals(ArmingParam, StringComparison.OrdinalIgnoreCase))
            {
                RunSafe(SyncArmingChecksAsync);
            }
            else if (parameterName.Equals(BattLowParam, StringComparison.OrdinalIgnoreCase))
            {
                RunSafe(SyncBattLowAsync);
            }
            else if (parameterName.Equals(BattCriticalParam, StringComparison.OrdinalIgnoreCase))
            {
                RunSafe(SyncBattCriticalAsync);
            }
            else if (parameterName.Equals(RcFailsafeParam, StringComparison.OrdinalIgnoreCase))
            {
                RunSafe(SyncRcFailsafeAsync);
            }
            else if (parameterName.Equals(FenceEnableParam, StringComparison.OrdinalIgnoreCase))
            {
                RunSafe(SyncFenceEnabledAsync);
            }
            else if (parameterName.Equals(FenceActionParam, StringComparison.OrdinalIgnoreCase))
            {
                RunSafe(SyncFenceActionAsync);
            }
        });
    }

    private async Task SyncAllFromCacheAsync()
    {
        await SyncArmingChecksAsync();
        await SyncBattLowAsync();
        await SyncBattCriticalAsync();
        await SyncRcFailsafeAsync();
        await SyncFenceEnabledAsync();
        await SyncFenceActionAsync();
    }

    private async Task SyncArmingChecksAsync()
    {
        var param = await _parameterService.GetParameterAsync(ArmingParam);
        if (param == null) return;

        var mask = (int)Math.Round(param.Value);
        var accel = (mask & ArmingAccelerometerBit) != 0;
        var compass = (mask & ArmingCompassBit) != 0;
        var gps = (mask & ArmingGpsBit) != 0;
        var barometer = (mask & ArmingBarometerBit) != 0;
        var rc = (mask & ArmingRcBit) != 0;
        var ins = (mask & ArmingInsBit) != 0;
        var all = (mask & ArmingAllBit) == ArmingAllBit || (accel && compass && gps && barometer && rc && ins);

        _isSyncing = true;
        try
        {
            AccelerometerCheck = all || accel;
            CompassCheck = all || compass;
            GpsCheck = all || gps;
            BarometerCheck = all || barometer;
            RcCheck = all || rc;
            InsCheck = all || ins;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SyncBattLowAsync()
    {
        var param = await _parameterService.GetParameterAsync(BattLowParam);
        if (param == null) return;

        var value = (int)Math.Round(param.Value);
        _isSyncing = true;
        try
        {
            SelectedBattLowAction = BatteryActions.FirstOrDefault(o => o.Value == value);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SyncBattCriticalAsync()
    {
        var param = await _parameterService.GetParameterAsync(BattCriticalParam);
        if (param == null) return;

        var value = (int)Math.Round(param.Value);
        _isSyncing = true;
        try
        {
            SelectedBattCriticalAction = BatteryActions.FirstOrDefault(o => o.Value == value);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SyncRcFailsafeAsync()
    {
        var param = await _parameterService.GetParameterAsync(RcFailsafeParam);
        if (param == null) return;

        var value = (int)Math.Round(param.Value);
        _isSyncing = true;
        try
        {
            SelectedRcFailsafeAction = RcFailsafeActions.FirstOrDefault(o => o.Value == value);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SyncFenceEnabledAsync()
    {
        var param = await _parameterService.GetParameterAsync(FenceEnableParam);
        if (param == null) return;

        var value = (int)Math.Round(param.Value);
        _fenceEnableCached = value;

        _isSyncing = true;
        try
        {
            FenceEnabled = value > 0;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SyncFenceActionAsync()
    {
        var param = await _parameterService.GetParameterAsync(FenceActionParam);
        if (param == null) return;

        var value = (int)Math.Round(param.Value);
        _isSyncing = true;
        try
        {
            SelectedFenceAction = FenceActions.FirstOrDefault(o => o.Value == value);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private bool CanWrite() => _connectionService.IsConnected && _parameterService.IsParameterDownloadComplete;

    private async Task<bool> WriteParameterAsync(string name, float value)
    {
        if (!CanWrite())
        {
            StatusMessage = "Cannot write parameters - connection unavailable or download incomplete.";
            await SyncAllFromCacheAsync();
            return false;
        }

        return await _parameterService.SetParameterAsync(name, value);
    }

    private async Task UpdateArmingCheckAsync()
    {
        if (_isSyncing || !IsPageEnabled) return;

        var mask = BuildArmingMask();

        await ExecuteWriteAsync(async () =>
        {
            var success = await WriteParameterAsync(ArmingParam, mask);
            if (!success)
            {
                StatusMessage = "Failed to update arming checks.";
                await SyncArmingChecksAsync();
            }
            else
            {
                StatusMessage = "Arming checks updated.";
            }
        });
    }

    private int BuildArmingMask()
    {
        if (AccelerometerCheck && CompassCheck && GpsCheck && BarometerCheck && RcCheck && InsCheck)
        {
            return ArmingAllBit
                   | ArmingAccelerometerBit
                   | ArmingCompassBit
                   | ArmingGpsBit
                   | ArmingBarometerBit
                   | ArmingRcBit
                   | ArmingInsBit;
        }

        var mask = 0;
        if (AccelerometerCheck) mask |= ArmingAccelerometerBit;
        if (CompassCheck) mask |= ArmingCompassBit;
        if (GpsCheck) mask |= ArmingGpsBit;
        if (BarometerCheck) mask |= ArmingBarometerBit;
        if (RcCheck) mask |= ArmingRcBit;
        if (InsCheck) mask |= ArmingInsBit;

        return mask;
    }

    private async Task ApplyBattLowAsync(SafetyOption? option)
    {
        if (_isSyncing || option == null || !IsPageEnabled) return;

        await ExecuteWriteAsync(async () =>
        {
            var success = await WriteParameterAsync(BattLowParam, option.Value);
            if (!success)
            {
                StatusMessage = "Failed to update battery failsafe (low).";
                await SyncBattLowAsync();
            }
            else
            {
                StatusMessage = "Battery low failsafe updated.";
            }
        });
    }

    private async Task ApplyBattCriticalAsync(SafetyOption? option)
    {
        if (_isSyncing || option == null || !IsPageEnabled) return;

        await ExecuteWriteAsync(async () =>
        {
            var success = await WriteParameterAsync(BattCriticalParam, option.Value);
            if (!success)
            {
                StatusMessage = "Failed to update battery failsafe (critical).";
                await SyncBattCriticalAsync();
            }
            else
            {
                StatusMessage = "Battery critical failsafe updated.";
            }
        });
    }

    private async Task ApplyRcFailsafeAsync(SafetyOption? option)
    {
        if (_isSyncing || option == null || !IsPageEnabled) return;

        await ExecuteWriteAsync(async () =>
        {
            var success = await WriteParameterAsync(RcFailsafeParam, option.Value);
            if (!success)
            {
                StatusMessage = "Failed to update RC failsafe.";
                await SyncRcFailsafeAsync();
            }
            else
            {
                StatusMessage = "RC failsafe updated.";
            }
        });
    }

    private async Task ApplyFenceEnabledAsync(bool enabled)
    {
        if (_isSyncing || !IsPageEnabled) return;

        var previousEnable = _fenceEnableCached;

        await ExecuteWriteAsync(async () =>
        {
            var success = await WriteParameterAsync(FenceEnableParam, enabled ? 1f : 0f);
            if (!success)
            {
                StatusMessage = "Failed to update fence enable.";
                await SyncFenceEnabledAsync();
                return;
            }

            _fenceEnableCached = enabled ? 1 : 0;
            StatusMessage = enabled ? "GeoFence enabled." : "GeoFence disabled.";

            if (enabled && SelectedFenceAction != null)
            {
                var actionSuccess = await WriteParameterAsync(FenceActionParam, SelectedFenceAction.Value);
                if (!actionSuccess)
                {
                    StatusMessage = "Fence action update failed; reverting fence enable.";
                    var rollback = await WriteParameterAsync(FenceEnableParam, (float)(previousEnable ?? 0));
                    if (!rollback)
                    {
                        StatusMessage = "Fence action update failed and fence enable rollback failed.";
                    }
                    await SyncFenceEnabledAsync();
                }
                else
                {
                    StatusMessage = "Fence action updated.";
                }
            }
        });
    }

    private async Task ApplyFenceActionAsync(SafetyOption? option)
    {
        if (_isSyncing || option == null || !IsPageEnabled || !FenceEnabled) return;

        await ExecuteWriteAsync(async () =>
        {
            var success = await WriteParameterAsync(FenceActionParam, option.Value);
            if (!success)
            {
                StatusMessage = "Failed to update fence action.";
                await SyncFenceActionAsync();
            }
            else
            {
                StatusMessage = "Fence action updated.";
            }
        });
    }

    private async Task ExecuteWriteAsync(Func<Task> operation)
    {
        await _writeLock.WaitAsync();
        IsBusy = true;
        try
        {
            await operation();
        }
        finally
        {
            IsBusy = false;
            _writeLock.Release();
        }
    }

    partial void OnAccelerometerCheckChanged(bool value) => RunSafe(UpdateArmingCheckAsync);
    partial void OnCompassCheckChanged(bool value) => RunSafe(UpdateArmingCheckAsync);
    partial void OnGpsCheckChanged(bool value) => RunSafe(UpdateArmingCheckAsync);
    partial void OnBarometerCheckChanged(bool value) => RunSafe(UpdateArmingCheckAsync);
    partial void OnRcCheckChanged(bool value) => RunSafe(UpdateArmingCheckAsync);
    partial void OnInsCheckChanged(bool value) => RunSafe(UpdateArmingCheckAsync);

    partial void OnSelectedBattLowActionChanged(SafetyOption? value) => RunSafe(() => ApplyBattLowAsync(value));
    partial void OnSelectedBattCriticalActionChanged(SafetyOption? value) => RunSafe(() => ApplyBattCriticalAsync(value));
    partial void OnSelectedRcFailsafeActionChanged(SafetyOption? value) => RunSafe(() => ApplyRcFailsafeAsync(value));
    partial void OnSelectedFenceActionChanged(SafetyOption? value) => RunSafe(() => ApplyFenceActionAsync(value));
    partial void OnFenceEnabledChanged(bool value) => RunSafe(() => ApplyFenceEnabledAsync(value));

    private void RunSafe(Func<Task> asyncAction)
    {
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                StatusMessage = "Safety action failed. Check logs.";
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _parameterService.ParameterDownloadProgressChanged -= OnParameterDownloadProgressChanged;
        _parameterService.ParameterUpdated -= OnParameterUpdated;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _writeLock.Dispose();
    }
}

public sealed record SafetyOption(int Value, string Label);
