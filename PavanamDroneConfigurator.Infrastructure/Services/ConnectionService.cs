using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.IO.Ports;
using System.Net.Sockets;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ConnectionService : IConnectionService
{
    private readonly ILogger<ConnectionService> _logger;
    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private bool _isConnected;
    private System.Timers.Timer? _heartbeatTimer;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private const int HeartbeatTimeoutMs = 5000; // 5 seconds timeout

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        try
        {
            await DisconnectAsync();

            _logger.LogInformation("Connecting via {Type}...", settings.Type);

            // Connect based on connection type
            bool connected = settings.Type == ConnectionType.Tcp
                ? await ConnectTcpAsync(settings)
                : await ConnectSerialAsync(settings);

            if (connected)
            {
                _isConnected = true;
                _lastHeartbeat = DateTime.Now;
                StartHeartbeatMonitoring();
                ConnectionStateChanged?.Invoke(this, true);
                _logger.LogInformation("Connected successfully via {Type}", settings.Type);
            }

            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect");
            return false;
        }
    }

    private async Task<bool> ConnectTcpAsync(ConnectionSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.IpAddress))
            {
                _logger.LogError("IP Address is required for TCP connection");
                return false;
            }

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(settings.IpAddress, settings.Port);
            _logger.LogInformation("TCP connection established to {IpAddress}:{Port}", settings.IpAddress, settings.Port);
            return _tcpClient.Connected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish TCP connection");
            _tcpClient?.Dispose();
            _tcpClient = null;
            return false;
        }
    }

    private Task<bool> ConnectSerialAsync(ConnectionSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.PortName))
            {
                _logger.LogError("Port name is required for Serial connection");
                return Task.FromResult(false);
            }

            _serialPort = new SerialPort(settings.PortName, settings.BaudRate)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _serialPort.Open();
            _logger.LogInformation("Serial connection established on {PortName} at {BaudRate} baud", settings.PortName, settings.BaudRate);
            return Task.FromResult(_serialPort.IsOpen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish Serial connection");
            _serialPort?.Dispose();
            _serialPort = null;
            return Task.FromResult(false);
        }
    }

    private void StartHeartbeatMonitoring()
    {
        _heartbeatTimer = new System.Timers.Timer(1000); // Check every second
        _heartbeatTimer.Elapsed += async (s, e) =>
        {
            var timeSinceLastHeartbeat = DateTime.Now - _lastHeartbeat;
            if (timeSinceLastHeartbeat.TotalMilliseconds > HeartbeatTimeoutMs)
            {
                _logger.LogWarning("Heartbeat timeout detected. Auto-disconnecting...");
                try
                {
                    await DisconnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-disconnect");
                }
            }
            else
            {
                // TODO: In production, remove this simulation
                // Heartbeat should be updated by incoming MAVLink HEARTBEAT messages
                // For now, we simulate a healthy connection to prevent auto-disconnect during development/testing
                _lastHeartbeat = DateTime.Now;
            }
        };
        _heartbeatTimer.Start();
    }

    private void StopHeartbeatMonitoring()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
        }
    }

    public Task DisconnectAsync()
    {
        if (_isConnected)
        {
            _logger.LogInformation("Disconnecting...");

            StopHeartbeatMonitoring();

            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }

        return Task.CompletedTask;
    }
}
