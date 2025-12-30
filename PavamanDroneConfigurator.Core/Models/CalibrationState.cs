using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

public class CalibrationState
{
    public CalibrationType Type { get; set; }
    public bool IsCalibrated { get; set; }
    public bool IsCalibrating { get; set; }
    public int Progress { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime? LastCalibration { get; set; }
}
