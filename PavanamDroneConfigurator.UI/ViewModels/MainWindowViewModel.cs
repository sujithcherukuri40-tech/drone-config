using CommunityToolkit.Mvvm.ComponentModel;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public ConnectionPageViewModel ConnectionPage { get; }
    public TelemetryPageViewModel TelemetryPage { get; }
    public ParametersPageViewModel ParametersPage { get; }
    public CalibrationPageViewModel CalibrationPage { get; }
    public SafetyPageViewModel SafetyPage { get; }
    public ProfilePageViewModel ProfilePage { get; }

    public MainWindowViewModel(
        ConnectionPageViewModel connectionPage,
        TelemetryPageViewModel telemetryPage,
        ParametersPageViewModel parametersPage,
        CalibrationPageViewModel calibrationPage,
        SafetyPageViewModel safetyPage,
        ProfilePageViewModel profilePage)
    {
        ConnectionPage = connectionPage;
        TelemetryPage = telemetryPage;
        ParametersPage = parametersPage;
        CalibrationPage = calibrationPage;
        SafetyPage = safetyPage;
        ProfilePage = profilePage;

        _currentPage = connectionPage;
    }
}
