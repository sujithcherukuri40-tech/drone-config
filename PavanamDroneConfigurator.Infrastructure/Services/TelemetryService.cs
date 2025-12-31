using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly TelemetryData _currentTelemetry = new();
    private bool _isRunning;
    private readonly object _sync = new();

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
    }

    public void Stop()
    {
        if (!_isRunning)
            return;

        _logger.LogInformation("Stopping telemetry service");
        _isRunning = false;
    }

    public void ProcessHeartbeat(byte baseMode, uint customMode)
    {
        if (!_isRunning)
            return;

        lock (_sync)
        {
            _currentTelemetry.Armed = (baseMode & 0x80) != 0;
            _currentTelemetry.FlightMode = customMode.ToString();
            _currentTelemetry.Timestamp = DateTime.UtcNow;
            TelemetryUpdated?.Invoke(this, _currentTelemetry);
        }
    }

    public void ProcessSysStatus(ushort voltageMillivolts)
    {
        if (!_isRunning)
            return;

        lock (_sync)
        {
            _currentTelemetry.BatteryVoltage = voltageMillivolts / 1000.0;
            _currentTelemetry.Timestamp = DateTime.UtcNow;
            TelemetryUpdated?.Invoke(this, _currentTelemetry);
        }
    }

    public void ProcessGpsRawInt(int latitudeE7, int longitudeE7, byte satellitesVisible)
    {
        if (!_isRunning)
            return;

        lock (_sync)
        {
            _currentTelemetry.Latitude = latitudeE7 / 1e7;
            _currentTelemetry.Longitude = longitudeE7 / 1e7;
            _currentTelemetry.SatelliteCount = satellitesVisible;
            _currentTelemetry.Timestamp = DateTime.UtcNow;
            TelemetryUpdated?.Invoke(this, _currentTelemetry);
        }
    }
}
