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
    public AirframePageViewModel AirframePage { get; }
    public ProfilePageViewModel ProfilePage { get; }

    public MainWindowViewModel(
        ConnectionPageViewModel connectionPage,
        TelemetryPageViewModel telemetryPage,
        ParametersPageViewModel parametersPage,
        CalibrationPageViewModel calibrationPage,
        SafetyPageViewModel safetyPage,
        AirframePageViewModel airframePage,
        ProfilePageViewModel profilePage)
    {
        ConnectionPage = connectionPage;
        TelemetryPage = telemetryPage;
        ParametersPage = parametersPage;
        CalibrationPage = calibrationPage;
        SafetyPage = safetyPage;
        AirframePage = airframePage;
        ProfilePage = profilePage;

        _currentPage = connectionPage;
    }
}
