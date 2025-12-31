using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IConnectionService
{
    Task<bool> ConnectAsync(ConnectionSettings settings);
    Task DisconnectAsync();
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStateChanged;
    IEnumerable<SerialPortInfo> GetAvailableSerialPorts();
    event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;
}
