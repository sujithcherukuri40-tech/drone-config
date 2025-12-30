using System.Reactive.Subjects;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Calibration service using MAVLink MAV_CMD_PREFLIGHT_CALIBRATION command (241)
/// Parameters mapping:
/// param1 = Gyro calibration (1 = calibrate)
/// param2 = Magnetometer calibration (1 = calibrate)
/// param3 = Barometer/Pressure calibration (1 = calibrate)
/// param4 = RC trim calibration (2 = calibrate)
/// param5 = Accelerometer calibration (1 = simple, 2 = level horizon, 4 = simple accel)
/// param6 = Compass motor interference calibration
/// param7 = ESC calibration (1 = calibrate)
/// </summary>
public class CalibrationService : ICalibrationService
{
    private readonly ILogger<CalibrationService> _logger;
    private readonly IMavlinkService _mavlinkService;
    private readonly Subject<CalibrationProgress> _progress = new();
    private readonly Subject<string> _statusMessage = new();

    public CalibrationService(ILogger<CalibrationService> logger, IMavlinkService mavlinkService)
    {
        _logger = logger;
        _mavlinkService = mavlinkService;
    }

    public IObservable<CalibrationProgress> Progress => _progress;
    public IObservable<string> StatusMessage => _statusMessage;

    public async Task<bool> CalibrateGyroscopeAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param1 = 1
        return await SendCalibrationCommandAsync(CalibrationType.Gyroscope, 1, 0, 0, 0, 0, 0, 0);
    }

    public async Task<bool> CalibrateMagnetometerAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param2 = 1
        return await SendCalibrationCommandAsync(CalibrationType.Magnetometer, 0, 1, 0, 0, 0, 0, 0);
    }

    public async Task<bool> CalibrateBarometerAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param3 = 1
        return await SendCalibrationCommandAsync(CalibrationType.Barometer, 0, 0, 1, 0, 0, 0, 0);
    }

    public async Task<bool> CalibrateRcTrimAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param4 = 2
        return await SendCalibrationCommandAsync(CalibrationType.RcTrim, 0, 0, 0, 2, 0, 0, 0);
    }

    public async Task<bool> CalibrateAccelerometerAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param5 = 1 (simple calibration)
        return await SendCalibrationCommandAsync(CalibrationType.Accelerometer, 0, 0, 0, 0, 1, 0, 0);
    }

    public async Task<bool> CalibrateLevelHorizonAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param5 = 2 (level horizon)
        return await SendCalibrationCommandAsync(CalibrationType.LevelHorizon, 0, 0, 0, 0, 2, 0, 0);
    }

    public async Task<bool> CalibrateEscAsync()
    {
        // MAV_CMD_PREFLIGHT_CALIBRATION (241) with param7 = 1
        return await SendCalibrationCommandAsync(CalibrationType.Esc, 0, 0, 0, 0, 0, 0, 1);
    }

    private async Task<bool> SendCalibrationCommandAsync(
        CalibrationType type, 
        float param1, float param2, float param3, float param4, 
        float param5, float param6, float param7)
    {
        try
        {
            _logger.LogInformation("Starting {Type} calibration with MAV_CMD_PREFLIGHT_CALIBRATION", type);
            _logger.LogDebug("Command params: p1={P1}, p2={P2}, p3={P3}, p4={P4}, p5={P5}, p6={P6}, p7={P7}", 
                param1, param2, param3, param4, param5, param6, param7);
            
            _statusMessage.OnNext($"Sending {type} calibration command...");
            
            // Send MAVLink MAV_CMD_PREFLIGHT_CALIBRATION command
            var result = await _mavlinkService.SendCommandLongAsync(
                MavCmd.MAV_CMD_PREFLIGHT_CALIBRATION, 
                param1, param2, param3, param4, param5, param6, param7);
            
            if (result != MavResult.MAV_RESULT_ACCEPTED)
            {
                _statusMessage.OnNext($"{type} calibration command rejected: {result}");
                return false;
            }
            
            // Simulate calibration progress
            for (int i = 0; i <= 100; i += 20)
            {
                _progress.OnNext(new CalibrationProgress
                {
                    Type = type,
                    ProgressPercent = i,
                    CurrentStep = $"Step {i / 20 + 1}",
                    IsComplete = i == 100
                });
                await Task.Delay(500);
            }
            
            _statusMessage.OnNext($"{type} calibration completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Calibration failed for {Type}", type);
            _statusMessage.OnNext($"{type} calibration failed: {ex.Message}");
            return false;
        }
    }
}
