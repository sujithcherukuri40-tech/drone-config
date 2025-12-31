using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Infrastructure.Services;

public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly Dictionary<string, DroneParameter> _parameters = new();

    public ParameterService(ILogger<ParameterService> logger)
    {
        _logger = logger;
        InitializeSampleParameters();
    }

    private void InitializeSampleParameters()
    {
        _parameters["FRAME_TYPE"] = new DroneParameter { Name = "FRAME_TYPE", Value = 1, Description = "Frame type" };
        _parameters["BATT_CAPACITY"] = new DroneParameter { Name = "BATT_CAPACITY", Value = 5200, Description = "Battery capacity (mAh)" };
        _parameters["RTL_ALT"] = new DroneParameter { Name = "RTL_ALT", Value = 1500, Description = "RTL altitude (cm)" };
    }

    public Task<List<DroneParameter>> GetAllParametersAsync()
    {
        _logger.LogInformation("Getting all parameters");
        return Task.FromResult(_parameters.Values.ToList());
    }

    public Task<DroneParameter?> GetParameterAsync(string name)
    {
        _logger.LogInformation("Getting parameter: {Name}", name);
        _parameters.TryGetValue(name, out var param);
        return Task.FromResult(param);
    }

    public Task<bool> SetParameterAsync(string name, float value)
    {
        _logger.LogInformation("Setting parameter {Name} = {Value}", name, value);

        if (_parameters.ContainsKey(name))
        {
            _parameters[name].Value = value;
        }
        else
        {
            _parameters[name] = new DroneParameter { Name = name, Value = value };
        }

        return Task.FromResult(true);
    }
}
