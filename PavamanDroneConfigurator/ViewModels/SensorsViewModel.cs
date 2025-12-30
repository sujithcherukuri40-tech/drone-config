using ReactiveUI;
using System.Reactive;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.ViewModels;

public class SensorsViewModel : ViewModelBase
{
    private readonly ICalibrationService _calibrationService;
    
    private string _selectedTab = "Accelerometer";
    private bool _isCalibrating;
    private int _calibrationProgress;
    private string _statusMessage = "Accelerometer is calibrated";

    public SensorsViewModel(ICalibrationService calibrationService)
    {
        _calibrationService = calibrationService;
        
        CalibrateAccelerometerCommand = ReactiveCommand.CreateFromTask(CalibrateAccelerometerAsync);
        CalibrateCompassCommand = ReactiveCommand.CreateFromTask(CalibrateCompassAsync);
        CalibrateLevelHorizonCommand = ReactiveCommand.CreateFromTask(CalibrateLevelHorizonAsync);
        CalibratePressureCommand = ReactiveCommand.CreateFromTask(CalibratePressureAsync);
        
        // Subscribe to calibration progress
        _calibrationService.Progress.Subscribe(progress =>
        {
            CalibrationProgress = progress.ProgressPercent;
            IsCalibrating = !progress.IsComplete;
        });
        
        _calibrationService.StatusMessage.Subscribe(message =>
        {
            StatusMessage = message;
        });
    }

    public string SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public bool IsCalibrating
    {
        get => _isCalibrating;
        set => this.RaiseAndSetIfChanged(ref _isCalibrating, value);
    }

    public int CalibrationProgress
    {
        get => _calibrationProgress;
        set => this.RaiseAndSetIfChanged(ref _calibrationProgress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public ReactiveCommand<Unit, Unit> CalibrateAccelerometerCommand { get; }
    public ReactiveCommand<Unit, Unit> CalibrateCompassCommand { get; }
    public ReactiveCommand<Unit, Unit> CalibrateLevelHorizonCommand { get; }
    public ReactiveCommand<Unit, Unit> CalibratePressureCommand { get; }

    private async Task CalibrateAccelerometerAsync()
    {
        SelectedTab = "Accelerometer";
        await _calibrationService.CalibrateAccelerometerAsync();
    }

    private async Task CalibrateCompassAsync()
    {
        SelectedTab = "Compass";
        await _calibrationService.CalibrateMagnetometerAsync();
    }

    private async Task CalibrateLevelHorizonAsync()
    {
        SelectedTab = "LevelHorizon";
        await _calibrationService.CalibrateLevelHorizonAsync();
    }

    private async Task CalibratePressureAsync()
    {
        SelectedTab = "Pressure";
        await _calibrationService.CalibrateBarometerAsync();
    }
}
