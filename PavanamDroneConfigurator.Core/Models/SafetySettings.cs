using PavanamDroneConfigurator.Core.Enums;

namespace PavanamDroneConfigurator.Core.Models;

public class SafetySettings
{
    // Battery Failsafe
    public float BattMonitor { get; set; }
    public float BattLowVolt { get; set; }
    public float BattCrtVolt { get; set; }
    public FailsafeAction BattFsLowAct { get; set; }
    public FailsafeAction BattFsCrtAct { get; set; }
    public float BattCapacity { get; set; }

    // RC Failsafe
    public float FsThrEnable { get; set; }
    public float FsThrValue { get; set; }
    public FailsafeAction FsThrAction { get; set; }

    // GCS Failsafe
    public float FsGcsEnable { get; set; }
    public float FsGcsTimeout { get; set; }
    public FailsafeAction FsGcsAction { get; set; }

    // Crash / Land Safety
    public float CrashDetect { get; set; }
    public FailsafeAction CrashAction { get; set; }
    public float LandDetect { get; set; }

    // Arming Checks (bitmask)
    public int ArmingCheck { get; set; }

    // Geo-Fence
    public float FenceEnable { get; set; }
    public float FenceType { get; set; }
    public float FenceAltMax { get; set; }
    public float FenceRadius { get; set; }
    public FailsafeAction FenceAction { get; set; }

    // Motor Safety
    public float MotSafeDisarm { get; set; }
    public float MotEmergencyStop { get; set; }
}
