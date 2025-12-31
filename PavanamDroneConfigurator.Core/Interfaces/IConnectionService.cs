using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IConnectionService
{
    Task<bool> ConnectAsync(ConnectionSettings settings);
    Task DisconnectAsync();
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStateChanged;
}
