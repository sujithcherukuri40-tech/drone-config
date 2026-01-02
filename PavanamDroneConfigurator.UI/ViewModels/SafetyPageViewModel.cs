using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;
using PavanamDroneConfigurator.Core.Interfaces;

namespace PavanamDroneConfigurator.UI.ViewModels;

public sealed partial class SafetyPageViewModel : ViewModelBase, IDisposable
{
    private readonly ISafetyService _safetyService;
    private readonly IConnectionService _connectionService;
    private readonly IParameterService _parameterService;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isLoading;

    // Battery Failsafe
    [ObservableProperty]
    private float _battMonitor;

    [ObservableProperty]
    private float _battLowVolt;

    [ObservableProperty]
    private float _battCrtVolt;

    [ObservableProperty]
    private FailsafeAction _battFsLowAct;

    [ObservableProperty]
    private FailsafeAction _battFsCrtAct;

    [ObservableProperty]
    private float _battCapacity;

    // RC Failsafe
    [ObservableProperty]
    private float _fsThrEnable;

    [ObservableProperty]
    private float _fsThrValue;

    [ObservableProperty]
    private FailsafeAction _fsThrAction;

    // GCS Failsafe
    [ObservableProperty]
    private float _fsGcsEnable;

    [ObservableProperty]
    private float _fsGcsTimeout;

    [ObservableProperty]
    private FailsafeAction _fsGcsAction;

    // Crash / Land Safety
    [ObservableProperty]
    private float _crashDetect;

    [ObservableProperty]
    private FailsafeAction _crashAction;

    [ObservableProperty]
    private float _landDetect;

    // Arming Checks (individual toggles) - ArduPilot ARMING_CHECK bitmask
    [ObservableProperty]
    private bool _armingCheckGps;

    [ObservableProperty]
    private bool _armingCheckCompass;

    [ObservableProperty]
    private bool _armingCheckIns;

    [ObservableProperty]
    private bool _armingCheckBattery;

    [ObservableProperty]
    private bool _armingCheckRc;

    [ObservableProperty]
    private bool _armingCheckEkf;

    // Geo-Fence
    [ObservableProperty]
    private float _fenceEnable;

    [ObservableProperty]
    private float _fenceType;

    [ObservableProperty]
    private float _fenceAltMax;

    [ObservableProperty]
    private float _fenceRadius;

    [ObservableProperty]
    private FailsafeAction _fenceAction;

    // Motor Safety
    [ObservableProperty]
    private float _motSafeDisarm;

    [ObservableProperty]
    private float _motEmergencyStop;

    public List<FailsafeAction> AvailableFailsafeActions { get; } = new()
    {
        FailsafeAction.None,
        FailsafeAction.Land,
        FailsafeAction.ReturnToLaunch,
        FailsafeAction.Disarm
    };

    public SafetyPageViewModel(ISafetyService safetyService, IConnectionService connectionService, IParameterService parameterService)
    {
        _safetyService = safetyService;
        _connectionService = connectionService;
        _parameterService = parameterService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterUpdated += OnParameterUpdated;
        IsConnected = _connectionService.IsConnected;
    }

