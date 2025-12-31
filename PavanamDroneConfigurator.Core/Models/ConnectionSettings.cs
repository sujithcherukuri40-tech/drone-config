using PavanamDroneConfigurator.Core.Enums;

namespace PavanamDroneConfigurator.Core.Models;

public class ConnectionSettings
{
    public ConnectionType Type { get; set; }
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 115200;
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 5760;
}
