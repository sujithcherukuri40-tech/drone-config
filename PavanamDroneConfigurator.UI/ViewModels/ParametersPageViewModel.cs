using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.ObjectModel;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ParametersPageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    private bool _downloadInProgress;
    private bool _hasLoadedParameters;

    [ObservableProperty]
    private ObservableCollection<DroneParameter> _parameters = new();

    [ObservableProperty]
    private DroneParameter? _selectedParameter;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _canEditParameters;

    public ParametersPageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        // Subscribe to connection state changes
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;
        
        // Initialize can edit state
        CanEditParameters = _connectionService.IsConnected && _parameterService.IsParameterDownloadComplete;
    }

    private async void OnConnectionStateChanged(object? sender, bool connected)
    {
        try
        {
            if (connected)
            {
                CanEditParameters = _parameterService.IsParameterDownloadComplete;
                _downloadInProgress = _parameterService.IsParameterDownloadInProgress;
                _hasLoadedParameters = false;
                StatusMessage = _parameterService.IsParameterDownloadComplete
                    ? "Parameters ready"
                    : "Waiting for parameter download...";
            }
            else
            {
                // Clear parameters when disconnected
                Parameters.Clear();
                var interruptedDownload = _downloadInProgress && !_parameterService.IsParameterDownloadComplete;
                StatusMessage = interruptedDownload
                    ? "Disconnected during parameter download - parameters unavailable"
                    : "Disconnected - Parameters cleared";
                _downloadInProgress = false;
                _hasLoadedParameters = false;
                CanEditParameters = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error handling connection state: {ex.Message}";
            // In production, this should be logged via ILogger
        }
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            return;
        }

        StatusMessage = "Loading parameters...";
        var parameters = await _parameterService.GetAllParametersAsync();
        Parameters.Clear();
        foreach (var p in parameters)
        {
            Parameters.Add(p);
        }
        StatusMessage = $"Loaded {Parameters.Count} parameters";
    }

    [RelayCommand]
    private async Task RefreshParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            return;
        }

        StatusMessage = "Refreshing parameters...";
        await _parameterService.RefreshParametersAsync();
        await LoadParametersAsync();
    }

    [RelayCommand]
    private async Task SaveParameterAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Cannot save parameter.";
            return;
        }

        if (SelectedParameter != null)
        {
            var updated = await _parameterService.SetParameterAsync(SelectedParameter.Name, SelectedParameter.Value);
            StatusMessage = updated
                ? $"Saved {SelectedParameter.Name} = {SelectedParameter.Value}"
                : $"Failed to save {SelectedParameter.Name}";
        }
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            _downloadInProgress = _parameterService.IsParameterDownloadInProgress;
            CanEditParameters = _connectionService.IsConnected && _parameterService.IsParameterDownloadComplete;

            if (_parameterService.IsParameterDownloadInProgress)
            {
                var expected = _parameterService.ExpectedParameterCount.HasValue
                    ? _parameterService.ExpectedParameterCount.Value.ToString()
                    : "?";
                StatusMessage = $"Downloading parameters... {_parameterService.ReceivedParameterCount}/{expected}";
            }
            else if (_parameterService.IsParameterDownloadComplete && _connectionService.IsConnected && !_hasLoadedParameters)
            {
                await LoadParametersAsync();
                StatusMessage = $"Parameters downloaded ({Parameters.Count})";
                _hasLoadedParameters = true;
            }
            else if (!_connectionService.IsConnected)
            {
                Parameters.Clear();
                StatusMessage = "Disconnected - Parameters cleared";
            }
        });
    }
}
