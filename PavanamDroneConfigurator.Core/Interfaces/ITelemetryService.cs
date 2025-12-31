using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface ITelemetryService
{
    TelemetryData? CurrentTelemetry { get; }
    event EventHandler<TelemetryData>? TelemetryUpdated;
    void Start();
    void Stop();
    void ProcessHeartbeat(byte baseMode, uint customMode);
    void ProcessSysStatus(ushort voltageMillivolts);
    void ProcessGpsRawInt(int latitudeE7, int longitudeE7, byte satellitesVisible);
}
