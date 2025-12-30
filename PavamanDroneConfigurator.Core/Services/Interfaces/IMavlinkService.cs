using System.IO.Ports;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Services.Interfaces;

public interface IMavlinkService
{
    // Connection
    Task<bool> ConnectSerialAsync(string port, int baudRate, int dataBits, Parity parity, StopBits stopBits);
    Task<bool> ConnectTcpAsync(string host, int port);
    Task<bool> ConnectUdpAsync(string host, int port);
    Task DisconnectAsync();
    
    // Connection State
    IObservable<LinkState> LinkState { get; }
    IObservable<double> LinkQuality { get; }
    IObservable<double> PacketRateHz { get; }
    
    // Telemetry streams
    IObservable<HeartbeatData> Heartbeat { get; }
    IObservable<AttitudeData> Attitude { get; }
    IObservable<PositionData> Position { get; }
    IObservable<SystemStatus> SystemStatus { get; }
    IObservable<RcChannels> RcChannels { get; }
    
    // Commands
    Task<MavResult> SendCommandLongAsync(MavCmd command, float param1, float param2, float param3, 
        float param4, float param5, float param6, float param7);
    
    // Parameters
    Task<float?> ReadParameterAsync(string paramName);
    Task<bool> WriteParameterAsync(string paramName, float value);
    
    bool IsConnected { get; }
}

public enum LinkState
{
    Disconnected,
    Connected,
    Downgrade
}

/// <summary>
/// MAVLink command enum - Common commands used in drone operations
/// </summary>
public enum MavCmd
{
    // Navigation commands
    MAV_CMD_NAV_WAYPOINT = 16,
    MAV_CMD_NAV_LOITER_UNLIM = 17,
    MAV_CMD_NAV_LOITER_TURNS = 18,
    MAV_CMD_NAV_LOITER_TIME = 19,
    MAV_CMD_NAV_RETURN_TO_LAUNCH = 20,
    MAV_CMD_NAV_LAND = 21,
    MAV_CMD_NAV_TAKEOFF = 22,
    
    // Condition commands
    MAV_CMD_CONDITION_DELAY = 112,
    MAV_CMD_CONDITION_CHANGE_ALT = 113,
    MAV_CMD_CONDITION_DISTANCE = 114,
    MAV_CMD_CONDITION_YAW = 115,
    
    // DO commands
    MAV_CMD_DO_SET_MODE = 176,
    MAV_CMD_DO_JUMP = 177,
    MAV_CMD_DO_CHANGE_SPEED = 178,
    MAV_CMD_DO_SET_HOME = 179,
    MAV_CMD_DO_SET_PARAMETER = 180,
    MAV_CMD_DO_SET_RELAY = 181,
    MAV_CMD_DO_REPEAT_RELAY = 182,
    MAV_CMD_DO_SET_SERVO = 183,
    MAV_CMD_DO_REPEAT_SERVO = 184,
    MAV_CMD_DO_FLIGHTTERMINATION = 185,
    MAV_CMD_DO_MOTOR_TEST = 209,
    MAV_CMD_DO_INVERTED_FLIGHT = 210,
    MAV_CMD_DO_GRIPPER = 211,
    MAV_CMD_DO_AUTOTUNE_ENABLE = 212,
    MAV_CMD_DO_SET_CAM_TRIGG_DIST = 206,
    MAV_CMD_DO_PARACHUTE = 208,
    
    // Preflight commands
    MAV_CMD_PREFLIGHT_CALIBRATION = 241,
    MAV_CMD_PREFLIGHT_SET_SENSOR_OFFSETS = 242,
    MAV_CMD_PREFLIGHT_UAVCAN = 243,
    MAV_CMD_PREFLIGHT_STORAGE = 245,
    MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN = 246,
    
    // Mission commands
    MAV_CMD_MISSION_START = 300,
    MAV_CMD_COMPONENT_ARM_DISARM = 400,
    
    // Camera commands
    MAV_CMD_DO_DIGICAM_CONFIGURE = 202,
    MAV_CMD_DO_DIGICAM_CONTROL = 203,
    
    // Mount commands
    MAV_CMD_DO_MOUNT_CONFIGURE = 204,
    MAV_CMD_DO_MOUNT_CONTROL = 205,
    
    // Gimbal commands
    MAV_CMD_DO_GIMBAL_MANAGER_PITCHYAW = 1000,
    MAV_CMD_DO_GIMBAL_MANAGER_CONFIGURE = 1001,
    
    // Other commands
    MAV_CMD_REQUEST_AUTOPILOT_CAPABILITIES = 520,
    MAV_CMD_START_RX_PAIR = 500,
    MAV_CMD_GET_HOME_POSITION = 410,
    MAV_CMD_SET_MESSAGE_INTERVAL = 511,
}

/// <summary>
/// MAVLink command result enum
/// </summary>
public enum MavResult
{
    MAV_RESULT_ACCEPTED = 0,
    MAV_RESULT_TEMPORARILY_REJECTED = 1,
    MAV_RESULT_DENIED = 2,
    MAV_RESULT_UNSUPPORTED = 3,
    MAV_RESULT_FAILED = 4,
    MAV_RESULT_IN_PROGRESS = 5,
    MAV_RESULT_CANCELLED = 6
}

public class HeartbeatData
{
    public byte SystemId { get; set; }
    public byte ComponentId { get; set; }
    public uint CustomMode { get; set; }
    public byte SystemStatus { get; set; }
}

public class AttitudeData
{
    public float Roll { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float RollSpeed { get; set; }
    public float PitchSpeed { get; set; }
    public float YawSpeed { get; set; }
}

public class PositionData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Altitude { get; set; }
    public float RelativeAltitude { get; set; }
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Vz { get; set; }
    public ushort Heading { get; set; }
}

public class SystemStatus
{
    public float Voltage { get; set; }
    public float Current { get; set; }
    public sbyte BatteryRemaining { get; set; }
}

public class RcChannels
{
    public ushort[] Channels { get; set; } = new ushort[18];
    public byte Rssi { get; set; }
}
