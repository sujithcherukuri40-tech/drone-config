using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class SafetyPageViewModel : ViewModelBase
{
    private readonly ISafetyService _safetyService;
    private readonly IConnectionService _connectionService;

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

    public SafetyPageViewModel(ISafetyService safetyService, IConnectionService connectionService)
    {
        _safetyService = safetyService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = _connectionService.IsConnected;
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
        }
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
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
}
