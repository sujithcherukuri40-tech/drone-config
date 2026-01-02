using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;
using System.IO;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IConnectionService
{
    Task<bool> ConnectAsync(ConnectionSettings settings);
    Task DisconnectAsync();
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStateChanged;
    IEnumerable<SerialPortInfo> GetAvailableSerialPorts();
    event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;
    void RegisterParameterService(IParameterService parameterService);
    Stream? GetTransportStream();
}
