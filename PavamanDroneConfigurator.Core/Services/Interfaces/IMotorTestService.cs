namespace PavamanDroneConfigurator.Core.Services.Interfaces;

/// <summary>
/// Service for motor testing operations using MAVLink commands
/// Uses MAV_CMD_DO_MOTOR_TEST command (209)
/// </summary>
public interface IMotorTestService
{
    /// <summary>
    /// Test individual motor using MAV_CMD_DO_MOTOR_TEST
    /// param1 = motor number (1-8)
    /// param2 = throttle type (0=PWM, 1=percentage, 2=throttle passthrough)
    /// param3 = throttle value
    /// param4 = timeout in seconds
    /// param5 = motor count
    /// param6 = test order (0=default, 1=reverse)
    /// </summary>
    Task<bool> TestMotorAsync(int motorNumber, int throttlePercent, int durationSeconds);
    
    /// <summary>
    /// Test all motors in sequence
    /// </summary>
    Task<bool> TestAllMotorsSequenceAsync(int throttlePercent, int durationSeconds);
    
    /// <summary>
    /// Stop all motors immediately
    /// </summary>
    Task StopAllMotorsAsync();
    
    // Safety confirmations
    bool RequireArmingConfirmation { get; }
    bool RequirePropellerRemovalConfirmation { get; }
}
