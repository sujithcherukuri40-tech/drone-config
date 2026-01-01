using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PavanamDroneConfigurator.Core.Interfaces;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
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

    [ObservableProperty]
    private bool _canAccessAirframe;

    public ConnectionPageViewModel ConnectionPage { get; }
    public AirframePageViewModel AirframePage { get; }
    public ParametersPageViewModel ParametersPage { get; }
    public CalibrationPageViewModel CalibrationPage { get; }
    public SafetyPageViewModel SafetyPage { get; }
    public ProfilePageViewModel ProfilePage { get; }

    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    public MainWindowViewModel(
        ConnectionPageViewModel connectionPage,
        AirframePageViewModel airframePage,
        ParametersPageViewModel parametersPage,
        CalibrationPageViewModel calibrationPage,
        SafetyPageViewModel safetyPage,
        ProfilePageViewModel profilePage,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        ConnectionPage = connectionPage;
        AirframePage = airframePage;
        ParametersPage = parametersPage;
        CalibrationPage = calibrationPage;
        SafetyPage = safetyPage;
        ProfilePage = profilePage;
        _parameterService = parameterService;
        _connectionService = connectionService;

        _parameterService.ParameterDownloadStarted += OnParameterDownloadStarted;
        _parameterService.ParameterDownloadCompleted += OnParameterDownloadCompleted;
        _parameterService.ParameterUpdated += OnParameterUpdated;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        InitializeFromServices();

        _currentPage = connectionPage;
    }

    private void OnParameterDownloadStarted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsParameterDownloadInProgress = true;
            IsParameterDownloadComplete = false;
            UpdateProgress();
            UpdateAccessPermissions();
        });
    }

    private void OnParameterDownloadCompleted(object? sender, bool completedSuccessfully)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsParameterDownloadInProgress = false;
            IsParameterDownloadComplete = completedSuccessfully;
            UpdateProgress();
            UpdateAccessPermissions();
        });
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsParameterDownloadInProgress)
            {
                UpdateProgress();
            }
        });
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(UpdateAccessPermissions);
    }

    private void InitializeFromServices()
    {
        IsParameterDownloadInProgress = _parameterService.IsParameterDownloadInProgress;
        IsParameterDownloadComplete = _parameterService.IsParameterDownloadComplete;
        UpdateProgress();
        UpdateAccessPermissions();
    }

    private void UpdateProgress()
    {
        ParameterDownloadReceived = _parameterService.ReceivedParameterCount;
        ParameterDownloadExpected = _parameterService.ExpectedParameterCount;
        var expectedText = ParameterDownloadExpected.HasValue ? ParameterDownloadExpected.Value.ToString() : "?";
        ParameterDownloadStatusText = $"{ParameterDownloadReceived}/{expectedText}";
    }

    private void UpdateAccessPermissions()
    {
        var connected = _connectionService.IsConnected;
        var parametersReady = _parameterService.IsParameterDownloadComplete;
        CanAccessParameters = connected && parametersReady;
        CanAccessAirframe = connected && parametersReady;
    }

    public void Dispose()
    {
        _parameterService.ParameterDownloadStarted -= OnParameterDownloadStarted;
        _parameterService.ParameterDownloadCompleted -= OnParameterDownloadCompleted;
        _parameterService.ParameterUpdated -= OnParameterUpdated;
        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
