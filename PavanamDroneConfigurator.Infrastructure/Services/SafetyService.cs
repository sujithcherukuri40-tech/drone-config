using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Enums;
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

            // Battery Failsafe
            settings.BattMonitor = await GetParameterValueAsync("BATT_MONITOR");
            settings.BattLowVolt = await GetParameterValueAsync("BATT_LOW_VOLT");
            settings.BattCrtVolt = await GetParameterValueAsync("BATT_CRT_VOLT");
            settings.BattFsLowAct = (FailsafeAction)await GetParameterValueAsync("BATT_FS_LOW_ACT");
            settings.BattFsCrtAct = (FailsafeAction)await GetParameterValueAsync("BATT_FS_CRT_ACT");
            settings.BattCapacity = await GetParameterValueAsync("BATT_CAPACITY");

            // RC Failsafe
            settings.FsThrEnable = await GetParameterValueAsync("FS_THR_ENABLE");
            settings.FsThrValue = await GetParameterValueAsync("FS_THR_VALUE");
            settings.FsThrAction = (FailsafeAction)await GetParameterValueAsync("FS_THR_ACTION");

            // GCS Failsafe
            settings.FsGcsEnable = await GetParameterValueAsync("FS_GCS_ENABLE");
            settings.FsGcsTimeout = await GetParameterValueAsync("FS_GCS_TIMEOUT");
            settings.FsGcsAction = (FailsafeAction)await GetParameterValueAsync("FS_GCS_ACTION");

            // Crash / Land Safety
            settings.CrashDetect = await GetParameterValueAsync("CRASH_DETECT");
            settings.CrashAction = (FailsafeAction)await GetParameterValueAsync("CRASH_ACTION");
            settings.LandDetect = await GetParameterValueAsync("LAND_DETECT");

            // Arming Checks
            settings.ArmingCheck = (int)await GetParameterValueAsync("ARMING_CHECK");

            // Geo-Fence
            settings.FenceEnable = await GetParameterValueAsync("FENCE_ENABLE");
            settings.FenceType = await GetParameterValueAsync("FENCE_TYPE");
            settings.FenceAltMax = await GetParameterValueAsync("FENCE_ALT_MAX");
            settings.FenceRadius = await GetParameterValueAsync("FENCE_RADIUS");
            settings.FenceAction = (FailsafeAction)await GetParameterValueAsync("FENCE_ACTION");

            // Motor Safety
            settings.MotSafeDisarm = await GetParameterValueAsync("MOT_SAFE_DISARM");
            settings.MotEmergencyStop = await GetParameterValueAsync("MOT_EMERGENCY_STOP");

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
            
            var failedParameters = new List<string>();
            var successCount = 0;
            var totalParameters = 0;

            // Helper method to set parameter and track result
            async Task<bool> SetAndTrack(string name, float value)
            {
                totalParameters++;
                try
                {
                    var result = await _parameterService.SetParameterAsync(name, value);
                    if (result)
                    {
                        successCount++;
                        _logger.LogInformation("? Set {Name} = {Value}", name, value);
                    }
                    else
                    {
                        failedParameters.Add($"{name} (timeout/no response)");
                        _logger.LogWarning("? Failed to set {Name} = {Value}", name, value);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    failedParameters.Add($"{name} (error: {ex.Message})");
                    _logger.LogError(ex, "Exception setting {Name}", name);
                    return false;
                }
            }

            // Battery Failsafe
            _logger.LogInformation("Updating Battery Failsafe parameters...");
            await SetAndTrack("BATT_MONITOR", settings.BattMonitor);
            await SetAndTrack("BATT_LOW_VOLT", settings.BattLowVolt);
            await SetAndTrack("BATT_CRT_VOLT", settings.BattCrtVolt);
            await SetAndTrack("BATT_FS_LOW_ACT", (float)settings.BattFsLowAct);
            await SetAndTrack("BATT_FS_CRT_ACT", (float)settings.BattFsCrtAct);
            await SetAndTrack("BATT_CAPACITY", settings.BattCapacity);

            // RC Failsafe
            _logger.LogInformation("Updating RC Failsafe parameters...");
            await SetAndTrack("FS_THR_ENABLE", settings.FsThrEnable);
            await SetAndTrack("FS_THR_VALUE", settings.FsThrValue);
            await SetAndTrack("FS_THR_ACTION", (float)settings.FsThrAction);

            // GCS Failsafe
            _logger.LogInformation("Updating GCS Failsafe parameters...");
            await SetAndTrack("FS_GCS_ENABLE", settings.FsGcsEnable);
            await SetAndTrack("FS_GCS_TIMEOUT", settings.FsGcsTimeout);
            await SetAndTrack("FS_GCS_ACTION", (float)settings.FsGcsAction);

            // Crash / Land Safety
            _logger.LogInformation("Updating Crash/Land Detection parameters...");
            await SetAndTrack("CRASH_DETECT", settings.CrashDetect);
            await SetAndTrack("CRASH_ACTION", (float)settings.CrashAction);
            await SetAndTrack("LAND_DETECT", settings.LandDetect);

            // Arming Checks
            _logger.LogInformation("Updating Arming Check parameters...");
            await SetAndTrack("ARMING_CHECK", settings.ArmingCheck);

            // Geo-Fence
            _logger.LogInformation("Updating Geo-Fence parameters...");
            await SetAndTrack("FENCE_ENABLE", settings.FenceEnable);
            await SetAndTrack("FENCE_TYPE", settings.FenceType);
            await SetAndTrack("FENCE_ALT_MAX", settings.FenceAltMax);
            await SetAndTrack("FENCE_RADIUS", settings.FenceRadius);
            await SetAndTrack("FENCE_ACTION", (float)settings.FenceAction);

            // Motor Safety
            _logger.LogInformation("Updating Motor Safety parameters...");
            await SetAndTrack("MOT_SAFE_DISARM", settings.MotSafeDisarm);
            await SetAndTrack("MOT_EMERGENCY_STOP", settings.MotEmergencyStop);

            // Log summary
            _logger.LogInformation("Parameter update summary: {Success}/{Total} succeeded", successCount, totalParameters);
            
            if (failedParameters.Any())
            {
                _logger.LogWarning("Failed parameters: {FailedParams}", string.Join(", ", failedParameters));
                
                // Return false only if more than 50% failed (some parameters might not exist on all vehicle types)
                return successCount >= (totalParameters / 2);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating safety settings");
            return false;
        }
    }

    private async Task<float> GetParameterValueAsync(string name)
    {
        var param = await _parameterService.GetParameterAsync(name);
        return param?.Value ?? 0f;
    }
}
