using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Concurrent;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly IConnectionService _connectionService;
    private readonly ConcurrentDictionary<string, DroneParameter> _parameters = new();
    private readonly SemaphoreSlim _parameterLock = new(1, 1);
    private const byte GcsSystemId = 255;
    private const byte GcsComponentId = 190;
    private const byte TargetSystemId = 1;
    private const byte TargetComponentId = 1;
    private const int ParameterTimeoutMs = 5000;
    private const int EepromWriteTimeoutMs = 5000; // Timeout for initial PARAM_SET with EEPROM write
    private const int VerificationTimeoutMs = 3000; // Timeout for verification reads
    private const int EepromWriteDelayMs = 200; // Delay to allow EEPROM write to complete
    private byte _sequenceNumber = 0;

    public event EventHandler<DroneParameter>? ParameterUpdated;

    public ParameterService(ILogger<ParameterService> logger, IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
        
        // Register this service with ConnectionService to receive parameter messages
        _connectionService.RegisterParameterService(this);
    }

    public async Task<List<DroneParameter>> GetAllParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot get parameters: not connected");
            return new List<DroneParameter>();
        }

        await _parameterLock.WaitAsync();
        try
        {
            // Request all parameters from the drone
            await RequestParameterListAsync();
            
            // Wait a bit for parameters to be received
            await Task.Delay(3000);
            
            return _parameters.Values.OrderBy(p => p.Name).ToList();
        }
        finally
        {
            _parameterLock.Release();
        }
    }

    public async Task<DroneParameter?> GetParameterAsync(string name)
    {
        return await GetParameterAsync(name, forceRefresh: false);
    }

    public async Task<DroneParameter?> GetParameterAsync(string name, bool forceRefresh)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot get parameter: not connected");
            return null;
        }

        // If forcing refresh, remove from cache first
        if (forceRefresh)
        {
            _parameters.TryRemove(name, out _);
            _logger.LogDebug("Forcing refresh for parameter: {Name}", name);
        }

        // Check if we already have it cached
        if (_parameters.TryGetValue(name, out var cachedParam))
        {
            _logger.LogDebug("Returning cached parameter: {Name} = {Value}", name, cachedParam.Value);
            return cachedParam;
        }

        // Request specific parameter from drone
        _logger.LogDebug("Parameter {Name} not in cache, requesting from drone", name);
        await RequestParameterReadAsync(name);
        
        // Wait for response with timeout
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < ParameterTimeoutMs)
        {
            if (_parameters.TryGetValue(name, out var param))
            {
                _logger.LogDebug("Received parameter from drone: {Name} = {Value}", name, param.Value);
                return param;
            }
            await Task.Delay(50);
        }

        _logger.LogWarning("Parameter {Name} not found or timeout", name);
        return null;
    }

    public async Task<bool> SetParameterAsync(string name, float value)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot set parameter: not connected");
            return false;
        }

        try
        {
            _logger.LogInformation("Setting parameter {Name} to {Value}", name, value);
            
            // Get the current value before sending
            _parameters.TryGetValue(name, out var beforeParam);
            float beforeValue = beforeParam?.Value ?? float.NaN;
            _logger.LogInformation("Current cached value for {Name}: {Before}", name, beforeValue);
            
            // Send the PARAM_SET message
            await SendParameterSetAsync(name, value);
            
            // Wait for the parameter to be updated (indicated by receiving PARAM_VALUE)
            bool receivedUpdate = await VerifyParameterValueAsync(name, value, EepromWriteTimeoutMs, 
                "Successfully set parameter", false);
            
            if (!receivedUpdate)
            {
                _logger.LogWarning("⚠️ Timeout waiting for parameter {Name} to be set to {Value}. " +
                    "Attempting verification by re-reading parameter...", name, value);
                
                // Force a fresh read from the drone to verify the parameter was actually written
                _parameters.TryRemove(name, out _);
                await RequestParameterReadAsync(name);
                
                bool verified = await VerifyParameterValueAsync(name, value, VerificationTimeoutMs,
                    "Parameter verified by re-reading from drone", true);
                
                if (!verified)
                {
                    _logger.LogError("❌ Failed to verify parameter {Name} = {Value} after re-reading", name, value);
                    return false;
                }
            }
            
            // Final verification: Re-read the parameter one more time to ensure it was persisted
            _logger.LogDebug("Performing final verification of parameter {Name}", name);
            _parameters.TryRemove(name, out _);
            await Task.Delay(EepromWriteDelayMs); // Allow EEPROM write to complete
            
            await RequestParameterReadAsync(name);
            
            bool finalVerified = await VerifyParameterValueAsync(name, value, VerificationTimeoutMs,
                "Parameter CONFIRMED persisted on drone", true);
            
            return finalVerified;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting parameter {Name}", name);
            return false;
        }
    }

    private async Task<bool> VerifyParameterValueAsync(string name, float expectedValue, int timeoutMs, 
        string successMessage, bool logError)
    {
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        
        while ((DateTime.UtcNow - startTime) < timeout)
        {
            await Task.Delay(100);
            
            if (_parameters.TryGetValue(name, out var param))
            {
                _logger.LogDebug("Checking parameter {Name}: current={CurrentValue}, target={TargetValue}", 
                    name, param.Value, expectedValue);
                
                // Allow small floating point differences
                if (Math.Abs(param.Value - expectedValue) < 0.001f)
                {
                    _logger.LogInformation("✓ {Message}: {Name} = {Value}", successMessage, name, expectedValue);
                    return true;
                }
                else if (logError)
                {
                    _logger.LogError("❌ Parameter {Name} verification failed: expected {Expected}, got {Actual}", 
                        name, expectedValue, param.Value);
                    return false;
                }
            }
        }
        
        return false;
    }

    public async Task RefreshParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot refresh parameters: not connected");
            return;
        }

        _parameters.Clear();
        await RequestParameterListAsync();
        await Task.Delay(3000); // Wait for parameters to be received
    }

    private async Task RequestParameterListAsync()
    {
        try
        {
            var stream = _connectionService.GetTransportStream();
            if (stream == null)
            {
                _logger.LogWarning("Transport stream not available");
                return;
            }

            // Build PARAM_REQUEST_LIST message (MAVLink message ID 21)
            // Using MAVLink 1.0 format for simplicity
            byte stx = 0xFE;
            byte payloadLen = 2;
            byte packetSeq = _sequenceNumber++;
            byte sysId = GcsSystemId;
            byte compId = GcsComponentId;
            byte msgId = 21; // PARAM_REQUEST_LIST

            // Payload: target_system (1 byte), target_component (1 byte)
            var packet = new List<byte>
            {
                stx,
                payloadLen,
                packetSeq,
                sysId,
                compId,
                msgId,
                TargetSystemId,
                TargetComponentId
            };

            // Calculate CRC
            ushort crc = CalculateCrc(packet.Skip(1).ToArray(), msgId);
            packet.Add((byte)(crc & 0xFF));
            packet.Add((byte)((crc >> 8) & 0xFF));

            await stream.WriteAsync(packet.ToArray());
            _logger.LogInformation("Sent PARAM_REQUEST_LIST to system {Sys}/{Comp}", TargetSystemId, TargetComponentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting parameter list");
        }
    }

    private async Task RequestParameterReadAsync(string paramName)
    {
        try
        {
            var stream = _connectionService.GetTransportStream();
            if (stream == null)
            {
                _logger.LogWarning("Transport stream not available");
                return;
            }

            // Build PARAM_REQUEST_READ message (MAVLink message ID 20)
            // CORRECT payload structure for MAVLink 1.0 PARAM_REQUEST_READ (#20):
            // - target_system: uint8_t (byte 0)
            // - target_component: uint8_t (byte 1)
            // - param_id: char[16] (bytes 2-17)
            // - param_index: int16_t (bytes 18-19)
            //
            // Reference: https://mavlink.io/en/messages/common.html#PARAM_REQUEST_READ
            
            byte stx = 0xFE;
            byte payloadLen = 20;
            byte packetSeq = _sequenceNumber++;
            byte sysId = GcsSystemId;
            byte compId = GcsComponentId;
            byte msgId = 20; // PARAM_REQUEST_READ

            // Encode param name (16 bytes, null-padded)
            byte[] paramIdBytes = new byte[16];
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(paramName);
            Array.Copy(nameBytes, paramIdBytes, Math.Min(nameBytes.Length, 16));
            
            short paramIndex = -1; // -1 means use param_id instead of index

            var packet = new List<byte>
            {
                stx,
                payloadLen,
                packetSeq,
                sysId,
                compId,
                msgId
            };
            
            // Add payload in CORRECT order (target_system, target_component, param_id, param_index)
            packet.Add(TargetSystemId);         // target_system (byte 0)
            packet.Add(TargetComponentId);      // target_component (byte 1)
            packet.AddRange(paramIdBytes);      // param_id (bytes 2-17)
            packet.Add((byte)(paramIndex & 0xFF));              // param_index low byte (byte 18)
            packet.Add((byte)((paramIndex >> 8) & 0xFF));       // param_index high byte (byte 19)

            // Calculate CRC
            ushort crc = CalculateCrc(packet.Skip(1).ToArray(), msgId);
            packet.Add((byte)(crc & 0xFF));
            packet.Add((byte)((crc >> 8) & 0xFF));

            await stream.WriteAsync(packet.ToArray());
            _logger.LogDebug("Sent PARAM_REQUEST_READ for {ParamName}", paramName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting parameter {ParamName}", paramName);
        }
    }

    private async Task SendParameterSetAsync(string paramName, float value)
    {
        try
        {
            var stream = _connectionService.GetTransportStream();
            if (stream == null)
            {
                _logger.LogWarning("Transport stream not available");
                return;
            }

            // Build PARAM_SET message (MAVLink message ID 23)
            // CORRECT payload structure for MAVLink 1.0 PARAM_SET (#23):
            // - target_system: uint8_t (byte 0)
            // - target_component: uint8_t (byte 1)
            // - param_id: char[16] (bytes 2-17)
            // - param_value: float (bytes 18-21)
            // - param_type: uint8_t (byte 22)
            //
            // Reference: https://mavlink.io/en/messages/common.html#PARAM_SET
            
            byte stx = 0xFE;
            byte payloadLen = 23;
            byte packetSeq = _sequenceNumber++;
            byte sysId = GcsSystemId;
            byte compId = GcsComponentId;
            byte msgId = 23; // PARAM_SET

            // Encode param name (16 bytes, null-padded)
            byte[] paramIdBytes = new byte[16];
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(paramName);
            Array.Copy(nameBytes, paramIdBytes, Math.Min(nameBytes.Length, 16));
            
            // Convert float to bytes (little-endian)
            byte[] valueBytes = BitConverter.GetBytes(value);
            
            byte paramType = 9; // MAV_PARAM_TYPE_REAL32

            var packet = new List<byte>
            {
                stx,
                payloadLen,
                packetSeq,
                sysId,
                compId,
                msgId
            };
            
            // Add payload in CORRECT order (target_system, target_component, param_id, param_value, param_type)
            packet.Add(TargetSystemId);         // target_system (byte 0)
            packet.Add(TargetComponentId);      // target_component (byte 1)
            packet.AddRange(paramIdBytes);      // param_id (bytes 2-17)
            packet.AddRange(valueBytes);        // param_value (bytes 18-21)
            packet.Add(paramType);              // param_type (byte 22)

            // Calculate CRC
            ushort crc = CalculateCrc(packet.Skip(1).ToArray(), msgId);
            packet.Add((byte)(crc & 0xFF));
            packet.Add((byte)((crc >> 8) & 0xFF));

            // Convert packet to array for sending
            byte[] packetArray = packet.ToArray();
            
            // Log detailed packet information
            _logger.LogInformation("???????????????????????????????????????????????????????");
            _logger.LogInformation("PARAM_SET Message for: {ParamName} = {Value}", paramName, value);
            _logger.LogInformation("???????????????????????????????????????????????????????");
            _logger.LogInformation("Header: STX=0x{Stx:X2}, Len={Len}, Seq={Seq}, SysID={Sys}, CompID={Comp}, MsgID={Msg}",
                stx, payloadLen, packetSeq, sysId, compId, msgId);
            _logger.LogInformation("Payload: TargetSys={TargetSys}, TargetComp={TargetComp}, Type={Type}",
                TargetSystemId, TargetComponentId, paramType);
            _logger.LogInformation("Value bytes (LE): {ValueHex}", BitConverter.ToString(valueBytes));
            _logger.LogInformation("Param ID: '{ParamId}'", paramName);
            _logger.LogInformation("Full Packet ({Length} bytes): {Hex}", 
                packetArray.Length, 
                BitConverter.ToString(packetArray).Replace("-", " "));
            _logger.LogInformation("???????????????????????????????????????????????????????");

            await stream.WriteAsync(packetArray);
            await stream.FlushAsync(); // Ensure data is sent immediately
            
            _logger.LogInformation("? Sent PARAM_SET for {ParamName} = {Value}", paramName, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting parameter {ParamName}", paramName);
            throw;
        }
    }

    private ushort CalculateCrc(byte[] buffer, byte msgId)
    {
        // MAVLink X.25 CRC calculation
        ushort crc = 0xFFFF;
        
        foreach (byte b in buffer)
        {
            byte tmp = (byte)(b ^ (byte)(crc & 0xFF));
            tmp ^= (byte)(tmp << 4);
            crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
        }
        
        // Add CRC_EXTRA byte (message-specific)
        byte crcExtra = GetCrcExtra(msgId);
        byte tmp2 = (byte)(crcExtra ^ (byte)(crc & 0xFF));
        tmp2 ^= (byte)(tmp2 << 4);
        crc = (ushort)((crc >> 8) ^ (tmp2 << 8) ^ (tmp2 << 3) ^ (tmp2 >> 4));
        
        return crc;
    }

    private byte GetCrcExtra(byte msgId)
    {
        // CRC_EXTRA values for MAVLink messages
        return msgId switch
        {
            20 => 214, // PARAM_REQUEST_READ
            21 => 159, // PARAM_REQUEST_LIST
            22 => 220, // PARAM_VALUE
            23 => 168, // PARAM_SET
            _ => 0
        };
    }

    // This method is called by ConnectionService when PARAM_VALUE messages are received
    public void OnParameterValueReceived(string name, float value, int index, int count)
    {
        var param = new DroneParameter
        {
            Name = name,
            Value = value,
            Description = $"Parameter {index + 1} of {count}"
        };

        _parameters[name] = param;
        _logger.LogInformation("📥 PARAM_VALUE: {Name} = {Value} (#{Index}/{Count})", 
            name, value, index + 1, count);
        
        // Notify subscribers that this parameter was updated
        ParameterUpdated?.Invoke(this, param);
    }
}
