using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using PavamanDroneConfigurator.Infrastructure.Services;
using PavamanDroneConfigurator.ViewModels;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace PavamanDroneConfigurator;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core Services (order matters for dependencies)
        services.AddSingleton<IMavlinkService, MavlinkService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IParameterService, ParameterService>();
        services.AddSingleton<ICalibrationService, CalibrationService>(); // Depends on IMavlinkService
        services.AddSingleton<IMotorTestService, MotorTestService>(); // Depends on IMavlinkService
        services.AddSingleton<IArmingService, ArmingService>(); // Depends on IMavlinkService
        services.AddSingleton<IFlightModeService, FlightModeService>(); // Depends on IMavlinkService

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionViewModel>();
        services.AddTransient<SensorsViewModel>();
        services.AddTransient<SafetyViewModel>();
        services.AddTransient<FlightModesViewModel>();
        services.AddTransient<RcCalibrationViewModel>();
        services.AddTransient<MotorEscViewModel>();
        services.AddTransient<PowerViewModel>();
        services.AddTransient<SprayingConfigViewModel>();
        services.AddTransient<PidTuningViewModel>();
        services.AddTransient<ParametersViewModel>();
    }
}
