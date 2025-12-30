using PavamanDroneConfigurator.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Flight mode service using MAV_CMD_DO_SET_MODE command (176)
/// and FLTMODE parameters for ArduPilot
/// </summary>
public class FlightModeService : IFlightModeService
{
    private readonly ILogger<FlightModeService> _logger;
    private readonly IMavlinkService _mavlinkService;
    private string? _currentFlightMode;

    // ArduPilot Copter flight modes
    private readonly Dictionary<string, int> _copterModes = new()
    {
        { "STABILIZE", 0 },
        { "ACRO", 1 },
        { "ALT_HOLD", 2 },
        { "AUTO", 3 },
        { "GUIDED", 4 },
        { "LOITER", 5 },
        { "RTL", 6 },
        { "CIRCLE", 7 },
        { "LAND", 9 },
        { "DRIFT", 11 },
        { "SPORT", 13 },
        { "FLIP", 14 },
        { "AUTOTUNE", 15 },
        { "POSHOLD", 16 },
        { "BRAKE", 17 },
        { "THROW", 18 },
        { "AVOID_ADSB", 19 },
        { "GUIDED_NOGPS", 20 },
        { "SMART_RTL", 21 },
        { "FLOWHOLD", 22 },
        { "FOLLOW", 23 },
        { "ZIGZAG", 24 },
        { "SYSTEMID", 25 },
        { "AUTOROTATE", 26 }
    };

    public FlightModeService(ILogger<FlightModeService> logger, IMavlinkService mavlinkService)
    {
        _logger = logger;
        _mavlinkService = mavlinkService;
    }

    public string? CurrentFlightMode => _currentFlightMode;

    /// <summary>
    /// Set flight mode using MAV_CMD_DO_SET_MODE (176)
    /// param1 = base mode (MAV_MODE_FLAG), param2 = custom mode (vehicle-specific)
    /// For ArduPilot: param1 = 1 (MAV_MODE_FLAG_CUSTOM_MODE_ENABLED), param2 = mode number
    /// </summary>
    public async Task<bool> SetFlightModeAsync(string modeName)
    {
        try
        {
            if (!_copterModes.TryGetValue(modeName.ToUpper(), out int modeNumber))
            {
                _logger.LogWarning("Unknown flight mode: {Mode}", modeName);
                return false;
            }

            _logger.LogInformation("Setting flight mode to {Mode} (mode number: {Number})", modeName, modeNumber);

            // MAV_CMD_DO_SET_MODE (176)
            // param1 = 1 (MAV_MODE_FLAG_CUSTOM_MODE_ENABLED)
            // param2 = custom mode number (ArduPilot-specific)
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_DO_SET_MODE,
                1,              // MAV_MODE_FLAG_CUSTOM_MODE_ENABLED
                modeNumber,     // Custom mode number
                0, 0, 0, 0, 0);

            if (result == MavResult.MAV_RESULT_ACCEPTED)
            {
                _currentFlightMode = modeName;
                _logger.LogInformation("Flight mode set to {Mode} successfully", modeName);
                return true;
            }
            else
            {
                _logger.LogWarning("Set flight mode command rejected: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set flight mode to {Mode}", modeName);
            return false;
        }
    }

    /// <summary>
    /// Get available flight modes for ArduPilot Copter
    /// </summary>
    public Task<List<string>> GetAvailableFlightModesAsync()
    {
        var modes = _copterModes.Keys.ToList();
        _logger.LogInformation("Retrieved {Count} available flight modes", modes.Count);
        return Task.FromResult(modes);
    }
}
