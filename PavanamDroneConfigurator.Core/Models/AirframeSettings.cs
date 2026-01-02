namespace PavanamDroneConfigurator.Core.Models;

public class AirframeSettings
{
    public int FrameClass { get; set; } = 2; // Default: Quadcopter
    public int FrameType { get; set; } = 1;  // Default: X configuration
    public string FrameName { get; set; } = "Generic Quadcopter X";
}
