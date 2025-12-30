namespace PavamanDroneConfigurator.Core.Services.Interfaces;

/// <summary>
/// Service for flight mode operations using MAVLink commands
/// Uses MAV_CMD_DO_SET_MODE command (176) and parameters
/// </summary>
public interface IFlightModeService
{
    /// <summary>
    /// Set flight mode using MAV_CMD_DO_SET_MODE
    /// param1 = base mode, param2 = custom mode (depends on autopilot)
    /// </summary>
    Task<bool> SetFlightModeAsync(string modeName);
    
    /// <summary>
    /// Get available flight modes for the current autopilot
    /// </summary>
    Task<List<string>> GetAvailableFlightModesAsync();
    
    /// <summary>
    /// Get current flight mode
    /// </summary>
    string? CurrentFlightMode { get; }
}
