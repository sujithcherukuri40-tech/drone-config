using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class CalibrationPageViewModel : ViewModelBase
{
    private readonly ICalibrationService _calibrationService;

    [ObservableProperty]
    private CalibrationStateModel? _currentState;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public CalibrationPageViewModel(ICalibrationService calibrationService)
    {
        _calibrationService = calibrationService;

        _calibrationService.CalibrationStateChanged += (s, state) =>
        {
            CurrentState = state;
            StatusMessage = state.Message ?? "Ready";
        };
    }

    [RelayCommand]
    private async Task CalibrateAccelerometerAsync()
    {
        await _calibrationService.StartCalibrationAsync(CalibrationType.Accelerometer);
    }

    [RelayCommand]
    private async Task CalibrateCompassAsync()
    {
        await _calibrationService.StartCalibrationAsync(CalibrationType.Compass);
    }

    [RelayCommand]
    private async Task CalibrateGyroscopeAsync()
    {
        await _calibrationService.StartCalibrationAsync(CalibrationType.Gyroscope);
    }

    [RelayCommand]
    private async Task CancelCalibrationAsync()
    {
        await _calibrationService.CancelCalibrationAsync();
    }
}
