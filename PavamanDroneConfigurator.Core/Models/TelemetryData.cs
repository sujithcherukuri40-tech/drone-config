namespace PavamanDroneConfigurator.Core.Models;

public class TelemetryData
{
    // Heartbeat
    public byte SystemId { get; set; }
    public byte ComponentId { get; set; }
    public uint CustomMode { get; set; }
    public byte SystemStatus { get; set; }
    
    // Attitude
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float RollSpeed { get; set; }
    public float PitchSpeed { get; set; }
    public float YawSpeed { get; set; }
    
    // Position
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Altitude { get; set; }
    public float RelativeAltitude { get; set; }
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Vz { get; set; }
    public ushort Heading { get; set; }
    
    // System status
    public float Voltage { get; set; }
    public float Current { get; set; }
    public sbyte BatteryRemaining { get; set; }
    
    // RC Channels
    public ushort[] RcChannels { get; set; } = new ushort[18];
    public byte Rssi { get; set; }
    
    // Connection
    public double LinkQuality { get; set; }
    public double PacketRateHz { get; set; }
    public DateTime LastUpdate { get; set; }
}
