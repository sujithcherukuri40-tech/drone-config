using PavanamDroneConfigurator.Core.Enums;

namespace PavanamDroneConfigurator.Core.Models;

public class SafetySettings
{
    public FailsafeAction BatteryFailsafe { get; set; } = FailsafeAction.ReturnToLaunch;
    public FailsafeAction GpsFailsafe { get; set; } = FailsafeAction.Land;
    public double BatteryLowVoltage { get; set; } = 10.5;
    public double BatteryCriticalVoltage { get; set; } = 9.5;
    public double ReturnToLaunchAltitude { get; set; } = 50.0;
    public bool GeofenceEnabled { get; set; }
    public double GeofenceRadius { get; set; } = 100.0;
}