    private void OnParameterUpdated(object? sender, DroneParameter updatedParam)
    {
        try
        {
            // Update the corresponding UI property when parameter changes
            switch (updatedParam.Name)
            {
                // Battery Failsafe
                case "BATT_MONITOR":
                    BattMonitor = updatedParam.Value;
                    break;
                case "BATT_LOW_VOLT":
                    BattLowVolt = updatedParam.Value;
                    break;
                case "BATT_CRT_VOLT":
                    BattCrtVolt = updatedParam.Value;
                    break;
                case "BATT_FS_LOW_ACT":
                    BattFsLowAct = (FailsafeAction)updatedParam.Value;
                    break;
                case "BATT_FS_CRT_ACT":
                    BattFsCrtAct = (FailsafeAction)updatedParam.Value;
                    break;
                case "BATT_CAPACITY":
                    BattCapacity = updatedParam.Value;
                    break;

                // RC Failsafe
                case "FS_THR_ENABLE":
                    FsThrEnable = updatedParam.Value;
                    break;
                case "FS_THR_VALUE":
                    FsThrValue = updatedParam.Value;
                    break;
                case "FS_THR_ACTION":
                    FsThrAction = (FailsafeAction)updatedParam.Value;
                    break;

                // GCS Failsafe
                case "FS_GCS_ENABLE":
                    FsGcsEnable = updatedParam.Value;
                    break;
                case "FS_GCS_TIMEOUT":
                    FsGcsTimeout = updatedParam.Value;
                    break;
                case "FS_GCS_ACTION":
                    FsGcsAction = (FailsafeAction)updatedParam.Value;
                    break;

                // Crash / Land Safety
                case "CRASH_DETECT":
                    CrashDetect = updatedParam.Value;
                    break;
                case "CRASH_ACTION":
                    CrashAction = (FailsafeAction)updatedParam.Value;
                    break;
                case "LAND_DETECT":
                    LandDetect = updatedParam.Value;
                    break;

                // Arming Checks
                case "ARMING_CHECK":
                    DecodeArmingChecks((int)updatedParam.Value);
                    break;

                // Geo-Fence
                case "FENCE_ENABLE":
                    FenceEnable = updatedParam.Value;
                    break;
                case "FENCE_TYPE":
                    FenceType = updatedParam.Value;
                    break;
                case "FENCE_ALT_MAX":
                    FenceAltMax = updatedParam.Value;
                    break;
                case "FENCE_RADIUS":
                    FenceRadius = updatedParam.Value;
                    break;
                case "FENCE_ACTION":
                    FenceAction = (FailsafeAction)updatedParam.Value;
                    break;

                // Motor Safety
                case "MOT_SAFE_DISARM":
                    MotSafeDisarm = updatedParam.Value;
                    break;
                case "MOT_EMERGENCY_STOP":
                    MotEmergencyStop = updatedParam.Value;
                    break;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            StatusMessage = $"Error updating parameter {updatedParam.Name}: {ex.Message}";
        }
    }

    private async void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        
        if (connected)
        {
            await LoadSettingsAsync();
        }
        else
        {
            StatusMessage = "Disconnected";
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
        var accel = HasBit(mask, ArmingAccelerometerBit);
        var compass = HasBit(mask, ArmingCompassBit);
        var gps = HasBit(mask, ArmingGpsBit);
        var barometer = HasBit(mask, ArmingBarometerBit);
        var rc = HasBit(mask, ArmingRcBit);
        var ins = HasBit(mask, ArmingInsBit);
        var all = AreAllArmingChecksEnabled(accel, compass, gps, barometer, rc, ins, mask);

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
        if (!IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading safety settings...";

        try
        {
            var settings = await _safetyService.GetSafetySettingsAsync();
            if (settings != null)
            {
                // Battery Failsafe
                BattMonitor = settings.BattMonitor;
                BattLowVolt = settings.BattLowVolt;
                BattCrtVolt = settings.BattCrtVolt;
                BattFsLowAct = settings.BattFsLowAct;
                BattFsCrtAct = settings.BattFsCrtAct;
                BattCapacity = settings.BattCapacity;

                // RC Failsafe
                FsThrEnable = settings.FsThrEnable;
                FsThrValue = settings.FsThrValue;
                FsThrAction = settings.FsThrAction;

                // GCS Failsafe
                FsGcsEnable = settings.FsGcsEnable;
                FsGcsTimeout = settings.FsGcsTimeout;
                FsGcsAction = settings.FsGcsAction;

                // Crash / Land Safety
                CrashDetect = settings.CrashDetect;
                CrashAction = settings.CrashAction;
                LandDetect = settings.LandDetect;

                // Arming Checks - decode bitmask
                DecodeArmingChecks(settings.ArmingCheck);

                // Geo-Fence
                FenceEnable = settings.FenceEnable;
                FenceType = settings.FenceType;
                FenceAltMax = settings.FenceAltMax;
                FenceRadius = settings.FenceRadius;
                FenceAction = settings.FenceAction;

                // Motor Safety
                MotSafeDisarm = settings.MotSafeDisarm;
                MotEmergencyStop = settings.MotEmergencyStop;

                StatusMessage = "Safety settings loaded successfully";
            }
            else
            {
                StatusMessage = "Failed to load safety settings";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected. Cannot apply settings.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Applying safety settings...";

        try
        {
            var settings = new SafetySettings
            {
                // Battery Failsafe
                BattMonitor = BattMonitor,
                BattLowVolt = BattLowVolt,
                BattCrtVolt = BattCrtVolt,
                BattFsLowAct = BattFsLowAct,
                BattFsCrtAct = BattFsCrtAct,
                BattCapacity = BattCapacity,

                // RC Failsafe
                FsThrEnable = FsThrEnable,
                FsThrValue = FsThrValue,
                FsThrAction = FsThrAction,

                // GCS Failsafe
                FsGcsEnable = FsGcsEnable,
                FsGcsTimeout = FsGcsTimeout,
                FsGcsAction = FsGcsAction,

                // Crash / Land Safety
                CrashDetect = CrashDetect,
                CrashAction = CrashAction,
                LandDetect = LandDetect,

                // Arming Checks - encode bitmask
                ArmingCheck = EncodeArmingChecks(),

                // Geo-Fence
                FenceEnable = FenceEnable,
                FenceType = FenceType,
                FenceAltMax = FenceAltMax,
                FenceRadius = FenceRadius,
                FenceAction = FenceAction,

                // Motor Safety
                MotSafeDisarm = MotSafeDisarm,
                MotEmergencyStop = MotEmergencyStop
            };

            var success = await _safetyService.UpdateSafetySettingsAsync(settings);
            StatusMessage = success 
                ? "Safety settings applied successfully" 
                : "Failed to apply safety settings";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSettingsAsync()
    {
        await LoadSettingsAsync();
    }

    private void DecodeArmingChecks(int bitmask)
    {
        // ArduPilot ARMING_CHECK bitmask values (matching Mission Planner)
        // Based on ArduPilot source: https://github.com/ArduPilot/ardupilot
        ArmingCheckGps = (bitmask & 0x08) != 0;       // Bit 3: GPS
        ArmingCheckCompass = (bitmask & 0x04) != 0;   // Bit 2: Compass
        ArmingCheckIns = (bitmask & 0x10) != 0;       // Bit 4: INS (Inertial Nav System)
        ArmingCheckBattery = (bitmask & 0x100) != 0;  // Bit 8: Battery level
        ArmingCheckRc = (bitmask & 0x40) != 0;        // Bit 6: RC Channels
        ArmingCheckEkf = (bitmask & 0x400) != 0;      // Bit 10: Logging/EKF
    }

    private int EncodeArmingChecks()
    {
        int bitmask = 0;
        
        // ArduPilot ARMING_CHECK bitmask values (matching Mission Planner)
        if (ArmingCheckGps) bitmask |= 0x08;       // Bit 3: GPS
        if (ArmingCheckCompass) bitmask |= 0x04;   // Bit 2: Compass
        if (ArmingCheckIns) bitmask |= 0x10;       // Bit 4: INS
        if (ArmingCheckBattery) bitmask |= 0x100;  // Bit 8: Battery level
        if (ArmingCheckRc) bitmask |= 0x40;        // Bit 6: RC Channels
        if (ArmingCheckEkf) bitmask |= 0x400;      // Bit 10: Logging/EKF
        
        return bitmask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events to prevent memory leaks
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _parameterService.ParameterUpdated -= OnParameterUpdated;
        }
        base.Dispose(disposing);
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
        var mask = 0;
        if (AccelerometerCheck) mask |= ArmingAccelerometerBit;
        if (CompassCheck) mask |= ArmingCompassBit;
        if (GpsCheck) mask |= ArmingGpsBit;
        if (BarometerCheck) mask |= ArmingBarometerBit;
        if (RcCheck) mask |= ArmingRcBit;
        if (InsCheck) mask |= ArmingInsBit;

        if (AreAllArmingChecksEnabled(AccelerometerCheck, CompassCheck, GpsCheck, BarometerCheck, RcCheck, InsCheck, 0))
        {
            mask |= ArmingAllBit;
        }

        return mask;
    }

    private static bool HasBit(int mask, int bit) => (mask & bit) != 0;

    private static bool AreAllArmingChecksEnabled(bool accel, bool compass, bool gps, bool barometer, bool rc, bool ins, int mask) =>
        (mask & ArmingAllBit) == ArmingAllBit || (accel && compass && gps && barometer && rc && ins);

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
                    else
                    {
                        _fenceEnableCached = previousEnable;
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
                StatusMessage = "Safety operation failed; the requested change may not be applied. See logs for details.";
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
