using PavamanDroneConfigurator.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Arming service using MAVLink commands
/// MAV_CMD_COMPONENT_ARM_DISARM (400) for arm/disarm
/// MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246) for reboot/shutdown
/// </summary>
public class ArmingService : IArmingService
{
    private readonly ILogger<ArmingService> _logger;
    private readonly IMavlinkService _mavlinkService;
    private bool _isArmed;

    public ArmingService(ILogger<ArmingService> logger, IMavlinkService mavlinkService)
    {
        _logger = logger;
        _mavlinkService = mavlinkService;
    }

    public bool IsArmed => _isArmed;

    /// <summary>
    /// Arm the vehicle using MAV_CMD_COMPONENT_ARM_DISARM (400)
    /// param1 = 1 (arm), param2 = force flag (21196 = force)
    /// </summary>
    public async Task<bool> ArmAsync(bool force = false)
    {
        try
        {
            _logger.LogInformation("Arming vehicle (force={Force})", force);

            // MAV_CMD_COMPONENT_ARM_DISARM (400)
            // param1 = 1 (arm)
            // param2 = force flag (21196 to force, 0 for normal)
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_COMPONENT_ARM_DISARM,
                1,                      // Arm
                force ? 21196 : 0,      // Force flag
                0, 0, 0, 0, 0);

            if (result == MavResult.MAV_RESULT_ACCEPTED)
            {
                _isArmed = true;
                _logger.LogInformation("Vehicle armed successfully");
                return true;
            }
            else
            {
                _logger.LogWarning("Arm command rejected: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to arm vehicle");
            return false;
        }
    }

    /// <summary>
    /// Disarm the vehicle using MAV_CMD_COMPONENT_ARM_DISARM (400)
    /// param1 = 0 (disarm), param2 = force flag (21196 = force)
    /// </summary>
    public async Task<bool> DisarmAsync(bool force = false)
    {
        try
        {
            _logger.LogInformation("Disarming vehicle (force={Force})", force);

            // MAV_CMD_COMPONENT_ARM_DISARM (400)
            // param1 = 0 (disarm)
            // param2 = force flag
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_COMPONENT_ARM_DISARM,
                0,                      // Disarm
                force ? 21196 : 0,      // Force flag
                0, 0, 0, 0, 0);

            if (result == MavResult.MAV_RESULT_ACCEPTED)
            {
                _isArmed = false;
                _logger.LogInformation("Vehicle disarmed successfully");
                return true;
            }
            else
            {
                _logger.LogWarning("Disarm command rejected: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disarm vehicle");
            return false;
        }
    }

    /// <summary>
    /// Reboot autopilot using MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
    /// param1 = 1 (reboot autopilot), param2 = 0, param3 = 0 (reboot autopilot and keep it in bootloader)
    /// </summary>
    public async Task<bool> RebootAsync()
    {
        try
        {
            _logger.LogInformation("Rebooting autopilot");

            // MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
            // param1 = 1 (reboot autopilot)
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN,
                1,      // Reboot autopilot
                0,      // Onboard computer
                0,      // Camera
                0,      // Mount
                0, 0, 0);

            if (result == MavResult.MAV_RESULT_ACCEPTED)
            {
                _logger.LogInformation("Reboot command sent successfully");
                return true;
            }
            else
            {
                _logger.LogWarning("Reboot command rejected: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reboot autopilot");
            return false;
        }
    }

    /// <summary>
    /// Shutdown autopilot using MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
    /// param1 = 2 (shutdown autopilot)
    /// </summary>
    public async Task<bool> ShutdownAsync()
    {
        try
        {
            _logger.LogInformation("Shutting down autopilot");

            // MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)
            // param1 = 2 (shutdown autopilot)
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN,
                2,      // Shutdown autopilot
                0, 0, 0, 0, 0, 0);

            if (result == MavResult.MAV_RESULT_ACCEPTED)
            {
                _logger.LogInformation("Shutdown command sent successfully");
                return true;
            }
            else
            {
                _logger.LogWarning("Shutdown command rejected: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to shutdown autopilot");
            return false;
        }
    }
}
