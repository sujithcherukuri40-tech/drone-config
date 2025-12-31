using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PavanamDroneConfigurator.Core.Interfaces;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private bool _isParameterDownloadInProgress;

    [ObservableProperty]
    private bool _isParameterDownloadComplete;

    [ObservableProperty]
    private int _parameterDownloadReceived;

    [ObservableProperty]
    private int? _parameterDownloadExpected;

    [ObservableProperty]
    private string _parameterDownloadStatusText = "Downloading parameters from vehicle...";

    [ObservableProperty]
    private bool _canAccessParameters;

    public ConnectionPageViewModel ConnectionPage { get; }
    public ParametersPageViewModel ParametersPage { get; }
    public CalibrationPageViewModel CalibrationPage { get; }
    public SafetyPageViewModel SafetyPage { get; }
    public ProfilePageViewModel ProfilePage { get; }

    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    public MainWindowViewModel(
        ConnectionPageViewModel connectionPage,
        ParametersPageViewModel parametersPage,
        CalibrationPageViewModel calibrationPage,
        SafetyPageViewModel safetyPage,
        ProfilePageViewModel profilePage,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        ConnectionPage = connectionPage;
        ParametersPage = parametersPage;
        CalibrationPage = calibrationPage;
        SafetyPage = safetyPage;
        ProfilePage = profilePage;
        _parameterService = parameterService;
        _connectionService = connectionService;

        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        UpdateParameterDownloadState();

        _currentPage = connectionPage;
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateParameterDownloadState);
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(UpdateParameterDownloadState);
    }

    private void UpdateParameterDownloadState()
    {
        IsParameterDownloadInProgress = _parameterService.IsParameterDownloadInProgress;
        IsParameterDownloadComplete = _parameterService.IsParameterDownloadComplete;
        ParameterDownloadReceived = _parameterService.ReceivedParameterCount;
        ParameterDownloadExpected = _parameterService.ExpectedParameterCount;
        var expectedText = ParameterDownloadExpected.HasValue ? ParameterDownloadExpected.Value.ToString() : "?";
        ParameterDownloadStatusText = $"{ParameterDownloadReceived}/{expectedText}";
        CanAccessParameters = _connectionService.IsConnected && _parameterService.IsParameterDownloadComplete;
    }
}
