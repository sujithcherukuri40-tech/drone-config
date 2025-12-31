using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ConnectionPageViewModel : ViewModelBase
{
    private readonly IConnectionService _connectionService;
    private readonly ITelemetryService _telemetryService;
    private readonly IParameterService _parameterService;

    [ObservableProperty]
    private ObservableCollection<string> _availableSerialPorts = new();

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
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private IBrush _connectionStatusBrush = Brushes.Red;

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
        _connectionService.AvailableSerialPortsChanged += OnAvailableSerialPortsChanged;

        var ports = _connectionService.GetAvailableSerialPorts().ToList();
        AvailableSerialPorts = new ObservableCollection<string>(ports);
        if (ports.Any())
        {
            SelectedPortName = ports.First();
        }

        _telemetryService.TelemetryUpdated += (s, telemetry) =>
        {
            CurrentTelemetry = telemetry;
        };
    }

    private void OnAvailableSerialPortsChanged(object? sender, IEnumerable<string> ports)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AvailableSerialPorts.Clear();
            foreach (var port in ports)
            {
                AvailableSerialPorts.Add(port);
            }

            if (AvailableSerialPorts.Any() && (string.IsNullOrEmpty(SelectedPortName) || !AvailableSerialPorts.Contains(SelectedPortName)))
            {
                SelectedPortName = AvailableSerialPorts.First();
            }
        });
    }

    private async void OnConnectionStateChanged(object? sender, bool connected)
    {
        try
        {
            IsConnected = connected;
            StatusMessage = connected ? "Connected" : "Disconnected";
            SetConnectionIndicator(connected ? "Connected" : "Disconnected", connected ? Brushes.Green : Brushes.Red);

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
        catch (Exception ex)
        {
            StatusMessage = $"Error during connection state change: {ex.Message}";
            // In production, this should be logged via ILogger
        }
    }

    private void SetConnectionIndicator(string text, IBrush brush)
    {
        ConnectionStatusText = text;
        ConnectionStatusBrush = brush;
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
        SetConnectionIndicator("Connecting", Brushes.Gold);
        var result = await _connectionService.ConnectAsync(settings);
        
        if (!result)
        {
            StatusMessage = "Connection failed";
            SetConnectionIndicator("Disconnected", Brushes.Red);
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        StatusMessage = "Disconnecting...";
        await _connectionService.DisconnectAsync();
        SetConnectionIndicator("Disconnected", Brushes.Red);
    }
}
