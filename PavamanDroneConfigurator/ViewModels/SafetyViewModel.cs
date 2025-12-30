using ReactiveUI;
using System.Reactive;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Services.Interfaces;

namespace PavamanDroneConfigurator.ViewModels;

public class SafetyViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    
    private string _selectedTab = "Battery";
    private SafetySettings _settings = new();

    public SafetyViewModel(IParameterService parameterService)
    {
        _parameterService = parameterService;
        
        UpdateBatterySettingsCommand = ReactiveCommand.CreateFromTask(UpdateBatterySettingsAsync);
        UpdateRtlSettingsCommand = ReactiveCommand.CreateFromTask(UpdateRtlSettingsAsync);
        UpdateGeofenceSettingsCommand = ReactiveCommand.CreateFromTask(UpdateGeofenceSettingsAsync);
        UpdateFailsafeSettingsCommand = ReactiveCommand.CreateFromTask(UpdateFailsafeSettingsAsync);
    }

    public string SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public SafetySettings Settings
    {
        get => _settings;
        set => this.RaiseAndSetIfChanged(ref _settings, value);
    }

    public double CriticalVoltageThreshold
    {
        get => _settings.CriticalVoltageThreshold;
        set
        {
            _settings.CriticalVoltageThreshold = value;
            this.RaisePropertyChanged();
        }
    }

    public int CriticalMahThreshold
    {
        get => _settings.CriticalMahThreshold;
        set
        {
            _settings.CriticalMahThreshold = value;
            this.RaisePropertyChanged();
        }
    }

    public int ThrottlePwmThreshold
    {
        get => _settings.ThrottlePwmThreshold;
        set
        {
            _settings.ThrottlePwmThreshold = value;
            this.RaisePropertyChanged();
        }
    }

    public ReactiveCommand<Unit, Unit> UpdateBatterySettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateRtlSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateGeofenceSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateFailsafeSettingsCommand { get; }

    private async Task UpdateBatterySettingsAsync()
    {
        await _parameterService.WriteParameterAsync("BATT_CRT_VOLT", (float)Settings.CriticalVoltageThreshold);
        await _parameterService.WriteParameterAsync("BATT_LOW_VOLT", (float)Settings.LowVoltageThreshold);
        await _parameterService.WriteParameterAsync("BATT_CRT_MAH", Settings.CriticalMahThreshold);
        await _parameterService.WriteParameterAsync("BATT_FS_LOW_ACT", (float)Settings.LowBatteryAction);
    }

    private async Task UpdateRtlSettingsAsync()
    {
        await _parameterService.WriteParameterAsync("RTL_ALT", (float)Settings.RtlAltitude);
        await _parameterService.WriteParameterAsync("RTL_SPEED", (float)Settings.RtlSpeed);
    }

    private async Task UpdateGeofenceSettingsAsync()
    {
        await _parameterService.WriteParameterAsync("FENCE_ENABLE", Settings.GeofenceEnabled ? 1 : 0);
        await _parameterService.WriteParameterAsync("FENCE_ALT_MAX", (float)Settings.MaxAltitude);
        await _parameterService.WriteParameterAsync("FENCE_RADIUS", (float)Settings.MaxRadius);
        await _parameterService.WriteParameterAsync("FENCE_ACTION", (float)Settings.GeofenceAction);
    }

    private async Task UpdateFailsafeSettingsAsync()
    {
        await _parameterService.WriteParameterAsync("FS_GCS_ENABLE", Settings.GcsFailsafeEnabled ? 1 : 0);
        await _parameterService.WriteParameterAsync("FS_THR_ENABLE", Settings.ThrottleFailsafeEnabled ? 1 : 0);
        await _parameterService.WriteParameterAsync("FS_THR_VALUE", Settings.ThrottlePwmThreshold);
    }
}
