using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Services.Interfaces;

public interface ICalibrationService
{
    Task<bool> CalibrateGyroscopeAsync();
    Task<bool> CalibrateMagnetometerAsync();
    Task<bool> CalibrateBarometerAsync();
    Task<bool> CalibrateRcTrimAsync();
    Task<bool> CalibrateAccelerometerAsync();
    Task<bool> CalibrateLevelHorizonAsync();
    Task<bool> CalibrateEscAsync();
    
    IObservable<CalibrationProgress> Progress { get; }
    IObservable<string> StatusMessage { get; }
}

public class CalibrationProgress
{
    public CalibrationType Type { get; set; }
    public int ProgressPercent { get; set; }
    public string? CurrentStep { get; set; }
    public bool IsComplete { get; set; }
}
