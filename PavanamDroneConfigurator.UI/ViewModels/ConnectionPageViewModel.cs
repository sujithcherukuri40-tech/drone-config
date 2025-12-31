using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ConnectionPageViewModel : ViewModelBase
{
    private readonly IConnectionService _connectionService;
    private readonly ITelemetryService _telemetryService;

    [ObservableProperty]
    private string _selectedPortName = "COM3";

    [ObservableProperty]
    private int _baudRate = 115200;

    [ObservableProperty]
    private string _ipAddress = "127.0.0.1";

    [ObservableProperty]
    private int _tcpPort = 5760;

    [ObservableProperty]
    private ConnectionType _connectionType = ConnectionType.Serial;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    [ObservableProperty]
    private TelemetryData? _currentTelemetry;

    public ConnectionPageViewModel(IConnectionService connectionService, ITelemetryService telemetryService)
    {
        _connectionService = connectionService;
        _telemetryService = telemetryService;

        _connectionService.ConnectionStateChanged += (s, connected) =>
        {
            IsConnected = connected;
            StatusMessage = connected ? "Connected" : "Disconnected";
        };

        _telemetryService.TelemetryUpdated += (s, telemetry) =>
        {
            CurrentTelemetry = telemetry;
        };
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var settings = new ConnectionSettings
        {
            Type = ConnectionType,
            PortName = SelectedPortName,
            BaudRate = BaudRate,
            IpAddress = IpAddress,
            Port = TcpPort
        };

        await _connectionService.ConnectAsync(settings);
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _connectionService.DisconnectAsync();
    }
}
