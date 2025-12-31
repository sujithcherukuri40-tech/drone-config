namespace PavanamDroneConfigurator.Core.Models;

public class TelemetryData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public int SatelliteCount { get; set; }
    public double BatteryVoltage { get; set; }
    public double BatteryCurrent { get; set; }
    public int BatteryRemaining { get; set; }
    public double Roll { get; set; }
    public double Pitch { get; set; }
    public double Yaw { get; set; }
    public double GroundSpeed { get; set; }
    public bool Armed { get; set; }
    public string FlightMode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
