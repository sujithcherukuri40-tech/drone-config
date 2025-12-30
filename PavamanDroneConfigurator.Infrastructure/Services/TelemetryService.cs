using System.Reactive.Subjects;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PavamanDroneConfigurator.Infrastructure.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;
    private readonly Subject<TelemetryData> _telemetryUpdates = new();
    private TelemetryData? _currentTelemetry;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    public IObservable<TelemetryData> TelemetryUpdates => _telemetryUpdates;
    public TelemetryData? CurrentTelemetry => _currentTelemetry;
}
