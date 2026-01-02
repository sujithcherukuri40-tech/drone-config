using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Generic;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class AirframePageViewModel : ViewModelBase
{
    private readonly IAirframeService _airframeService;
    private readonly IConnectionService _connectionService;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _selectedFrameClass;

    [ObservableProperty]
    private int _selectedFrameType;

    [ObservableProperty]
    private string _currentFrameName = "Not loaded";

    // Available frame classes
    public List<FrameClassOption> AvailableFrameClasses { get; } = new()
    {
        new FrameClassOption(1, "Quad", "Standard quadcopter with 4 motors"),
        new FrameClassOption(2, "Hexa", "Hexacopter with 6 motors"),
        new FrameClassOption(3, "Octa", "Octocopter with 8 motors"),
        new FrameClassOption(4, "OctaQuad", "Octo-Quad configuration"),
        new FrameClassOption(5, "Y6", "Y6 configuration"),
        new FrameClassOption(7, "Tri", "Tricopter with 3 motors"),
        new FrameClassOption(10, "BiCopter", "Dual rotor helicopter"),
        new FrameClassOption(13, "HeliQuad", "Helicopter-Quad hybrid")
    };

    // Available frame types (configurations)
    public List<FrameTypeOption> AvailableFrameTypes { get; } = new()
    {
        new FrameTypeOption(0, "Plus (+)", "Plus configuration"),
        new FrameTypeOption(1, "X", "X configuration"),
        new FrameTypeOption(2, "V", "V configuration"),
        new FrameTypeOption(3, "H", "H configuration"),
        new FrameTypeOption(4, "V-Tail", "V-Tail configuration"),
        new FrameTypeOption(5, "A-Tail", "A-Tail configuration")
    };

    public AirframePageViewModel(IAirframeService airframeService, IConnectionService connectionService)
    {
        _airframeService = airframeService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = _connectionService.IsConnected;
    }

    private async void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        
        if (connected)
        {
            await LoadSettingsAsync();
        }
        else
        {
            StatusMessage = "Disconnected";
            CurrentFrameName = "Not connected";
        }
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading airframe settings...";

        try
        {
            var settings = await _airframeService.GetAirframeSettingsAsync();
            if (settings != null)
            {
                SelectedFrameClass = settings.FrameClass;
                SelectedFrameType = settings.FrameType;
                CurrentFrameName = settings.FrameName;

                StatusMessage = "Airframe settings loaded successfully";
            }
            else
            {
                StatusMessage = "Failed to load airframe settings";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplySettingsAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected. Cannot apply settings.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Applying airframe settings...";

        try
        {
            var frameClassName = AvailableFrameClasses.FirstOrDefault(f => f.Value == SelectedFrameClass)?.Name ?? "Unknown";
            var frameTypeName = AvailableFrameTypes.FirstOrDefault(f => f.Value == SelectedFrameType)?.Name ?? "Unknown";
            
            var settings = new AirframeSettings
            {
                FrameClass = SelectedFrameClass,
                FrameType = SelectedFrameType,
                FrameName = $"{frameClassName} {frameTypeName}"
            };

            var success = await _airframeService.UpdateAirframeSettingsAsync(settings);
            
            if (success)
            {
                CurrentFrameName = settings.FrameName;
                StatusMessage = "Airframe settings applied successfully. Reboot required for changes to take effect.";
            }
            else
            {
                StatusMessage = "Failed to apply airframe settings";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSettingsAsync()
    {
        await LoadSettingsAsync();
    }
}

// Helper classes for dropdown options
public class FrameClassOption
{
    public int Value { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public FrameClassOption(int value, string name, string description)
    {
        Value = value;
        Name = name;
        Description = description;
    }
}

public class FrameTypeOption
{
    public int Value { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public FrameTypeOption(int value, string name, string description)
    {
        Value = value;
        Name = name;
        Description = description;
    }
}
