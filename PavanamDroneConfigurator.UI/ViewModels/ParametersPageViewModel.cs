using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ParametersPageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
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
        _parameterService.ParameterUpdated += OnParameterUpdated;
        
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
                _hasLoadedParameters = false;
                StatusMessage = _parameterService.IsParameterDownloadComplete
                    ? "Parameters ready"
                    : "Waiting for parameter download...";
            }
            else
            {
                // Clear parameters when disconnected
                Parameters.Clear();
                var downloadInterrupted = _parameterService.IsParameterDownloadInProgress ||
                                          (!_parameterService.IsParameterDownloadComplete &&
                                           _parameterService.ReceivedParameterCount > 0);
                if (downloadInterrupted)
                {
                    StatusMessage = "Disconnected during parameter download - parameters unavailable";
                }
                else
                {
                    StatusMessage = "Disconnected - Parameters cleared";
                }
                _hasLoadedParameters = false;
                CanEditParameters = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error handling connection state: {ex.Message}";
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

        try
        {
            StatusMessage = "Loading parameters...";
            var parameters = await _parameterService.GetAllParametersAsync();
            Parameters.Clear();
            foreach (var p in parameters)
            {
                Parameters.Add(p);
            }
            StatusMessage = $"Loaded {Parameters.Count} parameters";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading parameters: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            return;
        }

        try
        {
            StatusMessage = "Refreshing parameters...";
            await _parameterService.RefreshParametersAsync();
            await LoadParametersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing parameters: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveParameterAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Cannot save parameter.";
            return;
        }

        if (SelectedParameter == null)
        {
            StatusMessage = "No parameter selected.";
            return;
        }

        try
        {
            StatusMessage = $"Saving {SelectedParameter.Name} = {SelectedParameter.Value}...";
            
            var success = await _parameterService.SetParameterAsync(SelectedParameter.Name, SelectedParameter.Value);
            
            if (success)
            {
                StatusMessage = $"? Successfully saved {SelectedParameter.Name} = {SelectedParameter.Value}";
            }
            else
            {
                StatusMessage = $"? Failed to save {SelectedParameter.Name}. Timeout or not acknowledged.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving parameter: {ex.Message}";
        }
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(UpdateParameterDownloadStateAsync);
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var updatedParameter = await _parameterService.GetParameterAsync(parameterName);
            if (updatedParameter == null)
            {
                return;
            }

            for (int i = 0; i < Parameters.Count; i++)
            {
                if (string.Equals(Parameters[i].Name, updatedParameter.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Parameters[i] = updatedParameter;
                    if (SelectedParameter != null &&
                        string.Equals(SelectedParameter.Name, updatedParameter.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedParameter = updatedParameter;
                    }
                    return;
                }
            }

            Parameters.Add(updatedParameter);
        });
    }

    private async Task UpdateParameterDownloadStateAsync()
    {
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
    }
}
