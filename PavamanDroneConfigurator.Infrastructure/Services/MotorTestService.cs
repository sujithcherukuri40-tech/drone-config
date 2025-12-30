using PavamanDroneConfigurator.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Motor test service using MAV_CMD_DO_MOTOR_TEST command (209)
/// Parameters:
/// param1 = motor instance number (1-8, 0 for all)
/// param2 = throttle type (0=PWM, 1=percentage, 2=passthrough)
/// param3 = throttle value
/// param4 = timeout in seconds
/// param5 = motor count (number of motors)
/// param6 = test order (0=default sequence, 1=reverse sequence)
/// </summary>
public class MotorTestService : IMotorTestService
{
    private readonly ILogger<MotorTestService> _logger;
    private readonly IMavlinkService _mavlinkService;

    public MotorTestService(ILogger<MotorTestService> logger, IMavlinkService mavlinkService)
    {
        _logger = logger;
        _mavlinkService = mavlinkService;
    }

    public bool RequireArmingConfirmation => true;
    public bool RequirePropellerRemovalConfirmation => true;

    /// <summary>
    /// Test individual motor using MAV_CMD_DO_MOTOR_TEST
    /// </summary>
    public async Task<bool> TestMotorAsync(int motorNumber, int throttlePercent, int durationSeconds)
    {
        try
        {
            _logger.LogInformation("Testing motor {Motor} at {Throttle}% for {Duration}s", 
                motorNumber, throttlePercent, durationSeconds);

            // MAV_CMD_DO_MOTOR_TEST (209)
            // param1 = motor instance (1-8)
            // param2 = throttle type (1 = percentage)
            // param3 = throttle value (0-100)
            // param4 = timeout in seconds
            // param5 = motor count
            // param6 = test order
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_DO_MOTOR_TEST,
                motorNumber,        // Motor instance
                1,                  // Throttle type: percentage
                throttlePercent,    // Throttle value
                durationSeconds,    // Timeout
                0,                  // Motor count (0 = use all available)
                0,                  // Test order (0 = default)
                0);

            if (result == MavResult.MAV_RESULT_ACCEPTED)
            {
                _logger.LogInformation("Motor {Motor} test started successfully", motorNumber);
                return true;
            }
            else
            {
                _logger.LogWarning("Motor test command rejected: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test motor {Motor}", motorNumber);
            return false;
        }
    }

    /// <summary>
    /// Test all motors in sequence
    /// </summary>
    public async Task<bool> TestAllMotorsSequenceAsync(int throttlePercent, int durationSeconds)
    {
        _logger.LogInformation("Testing all motors sequentially at {Throttle}%", throttlePercent);

        // Test motors 1-4 (typical quadcopter configuration)
        for (int motor = 1; motor <= 4; motor++)
        {
            var success = await TestMotorAsync(motor, throttlePercent, durationSeconds);
            if (!success)
            {
                _logger.LogWarning("Motor {Motor} test failed, stopping sequence", motor);
                return false;
            }
            
            // Wait for motor test to complete before testing next motor
            await Task.Delay((durationSeconds + 1) * 1000);
        }

        _logger.LogInformation("All motors tested successfully");
        return true;
    }

    /// <summary>
    /// Stop all motors immediately by sending 0% throttle command
    /// </summary>
    public async Task StopAllMotorsAsync()
    {
        _logger.LogInformation("Stopping all motors");

        // Send motor test command with 0% throttle and 0 second duration
        await _mavlinkService.SendCommandLongAsync(
            MavCmd.MAV_CMD_DO_MOTOR_TEST,
            0,  // All motors
            1,  // Percentage
            0,  // 0% throttle
            0,  // 0 second duration
            0,
            0,
            0);
    }
}
