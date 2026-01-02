using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private int? _armingCached;
    private int? _battLowCached;
    private int? _battCriticalCached;
    private int? _rcFailsafeCached;
    private int? _fenceEnableCached;
    private int? _fenceActionCached;

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
    [ObservableProperty] private string _statusMessage = "Downloading parameters…";

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
        StatusMessage = IsPageEnabled ? "Safety parameters loaded." : "Downloading parameters…";

        if (IsPageEnabled)
        {
            _ = SyncAllFromCacheAsync();
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
                _ = SyncAllFromCacheAsync();
            }
            else
            {
                StatusMessage = connected ? "Downloading parameters…" : "Disconnected - parameters unavailable";
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
                _ = SyncAllFromCacheAsync();
            }
            else if (_parameterService.IsParameterDownloadInProgress)
            {
                var expected = _parameterService.ExpectedParameterCount?.ToString() ?? "?";
                StatusMessage = $"Downloading parameters… {_parameterService.ReceivedParameterCount}/{expected}";
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
                _ = SyncArmingChecksAsync();
            }
            else if (parameterName.Equals(BattLowParam, StringComparison.OrdinalIgnoreCase))
            {
                _ = SyncBattLowAsync();
            }
            else if (parameterName.Equals(BattCriticalParam, StringComparison.OrdinalIgnoreCase))
            {
                _ = SyncBattCriticalAsync();
            }
            else if (parameterName.Equals(RcFailsafeParam, StringComparison.OrdinalIgnoreCase))
            {
                _ = SyncRcFailsafeAsync();
            }
            else if (parameterName.Equals(FenceEnableParam, StringComparison.OrdinalIgnoreCase))
            {
                _ = SyncFenceEnabledAsync();
            }
            else if (parameterName.Equals(FenceActionParam, StringComparison.OrdinalIgnoreCase))
            {
                _ = SyncFenceActionAsync();
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
        _armingCached = mask;

        var all = (mask & ArmingAllBit) == ArmingAllBit;

        _isSyncing = true;
        try
        {
            AccelerometerCheck = all || (mask & ArmingAccelerometerBit) != 0;
            CompassCheck = all || (mask & ArmingCompassBit) != 0;
            GpsCheck = all || (mask & ArmingGpsBit) != 0;
            BarometerCheck = all || (mask & ArmingBarometerBit) != 0;
            RcCheck = all || (mask & ArmingRcBit) != 0;
            InsCheck = all || (mask & ArmingInsBit) != 0;
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
        _battLowCached = value;

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
        _battCriticalCached = value;

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
        _rcFailsafeCached = value;

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
        _fenceActionCached = value;

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

    private async Task<bool> WriteParameterAsync(string name, int value)
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
                _armingCached = mask;
                StatusMessage = "Arming checks updated.";
            }
        });
    }

    private int BuildArmingMask()
    {
        if (AccelerometerCheck && CompassCheck && GpsCheck && BarometerCheck && RcCheck && InsCheck)
        {
            return ArmingAllBit;
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
                _battLowCached = option.Value;
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
                _battCriticalCached = option.Value;
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
                _rcFailsafeCached = option.Value;
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
            var success = await WriteParameterAsync(FenceEnableParam, enabled ? 1 : 0);
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
                    await WriteParameterAsync(FenceEnableParam, previousEnable ?? 0);
                    await SyncFenceEnabledAsync();
                }
                else
                {
                    _fenceActionCached = SelectedFenceAction.Value;
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
                _fenceActionCached = option.Value;
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

    partial void OnAccelerometerCheckChanged(bool value) => _ = UpdateArmingCheckAsync();
    partial void OnCompassCheckChanged(bool value) => _ = UpdateArmingCheckAsync();
    partial void OnGpsCheckChanged(bool value) => _ = UpdateArmingCheckAsync();
    partial void OnBarometerCheckChanged(bool value) => _ = UpdateArmingCheckAsync();
    partial void OnRcCheckChanged(bool value) => _ = UpdateArmingCheckAsync();
    partial void OnInsCheckChanged(bool value) => _ = UpdateArmingCheckAsync();

    partial void OnSelectedBattLowActionChanged(SafetyOption? value) => _ = ApplyBattLowAsync(value);
    partial void OnSelectedBattCriticalActionChanged(SafetyOption? value) => _ = ApplyBattCriticalAsync(value);
    partial void OnSelectedRcFailsafeActionChanged(SafetyOption? value) => _ = ApplyRcFailsafeAsync(value);
    partial void OnSelectedFenceActionChanged(SafetyOption? value) => _ = ApplyFenceActionAsync(value);
    partial void OnFenceEnabledChanged(bool value) => _ = ApplyFenceEnabledAsync(value);

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
