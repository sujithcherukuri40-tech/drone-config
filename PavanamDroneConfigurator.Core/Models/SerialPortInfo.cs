namespace PavanamDroneConfigurator.Core.Models;

public class SerialPortInfo
{
    public string PortName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = "Serial";
    public string DisplayName => $"{PortName}  {FriendlyName}  {InterfaceType}";
}
