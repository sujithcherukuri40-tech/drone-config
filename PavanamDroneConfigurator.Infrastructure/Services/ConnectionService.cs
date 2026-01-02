using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ConnectionService : IConnectionService, IDisposable
{
    private readonly ILogger<ConnectionService> _logger;
    private readonly IParameterService _parameterService;
    private readonly object _sendLock = new();
    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private bool _isConnected;
    private System.Timers.Timer? _heartbeatTimer;
    private CancellationTokenSource? _serialPortWatcherCts;
    private Task? _serialPortWatcherTask;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private const int HeartbeatTimeoutMs = 5000;
    private const byte GroundControlSystemId = 255;
    private const byte GroundControlComponentId = 190;
    private const byte MavParamTypeReal32 = 9;
    private const byte DefaultTargetSystemId = 1;
    private const byte DefaultTargetComponentId = 1;
    private const int MavlinkV1MinFrameLength = 8;
    private const int MavlinkV2MinFrameHeaderLength = 12;
    private const byte CrcExtraHeartbeat = 50;
    private const byte CrcExtraParamRequestRead = 214;
    private const byte CrcExtraParamRequestList = 122;
    private const byte CrcExtraParamValue = 220;
    private const byte CrcExtraParamSet = 168;
    private const ushort X25InitialCrc = 0xFFFF;
    private const ushort X25Polynomial = 0xA001;
    private const int SerialPortWatcherIntervalMs = 1000;
    private const int MaxBufferBytes = 4096;
    private static readonly Regex ComPortRegex = new(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private TaskCompletionSource<bool>? _firstHeartbeatTcs;
    private readonly List<byte> _rxBuffer = new();
    private readonly object _bufferLock = new();
    private readonly SemaphoreSlim _disconnectLock = new(1, 1);
    private ConnectionType? _activeConnectionType;
    private string? _activeSerialPortName;
    private SerialPortInfo[] _availablePorts = Array.Empty<SerialPortInfo>();
    private byte _targetSystemId;
    private byte _targetComponentId;
    private byte _packetSequence;
    private bool _parameterDownloadStarted;
    private Task? _parameterDownloadTask;
    private readonly object _parameterDownloadLock = new();

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;

    public ConnectionService(ILogger<ConnectionService> logger, IParameterService parameterService)
    {
        _logger = logger;
        _parameterService = parameterService;
        _parameterService.ParameterListRequested += OnParameterListRequested;
        _parameterService.ParameterWriteRequested += OnParameterWriteRequested;
        _parameterService.ParameterReadRequested += OnParameterReadRequested;
        _availablePorts = EnumerateSerialPorts();
        StartSerialPortWatcher();
    }

    public void RegisterParameterService(IParameterService parameterService)
    {
        // Registration is handled in constructor - this method exists to satisfy interface contract
    }

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        try
        {
            await DisconnectAsync();
            ResetParameterTracking();
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

                byte stx = _rxBuffer[0];
                if (stx == 0xFE)
                {
                    if (_rxBuffer.Count < MavlinkV1MinFrameLength)
                    {
                        return;
                    }

                    byte payloadLen = _rxBuffer[1];
                    int frameLength = payloadLen + 8;
                    if (_rxBuffer.Count < frameLength)
                    {
                        return;
                    }

                    var frame = _rxBuffer.Take(frameLength).ToArray();
                    HandleMavlinkFrame(frame);
                    _rxBuffer.RemoveRange(0, frameLength);
                }
                else if (stx == 0xFD)
                {
                    if (_rxBuffer.Count < MavlinkV2MinFrameHeaderLength)
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

                    var frame = _rxBuffer.Take(frameLength).ToArray();
                    HandleMavlinkFrame(frame);
                    _rxBuffer.RemoveRange(0, frameLength);
                }
                else
                {
                    _rxBuffer.RemoveAt(0);
                }
            }
        }
    }

    private void ProcessParameterValue(byte[] payload)
    {
        try
        {
            if (payload.Length < 25) // PARAM_VALUE payload is 25 bytes
            {
                _logger.LogWarning("Invalid PARAM_VALUE payload length: {Length}", payload.Length);
                return;
            }

            // Parse PARAM_VALUE message
            // MAVLink 1.0 PARAM_VALUE structure:
            // - param_value: float (bytes 0-3)
            // - param_count: uint16_t (bytes 4-5)
            // - param_index: uint16_t (bytes 6-7)
            // - param_id: char[16] (bytes 8-23)
            // - param_type: uint8_t (byte 24)
            
            float value = BitConverter.ToSingle(payload, 0);
            ushort count = BitConverter.ToUInt16(payload, 4);
            ushort index = BitConverter.ToUInt16(payload, 6);
            
            // Extract parameter name (null-terminated)
            byte[] paramIdBytes = new byte[16];
            Array.Copy(payload, 8, paramIdBytes, 0, 16);
            string paramId = Encoding.ASCII.GetString(paramIdBytes).TrimEnd('\0');

            _logger.LogInformation("Received PARAM_VALUE: {Name} = {Value} (index {Index}/{Count})", 
                paramId, value, index + 1, count);

            // Forward to ParameterService
            _parameterService?.OnParameterValueReceived(paramId, value, index, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PARAM_VALUE message");
        }
    }

    private void HandleMavlinkFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
        {
            return;
        }

        var stx = frame[0];
        if (stx == 0xFE)
        {
            HandleMavlink1Frame(frame);
        }
        else if (stx == 0xFD)
        {
            HandleMavlink2Frame(frame);
        }
    }

    private void HandleMavlink1Frame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 8)
        {
            return;
        }

        byte payloadLen = frame[1];
        if (frame.Length < payloadLen + 8)
        {
            return;
        }

        byte systemId = frame[3];
        byte componentId = frame[4];
        byte messageId = frame[5];
        var payload = frame.Slice(6, payloadLen);

        switch (messageId)
        {
            case 0:
                OnHeartbeatReceived(systemId, componentId);
                break;
            case 22:
                HandleParamValuePayload(payload);
                break;
        }
    }

    private void HandleMavlink2Frame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 12)
        {
            return;
        }

        byte payloadLen = frame[1];
        byte incompatFlags = frame[2];
        bool hasSignature = (incompatFlags & 0x01) != 0;
        int signatureLength = hasSignature ? 13 : 0;

        if (frame.Length < payloadLen + 12 + signatureLength)
        {
            return;
        }

        byte systemId = frame[5];
        byte componentId = frame[6];
        int messageId = frame[7] | (frame[8] << 8) | (frame[9] << 16);
        var payload = frame.Slice(10, payloadLen);

        switch (messageId)
        {
            case 0:
                OnHeartbeatReceived(systemId, componentId);
                break;
            case 22:
                HandleParamValuePayload(payload);
                break;
        }
    }

    private void HandleParamValuePayload(ReadOnlySpan<byte> payload)
    {
        const int paramCountOffset = 4;
        const int paramIndexOffset = 6;
        const int paramIdOffset = 8;
        const int paramIdLength = 16;
        const int paramTypeOffset = paramIdOffset + paramIdLength;

        if (payload.Length < paramTypeOffset + 1)
        {
            _logger.LogWarning("Received PARAM_VALUE with insufficient payload length: {Length}", payload.Length);
            return;
        }

        float value = BinaryPrimitives.ReadSingleLittleEndian(payload);
        ushort paramCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(paramCountOffset, 2));
        ushort paramIndex = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(paramIndexOffset, 2));
        string name = Encoding.ASCII.GetString(payload.Slice(paramIdOffset, paramIdLength)).TrimEnd('\0', ' ');
        byte paramType = payload[paramTypeOffset];

        if (paramType != MavParamTypeReal32)
        {
            _logger.LogDebug("Received PARAM_VALUE {Name} with param_type {ParamType}", name, paramType);
        }

        var parameter = new DroneParameter
        {
            Name = name,
            Value = value
        };

        _parameterService.HandleParamValue(parameter, paramIndex, paramCount);
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

    private void StartParameterDownloadIfNeeded()
    {
        lock (_parameterDownloadLock)
        {
            if (_parameterDownloadStarted)
            {
                return;
            }

            _parameterDownloadStarted = true;
            _parameterDownloadTask = StartParameterDownloadAsync();
        }
    }

    private async Task StartParameterDownloadAsync()
    {
        try
        {
            await _parameterService.RefreshParametersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start parameter download");
        }
    }

    private void OnHeartbeatReceived(byte systemId, byte componentId)
    {
        // Ignore invalid or GCS-originated heartbeats
        if (systemId == 0 || componentId == GroundControlComponentId)
        {
            return;
        }

        _targetSystemId = systemId;
        _targetComponentId = componentId;
        _lastHeartbeat = DateTime.UtcNow;
        _firstHeartbeatTcs?.TrySetResult(true);

        if (!_isConnected)
        {
            _isConnected = true;
            StartHeartbeatMonitoring();
            StartParameterDownloadIfNeeded();
            ConnectionStateChanged?.Invoke(this, true);
            _logger.LogInformation("Heartbeat received from sysid {SystemId}, compid {ComponentId}. Connection established.", systemId, componentId);
            return;
        }

        StartParameterDownloadIfNeeded();
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
            _parameterService.Reset();
            ResetParameterTracking();

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

    public IEnumerable<SerialPortInfo> GetAvailableSerialPorts() => _availablePorts;

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
        var ports = EnumerateSerialPorts();
        if (!PortsEqual(_availablePorts, ports))
        {
            _availablePorts = ports;
            AvailableSerialPortsChanged?.Invoke(this, _availablePorts);
        }

        if (_serialPort != null &&
            _serialPort.IsOpen &&
            _activeSerialPortName != null &&
            !_availablePorts.Any(p => string.Equals(p.PortName, _activeSerialPortName, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Active serial port {Port} disappeared. Disconnecting.", _activeSerialPortName);
            await DisconnectAsync().ConfigureAwait(false);
        }
    }

    public Stream? GetTransportStream()
    {
        return _activeConnectionType == ConnectionType.Tcp
            ? _tcpClient?.GetStream()
            : _serialPort?.BaseStream;
    }

    private SerialPortInfo[] EnumerateSerialPorts()
    {
        if (!OperatingSystem.IsWindows())
        {
            return SerialPort.GetPortNames()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(port => new SerialPortInfo
                {
                    PortName = port,
                    FriendlyName = port,
                    InterfaceType = "Serial"
                })
                .ToArray();
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%)'");
            var ports = new List<SerialPortInfo>();
            foreach (var portObject in searcher.Get())
            {
                var name = portObject["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var portName = ExtractPortName(name);
                if (string.IsNullOrWhiteSpace(portName))
                {
                    continue;
                }

                ports.Add(new SerialPortInfo
                {
                    PortName = portName,
                    FriendlyName = ExtractFriendlyName(name, portName),
                    InterfaceType = DetectInterfaceType(name)
                });
            }

            if (ports.Count == 0)
            {
                return SerialPort.GetPortNames()
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(port => new SerialPortInfo
                    {
                        PortName = port,
                        FriendlyName = port,
                        InterfaceType = "Serial"
                    })
                    .ToArray();
            }

            return ports
                .OrderBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate serial ports via WMI. Falling back to SerialPort.GetPortNames.");
            return SerialPort.GetPortNames()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(port => new SerialPortInfo
                {
                    PortName = port,
                    FriendlyName = port,
                    InterfaceType = "Serial"
                })
                .ToArray();
        }
    }

    private static string ExtractPortName(string deviceName)
    {
        var match = ComPortRegex.Match(deviceName);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static string ExtractFriendlyName(string deviceName, string portName)
    {
        var portToken = $"({portName})";
        var friendly = deviceName.Replace(portToken, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(friendly) ? deviceName : friendly;
    }

    private static string DetectInterfaceType(string name)
    {
        if (name.Contains("SLCAN", StringComparison.OrdinalIgnoreCase))
        {
            return "SLCAN";
        }

        if (name.Contains("Cube", StringComparison.OrdinalIgnoreCase) || name.Contains("PX4", StringComparison.OrdinalIgnoreCase))
        {
            return "MAVLink";
        }

        return "Serial";
    }

    private static bool PortsEqual(IReadOnlyList<SerialPortInfo> existing, IReadOnlyList<SerialPortInfo> updated)
    {
        if (existing.Count != updated.Count)
        {
            return false;
        }

        for (int i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].PortName, updated[i].PortName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].FriendlyName, updated[i].FriendlyName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].InterfaceType, updated[i].InterfaceType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void OnParameterListRequested(object? sender, EventArgs e)
    {
        SendParamRequestList();
    }

    private void OnParameterWriteRequested(object? sender, ParameterWriteRequest request)
    {
        SendParamSet(request);
    }

    private void OnParameterReadRequested(object? sender, ParameterReadRequest request)
    {
        SendParamRequestRead(request.Index);
    }

    private void SendParamRequestList()
    {
        if (!TryGetActiveStream(out var stream))
        {
            _logger.LogWarning("Cannot send PARAM_REQUEST_LIST - no active connection");
            return;
        }

        var targetSystem = _targetSystemId == 0 ? DefaultTargetSystemId : _targetSystemId;
        var targetComponent = _targetComponentId == 0 ? DefaultTargetComponentId : _targetComponentId;
        Span<byte> payload = stackalloc byte[2];
        payload[0] = targetSystem;
        payload[1] = targetComponent;

        var frame = BuildMavlink1Frame(21, payload);
        SendFrame(stream, frame);
    }

    private void SendParamRequestRead(ushort paramIndex)
    {
        if (!TryGetActiveStream(out var stream))
        {
            _logger.LogWarning("Cannot send PARAM_REQUEST_READ - no active connection");
            return;
        }

        var targetSystem = _targetSystemId == 0 ? DefaultTargetSystemId : _targetSystemId;
        var targetComponent = _targetComponentId == 0 ? DefaultTargetComponentId : _targetComponentId;

        var payload = new byte[20];
        // param_id bytes (0-15) left empty when requesting by index
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16, 2), (short)paramIndex);
        payload[18] = targetSystem;
        payload[19] = targetComponent;

        var frame = BuildMavlink1Frame(20, payload);
        SendFrame(stream, frame);
    }

    private void SendParamSet(ParameterWriteRequest request)
    {
        if (!TryGetActiveStream(out var stream))
        {
            _logger.LogWarning("Cannot send PARAM_SET - no active connection");
            return;
        }

        var targetSystem = _targetSystemId == 0 ? DefaultTargetSystemId : _targetSystemId;
        var targetComponent = _targetComponentId == 0 ? DefaultTargetComponentId : _targetComponentId;

        var payload = new byte[23];
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(0, 4), request.Value);
        payload[4] = targetSystem;
        payload[5] = targetComponent;

        var nameBytes = Encoding.ASCII.GetBytes(request.Name);
        for (int i = 0; i < Math.Min(16, nameBytes.Length); i++)
        {
            payload[6 + i] = nameBytes[i];
        }

        payload[22] = MavParamTypeReal32; // MAV_PARAM_TYPE_REAL32

        var frame = BuildMavlink1Frame(23, payload);
        SendFrame(stream, frame);
    }

    private bool TryGetActiveStream([NotNullWhen(true)] out Stream? stream)
    {
        stream = null;
        if (_activeConnectionType == ConnectionType.Tcp && _tcpClient?.Connected == true)
        {
            stream = _tcpClient.GetStream();
        }
        else if (_activeConnectionType == ConnectionType.Serial && _serialPort?.IsOpen == true)
        {
            stream = _serialPort.BaseStream;
        }

        return stream != null;
    }

    private byte[] BuildMavlink1Frame(byte messageId, ReadOnlySpan<byte> payload)
    {
        var frame = new byte[payload.Length + 8];
        frame[0] = 0xFE;
        frame[1] = (byte)payload.Length;
        frame[2] = _packetSequence++;
        frame[3] = GroundControlSystemId;
        frame[4] = GroundControlComponentId;
        frame[5] = messageId;
        payload.CopyTo(frame.AsSpan(6));

        ushort crc = ComputeX25Crc(frame.AsSpan(1, payload.Length + 5), GetCrcExtra(messageId));
        frame[^2] = (byte)(crc & 0xFF);
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    private void SendFrame(Stream stream, ReadOnlySpan<byte> frame)
    {
        try
        {
            lock (_sendLock)
            {
                stream.Write(frame);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send MAVLink frame");
        }
    }

    private static ushort ComputeX25Crc(ReadOnlySpan<byte> buffer, byte crcExtra)
    {
        ushort crc = X25InitialCrc;
        foreach (var b in buffer)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                {
                    crc = (ushort)((crc >> 1) ^ X25Polynomial);
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        crc ^= crcExtra;
        for (int i = 0; i < 8; i++)
        {
            if ((crc & 1) != 0)
            {
                crc = (ushort)((crc >> 1) ^ X25Polynomial);
            }
            else
            {
                crc >>= 1;
            }
        }

        return crc;
    }

    private static byte GetCrcExtra(byte messageId) => messageId switch
    {
        0 => CrcExtraHeartbeat,
        20 => CrcExtraParamRequestRead,
        21 => CrcExtraParamRequestList,
        22 => CrcExtraParamValue,
        23 => CrcExtraParamSet,
        _ => 0
    };

    private void ResetParameterTracking()
    {
        _parameterDownloadStarted = false;
        _targetSystemId = 0;
        _targetComponentId = 0;
        _packetSequence = 0;
        _parameterDownloadTask = null;
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
