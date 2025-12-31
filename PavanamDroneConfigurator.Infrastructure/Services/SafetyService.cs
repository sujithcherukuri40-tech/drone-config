using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class SafetyService : ISafetyService
{
    private readonly IParameterService _parameterService;
    private readonly ILogger<SafetyService> _logger;

    public SafetyService(
        IParameterService parameterService,
        ILogger<SafetyService> logger)
    {
        _parameterService = parameterService;
        _logger = logger;
    }

    public async Task<SafetySettings?> GetSafetySettingsAsync()
    {
        try
        {
            _logger.LogInformation("Getting safety settings");

            var settings = new SafetySettings();

            var battLowVolt = await _parameterService.GetParameterAsync("BATT_LOW_VOLT");
            if (battLowVolt != null)
                settings.BatteryLowVoltage = battLowVolt.Value;

            var battCritVolt = await _parameterService.GetParameterAsync("BATT_CRT_VOLT");
            if (battCritVolt != null)
                settings.BatteryCriticalVoltage = battCritVolt.Value;

            var rtlAlt = await _parameterService.GetParameterAsync("RTL_ALT");
            if (rtlAlt != null)
                settings.ReturnToLaunchAltitude = rtlAlt.Value / 100.0;

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting safety settings");
            return null;
        }
    }

    public async Task<bool> UpdateSafetySettingsAsync(SafetySettings settings)
    {
        try
        {
            _logger.LogInformation("Updating safety settings");

            await _parameterService.SetParameterAsync("BATT_LOW_VOLT", (float)settings.BatteryLowVoltage);
            await _parameterService.SetParameterAsync("BATT_CRT_VOLT", (float)settings.BatteryCriticalVoltage);
            await _parameterService.SetParameterAsync("RTL_ALT", (float)(settings.ReturnToLaunchAltitude * 100));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating safety settings");
            return false;
        }
    }
}
