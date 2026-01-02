using System;
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
    private readonly IParameterService _parameterService;
    private bool _downloadInProgress;

    [ObservableProperty]
    private ObservableCollection<SerialPortInfo> _availableSerialPorts = new();

    [ObservableProperty]
    private SerialPortInfo? _selectedSerialPort;

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

    public ConnectionPageViewModel(
        IConnectionService connectionService, 
        IParameterService parameterService)
    {
        _connectionService = connectionService;
        _parameterService = parameterService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.AvailableSerialPortsChanged += OnAvailableSerialPortsChanged;
        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;
        _downloadInProgress = _parameterService.IsParameterDownloadInProgress;

        var ports = _connectionService.GetAvailableSerialPorts().ToList();
        AvailableSerialPorts = new ObservableCollection<SerialPortInfo>(ports);
        if (ports.Any())
        {
            SelectedSerialPort = ports.First();
        }
    }

    private void OnAvailableSerialPortsChanged(object? sender, IEnumerable<SerialPortInfo> ports)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AvailableSerialPorts.Clear();
            foreach (var port in ports)
            {
                AvailableSerialPorts.Add(port);
            }

            var selectedPortName = SelectedSerialPort?.PortName;
            if (AvailableSerialPorts.Any() && (selectedPortName == null || !AvailableSerialPorts.Any(p => p.PortName == selectedPortName)))
            {
                SelectedSerialPort = AvailableSerialPorts.First();
            }
        });
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        try
        {
            IsConnected = connected;
            StatusMessage = connected ? "Connected" : "Disconnected";
            SetConnectionIndicator(connected ? "Connected" : "Disconnected", connected ? Brushes.Green : Brushes.Red);

            if (connected)
            {
                _downloadInProgress = true;
                StatusMessage = "Connected - Downloading parameters...";
            }
            else
            {
                var interruptedDownload = _downloadInProgress && !_parameterService.IsParameterDownloadComplete;
                StatusMessage = interruptedDownload
                    ? "Disconnected during parameter download"
                    : "Disconnected";
                _downloadInProgress = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during connection state change: {ex.Message}";
        }
    }

    private void SetConnectionIndicator(string text, IBrush brush)
    {
        ConnectionStatusText = text;
        ConnectionStatusBrush = brush;
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _downloadInProgress = _parameterService.IsParameterDownloadInProgress;
            if (_parameterService.IsParameterDownloadInProgress)
            {
                var expected = _parameterService.ExpectedParameterCount.HasValue
                    ? _parameterService.ExpectedParameterCount.Value.ToString()
                    : "?";
                StatusMessage = $"Downloading parameters... {_parameterService.ReceivedParameterCount}/{expected}";
            }
            else if (_parameterService.IsParameterDownloadComplete && _connectionService.IsConnected)
            {
                StatusMessage = "Connected - Parameters loaded";
            }
        });
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var settings = new ConnectionSettings
        {
            Type = ConnectionType,
            PortName = SelectedSerialPort?.PortName ?? string.Empty,
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
