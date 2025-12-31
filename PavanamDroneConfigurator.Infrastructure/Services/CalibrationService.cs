using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class CalibrationService : ICalibrationService
{
    private readonly ILogger<CalibrationService> _logger;
    private CalibrationStateModel _currentState = new();

    public CalibrationStateModel? CurrentState => _currentState;

    public event EventHandler<CalibrationStateModel>? CalibrationStateChanged;

    public CalibrationService(ILogger<CalibrationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StartCalibrationAsync(CalibrationType type)
    {
        _logger.LogInformation("Starting {Type} calibration", type);

        _currentState = new CalibrationStateModel
        {
            Type = type,
            State = CalibrationState.InProgress,
            Progress = 0,
            Message = $"Starting {type} calibration..."
        };
        CalibrationStateChanged?.Invoke(this, _currentState);

        // Simulate calibration process
        for (int i = 0; i <= 100; i += 20)
        {
            await Task.Delay(500);
            _currentState.Progress = i;
            _currentState.Message = $"{type} calibration: {i}%";
            CalibrationStateChanged?.Invoke(this, _currentState);
        }

        _currentState.State = CalibrationState.Completed;
        _currentState.Message = $"{type} calibration completed";
        CalibrationStateChanged?.Invoke(this, _currentState);

        return true;
    }

    public Task<bool> CancelCalibrationAsync()
    {
        _logger.LogInformation("Cancelling calibration");
        _currentState.State = CalibrationState.Failed;
        _currentState.Message = "Calibration cancelled";
        CalibrationStateChanged?.Invoke(this, _currentState);
        return Task.FromResult(true);
    }
}
