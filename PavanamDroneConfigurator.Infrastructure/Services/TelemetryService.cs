using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly TelemetryData _currentTelemetry = new();
    private System.Timers.Timer? _updateTimer;
    private bool _isRunning;

    public TelemetryData? CurrentTelemetry => _currentTelemetry;

    public event EventHandler<TelemetryData>? TelemetryUpdated;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _logger.LogInformation("Starting telemetry service");
        _isRunning = true;
        StartSimulatedTelemetry();
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _logger.LogInformation("Stopping telemetry service");
        _isRunning = false;
        
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Dispose();
            _updateTimer = null;
        }
    }

    private void StartSimulatedTelemetry()
    {
        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += (s, e) =>
        {
            if (!_isRunning)
                return;

            // Simulate telemetry updates
            _currentTelemetry.Timestamp = DateTime.Now;
            _currentTelemetry.BatteryVoltage = 12.4 + Random.Shared.NextDouble() * 0.2;
            _currentTelemetry.BatteryRemaining = 75;
            _currentTelemetry.SatelliteCount = 12;
            _currentTelemetry.FlightMode = "Stabilize";

            TelemetryUpdated?.Invoke(this, _currentTelemetry);
        };
        _updateTimer.Start();
    }
}
