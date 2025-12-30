using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Services.Events;

public class TelemetryEventArgs : EventArgs
{
    public TelemetryData Data { get; set; }
    
    public TelemetryEventArgs(TelemetryData data)
    {
        Data = data;
    }
}
