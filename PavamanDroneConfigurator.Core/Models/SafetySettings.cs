using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

public class SafetySettings
{
    // Battery Failsafe
    public double LowVoltageThreshold { get; set; } = 10.5;
    public double CriticalVoltageThreshold { get; set; } = 10.0;
    public int LowMahThreshold { get; set; } = 0;
    public int CriticalMahThreshold { get; set; } = 0;
    public FailsafeAction LowBatteryAction { get; set; } = FailsafeAction.RTL;
    public FailsafeAction CriticalBatteryAction { get; set; } = FailsafeAction.Land;
    
    // Ground Station Failsafe
    public bool GcsFailsafeEnabled { get; set; } = true;
    public FailsafeAction GcsFailsafeAction { get; set; } = FailsafeAction.RTL;
    public int GcsTimeoutSeconds { get; set; } = 5;
    
    // RC/Throttle Failsafe
    public bool ThrottleFailsafeEnabled { get; set; } = true;
    public int ThrottlePwmThreshold { get; set; } = 975;
    public FailsafeAction ThrottleFailsafeAction { get; set; } = FailsafeAction.RTL;
    
    // Geofence
    public bool GeofenceEnabled { get; set; } = false;
    public double MaxAltitude { get; set; } = 100;
    public double MaxRadius { get; set; } = 500;
    public FailsafeAction GeofenceAction { get; set; } = FailsafeAction.RTL;
    
    // RTL Settings
    public double RtlAltitude { get; set; } = 15;
    public double RtlSpeed { get; set; } = 5;
    public bool RtlClimbFirst { get; set; } = true;
}
