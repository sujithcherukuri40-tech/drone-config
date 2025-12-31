namespace PavanamDroneConfigurator.Core.Models;

public class DroneParameter
{
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
    public string? Description { get; set; }
    public float? MinValue { get; set; }
    public float? MaxValue { get; set; }
}
