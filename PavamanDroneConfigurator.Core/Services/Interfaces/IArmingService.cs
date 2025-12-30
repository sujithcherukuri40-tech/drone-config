namespace PavamanDroneConfigurator.Core.Services.Interfaces;

/// <summary>
/// Service for arming/disarming and reboot operations using MAVLink commands
/// </summary>
public interface IArmingService
{
    /// <summary>
    /// Arm the vehicle using MAV_CMD_COMPONENT_ARM_DISARM (400)
    /// param1 = 1 (arm), param2 = force flag
    /// </summary>
    Task<bool> ArmAsync(bool force = false);
    
    /// <summary>
    /// Disarm the vehicle using MAV_CMD_COMPONENT_ARM_DISARM (400)
    /// param1 = 0 (disarm), param2 = force flag
    /// </summary>
    Task<bool> DisarmAsync(bool force = false);
    
    /// <summary>
    /// Reboot autopilot using MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
    /// param1 = 1 (reboot autopilot)
    /// </summary>
    Task<bool> RebootAsync();
    
    /// <summary>
    /// Shutdown autopilot using MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
    /// param1 = 2 (shutdown autopilot)
    /// </summary>
    Task<bool> ShutdownAsync();
    
    /// <summary>
    /// Check if vehicle is armed
    /// </summary>
    bool IsArmed { get; }
}
