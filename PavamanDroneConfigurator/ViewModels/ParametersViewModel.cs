using ReactiveUI;
using System.Reactive;
using PavamanDroneConfigurator.Core.Services.Interfaces;

namespace PavamanDroneConfigurator.ViewModels;

public class ParametersViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;

    public ParametersViewModel(IParameterService parameterService)
    {
        _parameterService = parameterService;
        
        ResetParametersCommand = ReactiveCommand.CreateFromTask(ResetParametersAsync);
    }

    public ReactiveCommand<Unit, Unit> ResetParametersCommand { get; }

    private async Task ResetParametersAsync()
    {
        await _parameterService.ResetToDefaultsAsync();
    }
}
