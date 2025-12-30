using System.IO.Ports;
using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

public class ConnectionSettings
{
    public ConnectionType ConnectionType { get; set; }
    
    // Serial settings
    public string? SerialPort { get; set; }
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    
    // TCP/UDP settings
    public string? Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5760;
}
