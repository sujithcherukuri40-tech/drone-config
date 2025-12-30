using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Services.Interfaces;

public interface ITelemetryService
{
    IObservable<TelemetryData> TelemetryUpdates { get; }
    TelemetryData? CurrentTelemetry { get; }
}
