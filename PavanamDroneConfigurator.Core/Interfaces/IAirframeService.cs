using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IAirframeService
{
    Task<AirframeSettings?> GetAirframeSettingsAsync();
    Task<bool> UpdateAirframeSettingsAsync(AirframeSettings settings);
}
