using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface ISafetyService
{
    Task<SafetySettings?> GetSafetySettingsAsync();
    Task<bool> UpdateSafetySettingsAsync(SafetySettings settings);
}
