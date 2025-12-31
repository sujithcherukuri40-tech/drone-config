using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface ITelemetryService
{
    TelemetryData? CurrentTelemetry { get; }
    event EventHandler<TelemetryData>? TelemetryUpdated;
}
