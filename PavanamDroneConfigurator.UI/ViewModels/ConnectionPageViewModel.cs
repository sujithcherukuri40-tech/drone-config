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
    private readonly IParameterService _parameterService;

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

    public ConnectionPageViewModel(
        IConnectionService connectionService, 
        ITelemetryService telemetryService,
        IParameterService parameterService)
    {
        _connectionService = connectionService;
        _telemetryService = telemetryService;
        _parameterService = parameterService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        _telemetryService.TelemetryUpdated += (s, telemetry) =>
        {
            CurrentTelemetry = telemetry;
        };
    }

    private async void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        StatusMessage = connected ? "Connected" : "Disconnected";

        if (connected)
        {
            // Start telemetry when connected
            _telemetryService.Start();
            
            // Load parameters when connected
            StatusMessage = "Connected - Loading parameters...";
            await _parameterService.RefreshParametersAsync();
            StatusMessage = "Connected - Parameters loaded";
        }
        else
        {
            // Stop telemetry when disconnected
            _telemetryService.Stop();
            
            // Clear telemetry data
            CurrentTelemetry = null;
        }
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

        StatusMessage = "Connecting...";
        var result = await _connectionService.ConnectAsync(settings);
        
        if (!result)
        {
            StatusMessage = "Connection failed";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        StatusMessage = "Disconnecting...";
        await _connectionService.DisconnectAsync();
    }
}
