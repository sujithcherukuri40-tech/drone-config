using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Services.Interfaces;

public interface IParameterService
{
    Task<Dictionary<string, DroneParameter>> ReadAllParametersAsync();
    Task<DroneParameter?> ReadParameterAsync(string name);
    Task<bool> WriteParameterAsync(string name, float value);
    Task<bool> ResetToDefaultsAsync();
    
    IObservable<ParameterProgress> DownloadProgress { get; }
}

public class ParameterProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string? CurrentParameter { get; set; }
}
