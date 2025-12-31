using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.ObjectModel;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ParametersPageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;

    [ObservableProperty]
    private ObservableCollection<DroneParameter> _parameters = new();

    [ObservableProperty]
    private DroneParameter? _selectedParameter;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ParametersPageViewModel(IParameterService parameterService)
    {
        _parameterService = parameterService;
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
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

    [RelayCommand]
    private async Task SaveParameterAsync()
    {
        if (SelectedParameter != null)
        {
            await _parameterService.SetParameterAsync(SelectedParameter.Name, SelectedParameter.Value);
            StatusMessage = $"Saved {SelectedParameter.Name}";
        }
    }
}
