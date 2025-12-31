using PavanamDroneConfigurator.Core.Enums;

namespace PavanamDroneConfigurator.Core.Models;

public class CalibrationStateModel
{
    public CalibrationType Type { get; set; }
    public CalibrationState State { get; set; }
    public int Progress { get; set; }
    public string? Message { get; set; }
}
