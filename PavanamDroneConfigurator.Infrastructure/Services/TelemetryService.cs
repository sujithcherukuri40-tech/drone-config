using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly TelemetryData _currentTelemetry = new();
    private System.Timers.Timer? _updateTimer;

    public TelemetryData? CurrentTelemetry => _currentTelemetry;

    public event EventHandler<TelemetryData>? TelemetryUpdated;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
        StartSimulatedTelemetry();
    }

    private void StartSimulatedTelemetry()
    {
        _updateTimer = new System.Timers.Timer(1000);
        _updateTimer.Elapsed += (s, e) =>
        {
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
