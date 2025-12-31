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

    public async Task RefreshParametersAsync()
    {
        _logger.LogInformation("Refreshing parameters from drone");
        
        // Clear existing parameters
        _parameters.Clear();
        
        // Simulate loading parameters from drone
        await Task.Delay(500);
        
        // Load sample parameters (simulating MAVLink parameter list)
        _parameters["FRAME_TYPE"] = new DroneParameter { Name = "FRAME_TYPE", Value = 1, Description = "Frame type" };
        _parameters["BATT_CAPACITY"] = new DroneParameter { Name = "BATT_CAPACITY", Value = 5200, Description = "Battery capacity (mAh)" };
        _parameters["RTL_ALT"] = new DroneParameter { Name = "RTL_ALT", Value = 1500, Description = "RTL altitude (cm)" };
        _parameters["WPNAV_SPEED"] = new DroneParameter { Name = "WPNAV_SPEED", Value = 500, Description = "Waypoint navigation speed (cm/s)" };
        _parameters["PILOT_SPEED_UP"] = new DroneParameter { Name = "PILOT_SPEED_UP", Value = 250, Description = "Pilot vertical speed up (cm/s)" };
        _parameters["PILOT_SPEED_DN"] = new DroneParameter { Name = "PILOT_SPEED_DN", Value = 150, Description = "Pilot vertical speed down (cm/s)" };
        _parameters["ANGLE_MAX"] = new DroneParameter { Name = "ANGLE_MAX", Value = 4500, Description = "Maximum lean angle (centidegrees)" };
        _parameters["PSC_ACCZ_P"] = new DroneParameter { Name = "PSC_ACCZ_P", Value = 0.5f, Description = "Position control Z axis P gain" };
        _parameters["PSC_ACCZ_I"] = new DroneParameter { Name = "PSC_ACCZ_I", Value = 1.0f, Description = "Position control Z axis I gain" };
        _parameters["ATC_RAT_RLL_P"] = new DroneParameter { Name = "ATC_RAT_RLL_P", Value = 0.135f, Description = "Roll rate P gain" };
        
        _logger.LogInformation("Loaded {Count} parameters", _parameters.Count);
    }
}
