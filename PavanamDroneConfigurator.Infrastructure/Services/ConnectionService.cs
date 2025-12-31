using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.IO.Ports;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ConnectionService : IConnectionService
{
    private readonly ILogger<ConnectionService> _logger;
    private SerialPort? _serialPort;
    private bool _isConnected;

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

            // Simulate connection establishment
            await Task.Delay(500);

            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, true);

            _logger.LogInformation("Connected successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect");
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        if (_isConnected)
        {
            _logger.LogInformation("Disconnecting...");

            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;

            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }

        return Task.CompletedTask;
    }
}
