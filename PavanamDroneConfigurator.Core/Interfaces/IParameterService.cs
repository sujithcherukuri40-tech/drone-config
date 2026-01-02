using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IParameterService
{
    Task<List<DroneParameter>> GetAllParametersAsync();
    Task<DroneParameter?> GetParameterAsync(string name);
    Task<DroneParameter?> GetParameterAsync(string name, bool forceRefresh);
    Task<bool> SetParameterAsync(string name, float value);
    Task RefreshParametersAsync();
    
    // Method to be called when PARAM_VALUE messages are received
    void OnParameterValueReceived(string name, float value, int index, int count);
    
    // Event fired when a parameter is updated
    event EventHandler<DroneParameter>? ParameterUpdated;
}
