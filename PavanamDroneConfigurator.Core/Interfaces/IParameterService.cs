using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IParameterService
{
    Task<List<DroneParameter>> GetAllParametersAsync();
    Task<DroneParameter?> GetParameterAsync(string name);
    Task<bool> SetParameterAsync(string name, float value);
}
