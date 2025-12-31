using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ConnectionService : IConnectionService, IDisposable
{
    private readonly ILogger<ConnectionService> _logger;
    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private bool _isConnected;
    private System.Timers.Timer? _heartbeatTimer;
    private CancellationTokenSource? _serialPortWatcherCts;
    private Task? _serialPortWatcherTask;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private const int HeartbeatTimeoutMs = 5000;
    private const byte GroundControlComponentId = 190;
    private const int SerialPortWatcherIntervalMs = 1000;
    private const int MaxBufferBytes = 4096;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private TaskCompletionSource<bool>? _firstHeartbeatTcs;
    private readonly List<byte> _rxBuffer = new();
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _disconnectLock = new(1, 1);
    private ConnectionType? _activeConnectionType;
    private string? _activeSerialPortName;
    private string[] _availablePorts = Array.Empty<string>();

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<IEnumerable<string>>? AvailableSerialPortsChanged;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;
        _availablePorts = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        StartSerialPortWatcher();
    }

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        try
        {
            await DisconnectAsync();
            _logger.LogInformation("Connecting via {Type}...", settings.Type);

            bool transportOpened = settings.Type == ConnectionType.Tcp
                ? await ConnectTcpAsync(settings)
                : await ConnectSerialAsync(settings);

            if (!transportOpened)
            {
                return false;
            }

            _activeConnectionType = settings.Type;
            _firstHeartbeatTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            StartReceiveLoop(settings.Type);

            var completed = await Task.WhenAny(_firstHeartbeatTcs.Task, Task.Delay(HeartbeatTimeoutMs));
            var heartbeatReceived = completed == _firstHeartbeatTcs.Task && _firstHeartbeatTcs.Task.Result;
            if (!heartbeatReceived)
            {
                _logger.LogWarning("No heartbeat received within timeout. Disconnecting.");
                await DisconnectAsync();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect");
            await DisconnectAsync();
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
            _activeSerialPortName = settings.PortName;
            _logger.LogInformation("Serial connection established on {PortName} at {BaudRate} baud", settings.PortName, settings.BaudRate);
            return Task.FromResult(_serialPort.IsOpen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish Serial connection");
            _serialPort?.Dispose();
            _serialPort = null;
            _activeSerialPortName = null;
            return Task.FromResult(false);
        }
    }

    private void StartReceiveLoop(ConnectionType type)
    {
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(type, _receiveCts.Token));
    }

    private async Task ReceiveLoopAsync(ConnectionType type, CancellationToken token)
    {
        try
        {
            Stream? stream = type == ConnectionType.Tcp
                ? _tcpClient?.GetStream()
                : _serialPort?.BaseStream;

            if (stream == null)
            {
                _logger.LogWarning("No transport stream available for receiving data.");
                await DisconnectInternalAsync(true);
                return;
            }

            var buffer = new byte[512];
            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read == 0)
                {
                    _logger.LogWarning("Transport closed by remote endpoint.");
                    await DisconnectInternalAsync(true);
                    return;
                }

                ProcessIncomingData(buffer.AsSpan(0, read));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disconnect
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Transport I/O failure detected");
            await DisconnectInternalAsync(true);
        }
        catch (ObjectDisposedException)
        {
            // Expected during teardown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure in receive loop");
            await DisconnectInternalAsync(true);
        }
    }

    private void ProcessIncomingData(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        lock (_bufferLock)
        {
            AppendDataInternal(data);

            while (true)
            {
                int startIndex = FindStartIndex();
                if (startIndex < 0)
                {
                    if (_rxBuffer.Count > MaxBufferBytes)
                    {
                        _rxBuffer.Clear();
                    }
                    return;
                }

                if (startIndex > 0)
                {
                    _rxBuffer.RemoveRange(0, startIndex);
                }

                if (_rxBuffer.Count < 8)
                {
                    return;
                }

                byte stx = _rxBuffer[0];
                if (stx == 0xFE)
                {
                    byte payloadLen = _rxBuffer[1];
                    int frameLength = payloadLen + 8;
                    if (_rxBuffer.Count < frameLength)
                    {
                        return;
                    }

                    byte systemId = _rxBuffer[3];
                    byte componentId = _rxBuffer[4];
                    byte messageId = _rxBuffer[5];

                    if (messageId == 0)
                    {
                        OnHeartbeatReceived(systemId, componentId);
                    }

                    _rxBuffer.RemoveRange(0, frameLength);
                }
                else if (stx == 0xFD)
                {
                    if (_rxBuffer.Count < 12)
                    {
                        return;
                    }

                    byte payloadLen = _rxBuffer[1];
                    byte incompatFlags = _rxBuffer[2];
                    bool hasSignature = (incompatFlags & 0x01) != 0;
                    int signatureLength = hasSignature ? 13 : 0;
                    int frameLength = payloadLen + 12 + signatureLength;

                    if (_rxBuffer.Count < frameLength)
                    {
                        return;
                    }

                    byte systemId = _rxBuffer[5];
                    byte componentId = _rxBuffer[6];
                    int messageId = _rxBuffer[7] | (_rxBuffer[8] << 8) | (_rxBuffer[9] << 16);

                    if (messageId == 0)
                    {
                        OnHeartbeatReceived(systemId, componentId);
                    }

                    _rxBuffer.RemoveRange(0, frameLength);
                }
                else
                {
                    _rxBuffer.RemoveAt(0);
                }
            }
        }
    }

    private int FindStartIndex()
    {
        for (int i = 0; i < _rxBuffer.Count; i++)
        {
            if (_rxBuffer[i] == 0xFE || _rxBuffer[i] == 0xFD)
            {
                return i;
            }
        }

        return -1;
    }

    private void AppendDataInternal(ReadOnlySpan<byte> data)
    {
        _rxBuffer.EnsureCapacity(_rxBuffer.Count + data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            _rxBuffer.Add(data[i]);
        }
    }

    private void OnHeartbeatReceived(byte systemId, byte componentId)
    {
        // Ignore invalid or GCS-originated heartbeats (Day-2 requirement: systemId > 0 and componentId must not be the GCS)
        if (systemId == 0 || componentId == GroundControlComponentId)
        {
            return;
        }

        _lastHeartbeat = DateTime.UtcNow;
        _firstHeartbeatTcs?.TrySetResult(true);

        if (_isConnected)
        {
            return;
        }

        _isConnected = true;
        StartHeartbeatMonitoring();
        ConnectionStateChanged?.Invoke(this, true);
        _logger.LogInformation("Heartbeat received from sysid {SystemId}, compid {ComponentId}. Connection established.", systemId, componentId);
    }

    private void StartHeartbeatMonitoring()
    {
        StopHeartbeatMonitoring();

        _heartbeatTimer = new System.Timers.Timer(1000);
        _heartbeatTimer.Elapsed += async (s, e) =>
        {
            if (_lastHeartbeat == DateTime.MinValue)
            {
                return;
            }

            var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeat;
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

    public Task DisconnectAsync() => DisconnectInternalAsync(false);

    private async Task DisconnectInternalAsync(bool calledFromReceiveLoop)
    {
        await _disconnectLock.WaitAsync();
        try
        {
            _firstHeartbeatTcs?.TrySetResult(false);

            _receiveCts?.Cancel();

            if (!calledFromReceiveLoop && _receiveTask != null)
            {
                try
                {
                    await _receiveTask;
                }
                catch (OperationCanceledException)
                {
                    // expected during shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for receive loop to stop");
                }
            }

            StopHeartbeatMonitoring();

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing serial port");
                }

                _serialPort.Dispose();
                _serialPort = null;
            }

            if (_tcpClient != null)
            {
                try
                {
                    _tcpClient.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing TCP client");
                }

                _tcpClient.Dispose();
                _tcpClient = null;
            }

            _receiveCts?.Dispose();
            _receiveCts = null;
            _receiveTask = null;
            _activeConnectionType = null;
            _activeSerialPortName = null;
            _rxBuffer.Clear();

            bool wasConnected = _isConnected;
            _isConnected = false;
            _lastHeartbeat = DateTime.MinValue;

            if (wasConnected)
            {
                ConnectionStateChanged?.Invoke(this, false);
            }
        }
        finally
        {
            _disconnectLock.Release();
        }
    }

    public IEnumerable<string> GetAvailableSerialPorts() => _availablePorts;

    private void StartSerialPortWatcher()
    {
        _serialPortWatcherCts = new CancellationTokenSource();
        _serialPortWatcherTask = Task.Run(() => SerialPortWatcherLoopAsync(_serialPortWatcherCts.Token));
    }

    private void StopSerialPortWatcher()
    {
        if (_serialPortWatcherCts != null)
        {
            _serialPortWatcherCts.Cancel();
            _serialPortWatcherCts.Dispose();
            _serialPortWatcherCts = null;
            _serialPortWatcherTask = null;
        }
    }

    private async Task UpdateSerialPortsSafeAsync()
    {
        try
        {
            await UpdateSerialPortsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh serial port list");
        }
    }

    private async Task SerialPortWatcherLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UpdateSerialPortsSafeAsync().ConfigureAwait(false);
            try
            {
                await Task.Delay(SerialPortWatcherIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                // expected on shutdown
            }
        }
    }

    private async Task UpdateSerialPortsAsync()
    {
        var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        if (!_availablePorts.SequenceEqual(ports))
        {
            _availablePorts = ports;
            AvailableSerialPortsChanged?.Invoke(this, _availablePorts);
        }

        if (_serialPort != null && _serialPort.IsOpen && _activeSerialPortName != null && !_availablePorts.Contains(_activeSerialPortName))
        {
            _logger.LogWarning("Active serial port {Port} disappeared. Disconnecting.", _activeSerialPortName);
            await DisconnectAsync().ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        StopHeartbeatMonitoring();
        StopSerialPortWatcher();
        _receiveCts?.Cancel();
        _serialPort?.Dispose();
        _tcpClient?.Dispose();
        _receiveCts?.Dispose();
    }
}
