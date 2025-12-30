using ReactiveUI;
using System.Reactive;
using System.Collections.ObjectModel;
using System.IO.Ports;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using PavamanDroneConfigurator.Infrastructure.Helpers;

namespace PavamanDroneConfigurator.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    private readonly IMavlinkService _mavlinkService;
    private readonly IParameterService _parameterService;
    private readonly ITelemetryService _telemetryService;
    
    private ConnectionType _selectedConnectionType = ConnectionType.Serial;
    private string? _selectedPort;
    private int _selectedBaudRate = 115200;
    private int _selectedDataBits = 8;
    private Parity _selectedParity = Parity.None;
    private StopBits _selectedStopBits = StopBits.One;
    private string _host = "127.0.0.1";
    private int _port = 5760;
    private bool _isConnected;
    private string _connectionStatus = "Disconnected";
    
    // Realtime telemetry data
    private double _voltage;
    private double _current;
    private int _batteryRemaining;
    private double _latitude;
    private double _longitude;
    private double _altitude;
    private double _roll;
    private double _pitch;
    private double _yaw;
    private string _flightMode = "UNKNOWN";
    private bool _isArmed;
    private double _groundSpeed;
    private double _airSpeed;
    private int _satelliteCount;
    private string _gpsStatus = "NO GPS";
    
    // Parameter loading
    private bool _isLoadingParameters;
    private int _parametersLoaded;
    private int _totalParameters;
    private string _currentParameter = "";
    private string _frameType = "Unknown";
    
    private IDisposable? _telemetrySubscription;
    private IDisposable? _parameterProgressSubscription;

    public ConnectionViewModel(IMavlinkService mavlinkService, IParameterService parameterService, ITelemetryService telemetryService)
    {
        _mavlinkService = mavlinkService;
        _parameterService = parameterService;
        _telemetryService = telemetryService;
        
        // Initialize available ports
        AvailablePorts = new ObservableCollection<string>(SerialPortHelper.GetAvailablePorts());
        AvailableBaudRates = new ObservableCollection<int>(SerialPortHelper.GetStandardBaudRates());
        AvailableDataBits = new ObservableCollection<int> { 6, 7, 8 };
        AvailableParities = new ObservableCollection<Parity> { Parity.None, Parity.Even, Parity.Odd };
        AvailableStopBits = new ObservableCollection<StopBits> { StopBits.One, StopBits.Two };
        
        if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
        
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync);
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
        
        // Subscribe to link state
        _mavlinkService.LinkState.Subscribe(state =>
        {
            IsConnected = state == Core.Services.Interfaces.LinkState.Connected;
            ConnectionStatus = state.ToString();
        });
        
        // Subscribe to telemetry updates
        _telemetrySubscription = _telemetryService.TelemetryUpdates.Subscribe(telemetry =>
        {
            Voltage = telemetry.Voltage;
            Current = telemetry.Current;
            BatteryRemaining = telemetry.BatteryRemaining;
            Latitude = telemetry.Latitude;
            Longitude = telemetry.Longitude;
            Altitude = telemetry.Altitude;
            Roll = telemetry.Roll * (180.0 / Math.PI); // Convert to degrees
            Pitch = telemetry.Pitch * (180.0 / Math.PI);
            Yaw = telemetry.Yaw * (180.0 / Math.PI);
        });
        
        // Subscribe to parameter download progress
        _parameterProgressSubscription = _parameterService.DownloadProgress.Subscribe(progress =>
        {
            ParametersLoaded = progress.Current;
            TotalParameters = progress.Total;
            CurrentParameter = progress.CurrentParameter ?? "";
            IsLoadingParameters = progress.Current < progress.Total;
        });
    }

    public ObservableCollection<string> AvailablePorts { get; }
    public ObservableCollection<int> AvailableBaudRates { get; }
    public ObservableCollection<int> AvailableDataBits { get; }
    public ObservableCollection<Parity> AvailableParities { get; }
    public ObservableCollection<StopBits> AvailableStopBits { get; }

    public ConnectionType SelectedConnectionType
    {
        get => _selectedConnectionType;
        set => this.RaiseAndSetIfChanged(ref _selectedConnectionType, value);
    }

    public string? SelectedPort
    {
        get => _selectedPort;
        set => this.RaiseAndSetIfChanged(ref _selectedPort, value);
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => this.RaiseAndSetIfChanged(ref _selectedBaudRate, value);
    }

    public int SelectedDataBits
    {
        get => _selectedDataBits;
        set => this.RaiseAndSetIfChanged(ref _selectedDataBits, value);
    }

    public Parity SelectedParity
    {
        get => _selectedParity;
        set => this.RaiseAndSetIfChanged(ref _selectedParity, value);
    }

    public StopBits SelectedStopBits
    {
        get => _selectedStopBits;
        set => this.RaiseAndSetIfChanged(ref _selectedStopBits, value);
    }

    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => this.RaiseAndSetIfChanged(ref _connectionStatus, value);
    }

    // Realtime Telemetry Properties
    public double Voltage
    {
        get => _voltage;
        set => this.RaiseAndSetIfChanged(ref _voltage, value);
    }

    public double Current
    {
        get => _current;
        set => this.RaiseAndSetIfChanged(ref _current, value);
    }

    public int BatteryRemaining
    {
        get => _batteryRemaining;
        set => this.RaiseAndSetIfChanged(ref _batteryRemaining, value);
    }

    public double Latitude
    {
        get => _latitude;
        set => this.RaiseAndSetIfChanged(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => this.RaiseAndSetIfChanged(ref _longitude, value);
    }

    public double Altitude
    {
        get => _altitude;
        set => this.RaiseAndSetIfChanged(ref _altitude, value);
    }

    public double Roll
    {
        get => _roll;
        set => this.RaiseAndSetIfChanged(ref _roll, value);
    }

    public double Pitch
    {
        get => _pitch;
        set => this.RaiseAndSetIfChanged(ref _pitch, value);
    }

    public double Yaw
    {
        get => _yaw;
        set => this.RaiseAndSetIfChanged(ref _yaw, value);
    }

    public string FlightMode
    {
        get => _flightMode;
        set => this.RaiseAndSetIfChanged(ref _flightMode, value);
    }

    public bool IsArmed
    {
        get => _isArmed;
        set => this.RaiseAndSetIfChanged(ref _isArmed, value);
    }

    public double GroundSpeed
    {
        get => _groundSpeed;
        set => this.RaiseAndSetIfChanged(ref _groundSpeed, value);
    }

    public double AirSpeed
    {
        get => _airSpeed;
        set => this.RaiseAndSetIfChanged(ref _airSpeed, value);
    }

    public int SatelliteCount
    {
        get => _satelliteCount;
        set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    public string GpsStatus
    {
        get => _gpsStatus;
        set => this.RaiseAndSetIfChanged(ref _gpsStatus, value);
    }

    // Parameter Loading Properties
    public bool IsLoadingParameters
    {
        get => _isLoadingParameters;
        set => this.RaiseAndSetIfChanged(ref _isLoadingParameters, value);
    }

    public int ParametersLoaded
    {
        get => _parametersLoaded;
        set => this.RaiseAndSetIfChanged(ref _parametersLoaded, value);
    }

    public int TotalParameters
    {
        get => _totalParameters;
        set => this.RaiseAndSetIfChanged(ref _totalParameters, value);
    }

    public string CurrentParameter
    {
        get => _currentParameter;
        set => this.RaiseAndSetIfChanged(ref _currentParameter, value);
    }

    public string FrameType
    {
        get => _frameType;
        set => this.RaiseAndSetIfChanged(ref _frameType, value);
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }

    private async Task ConnectAsync()
    {
        bool success = false;
        
        switch (SelectedConnectionType)
        {
            case ConnectionType.Serial:
                if (!string.IsNullOrEmpty(SelectedPort))
                {
                    success = await _mavlinkService.ConnectSerialAsync(
                        SelectedPort, SelectedBaudRate, SelectedDataBits, 
                        SelectedParity, SelectedStopBits);
                }
                break;
            case ConnectionType.TCP:
                success = await _mavlinkService.ConnectTcpAsync(Host, Port);
                break;
            case ConnectionType.UDP:
                success = await _mavlinkService.ConnectUdpAsync(Host, Port);
                break;
        }
        
        if (success)
        {
            // Automatically load all parameters (Mission Planner style)
            _ = LoadParametersAsync();
        }
        else
        {
            ConnectionStatus = "Connection failed";
        }
    }

    private async Task LoadParametersAsync()
    {
        try
        {
            IsLoadingParameters = true;
            ConnectionStatus = "Loading parameters...";
            
            var parameters = await _parameterService.ReadAllParametersAsync();
            
            // Detect frame type from parameters
            if (parameters.TryGetValue("FRAME_TYPE", out var frameTypeParam))
            {
                FrameType = GetFrameTypeName((int)frameTypeParam.Value);
            }
            else if (parameters.TryGetValue("FRAME_CLASS", out var frameClassParam))
            {
                FrameType = GetFrameClassName((int)frameClassParam.Value);
            }
            
            ConnectionStatus = $"Connected - {parameters.Count} parameters loaded";
            IsLoadingParameters = false;
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Parameter loading failed: {ex.Message}";
            IsLoadingParameters = false;
        }
    }

    private string GetFrameTypeName(int frameType)
    {
        return frameType switch
        {
            0 => "Plus",
            1 => "X",
            2 => "V",
            3 => "H",
            10 => "Octa Plus",
            11 => "Octa X",
            12 => "Octa V",
            13 => "Octa H",
            14 => "Y6",
            _ => $"Unknown ({frameType})"
        };
    }

    private string GetFrameClassName(int frameClass)
    {
        return frameClass switch
        {
            0 => "Undefined",
            1 => "Quad",
            2 => "Hexa",
            3 => "Octa",
            4 => "OctaQuad",
            5 => "Y6",
            6 => "Heli",
            7 => "Tri",
            8 => "SingleCopter",
            9 => "CoaxCopter",
            10 => "BiCopter",
            11 => "Heli Dual",
            12 => "DodecaHexa",
            13 => "HeliQuad",
            _ => $"Unknown ({frameClass})"
        };
    }

    private async Task DisconnectAsync()
    {
        await _mavlinkService.DisconnectAsync();
        
        // Reset telemetry data
        Voltage = 0;
        Current = 0;
        BatteryRemaining = 0;
        Latitude = 0;
        Longitude = 0;
        Altitude = 0;
        Roll = 0;
        Pitch = 0;
        Yaw = 0;
        FlightMode = "UNKNOWN";
        IsArmed = false;
        FrameType = "Unknown";
        ParametersLoaded = 0;
        TotalParameters = 0;
    }

    private void RefreshPorts()
    {
        var currentPort = SelectedPort;
        AvailablePorts.Clear();
        foreach (var port in SerialPortHelper.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }
        
        if (AvailablePorts.Contains(currentPort!))
            SelectedPort = currentPort;
        else if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }
}
