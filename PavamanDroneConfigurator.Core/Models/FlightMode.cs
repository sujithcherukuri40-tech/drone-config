namespace PavamanDroneConfigurator.Core.Models;

public class FlightMode
{
    public int Slot { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ModeNumber { get; set; }
    public bool SimpleMode { get; set; }
    public bool SuperSimpleMode { get; set; }
}
