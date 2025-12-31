using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using System.Collections.ObjectModel;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ProfilePageViewModel : ViewModelBase
{
    private readonly IPersistenceService _persistenceService;

    [ObservableProperty]
    private ObservableCollection<string> _profiles = new();

    [ObservableProperty]
    private string _selectedProfile = string.Empty;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ProfilePageViewModel(IPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
    }

    [RelayCommand]
    private async Task LoadProfilesAsync()
    {
        var profiles = await _persistenceService.GetProfileNamesAsync();
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }
        StatusMessage = $"Found {Profiles.Count} profiles";
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
        {
            StatusMessage = "Please enter a profile name";
            return;
        }

        var data = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.Now.ToString("O")
        };

        var success = await _persistenceService.SaveProfileAsync(NewProfileName, data);
        StatusMessage = success ? $"Profile '{NewProfileName}' saved" : "Failed to save profile";

        if (success)
        {
            await LoadProfilesAsync();
            NewProfileName = string.Empty;
        }
    }

    [RelayCommand]
    private async Task LoadProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedProfile))
        {
            StatusMessage = "Please select a profile";
            return;
        }

        var data = await _persistenceService.LoadProfileAsync(SelectedProfile);
        StatusMessage = data != null ? $"Profile '{SelectedProfile}' loaded" : "Failed to load profile";
    }
}
