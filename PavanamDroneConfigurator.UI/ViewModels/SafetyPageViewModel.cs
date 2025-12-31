using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class SafetyPageViewModel : ViewModelBase
{
    private readonly ISafetyService _safetyService;

    [ObservableProperty]
    private SafetySettings _settings = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SafetyPageViewModel(ISafetyService safetyService)
    {
        _safetyService = safetyService;
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        StatusMessage = "Loading safety settings...";
        var settings = await _safetyService.GetSafetySettingsAsync();
        if (settings != null)
        {
            Settings = settings;
            StatusMessage = "Safety settings loaded";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        StatusMessage = "Saving safety settings...";
        var success = await _safetyService.UpdateSafetySettingsAsync(Settings);
        StatusMessage = success ? "Safety settings saved" : "Failed to save settings";
    }
}
