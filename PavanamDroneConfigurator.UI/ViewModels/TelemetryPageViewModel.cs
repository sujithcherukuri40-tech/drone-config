using CommunityToolkit.Mvvm.ComponentModel;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class TelemetryPageViewModel : ViewModelBase
{
    private readonly ITelemetryService _telemetryService;

    [ObservableProperty]
    private TelemetryData? _currentTelemetry;

    public TelemetryPageViewModel(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;

        _telemetryService.TelemetryUpdated += (s, telemetry) =>
        {
            CurrentTelemetry = telemetry;
        };
    }
}
