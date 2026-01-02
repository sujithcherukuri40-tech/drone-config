using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IParameterService
{
    Task<List<DroneParameter>> GetAllParametersAsync();
    Task<DroneParameter?> GetParameterAsync(string name);
    Task<bool> SetParameterAsync(string name, float value);
    Task RefreshParametersAsync();
    
    // Method to be called when PARAM_VALUE messages are received
    void OnParameterValueReceived(string name, float value, int index, int count);
    
    // Event fired when a parameter is updated (provides parameter name)
    event EventHandler<string>? ParameterUpdated;
    event EventHandler? ParameterListRequested;
    event EventHandler<ParameterWriteRequest>? ParameterWriteRequested;
    event EventHandler<ParameterReadRequest>? ParameterReadRequested;
    event EventHandler? ParameterDownloadStarted;
    event EventHandler<bool>? ParameterDownloadCompleted;
    bool IsParameterDownloadInProgress { get; }
    bool IsParameterDownloadComplete { get; }
    int ReceivedParameterCount { get; }
    int? ExpectedParameterCount { get; }
    event EventHandler? ParameterDownloadProgressChanged;
    void HandleParamValue(DroneParameter parameter, ushort paramIndex, ushort paramCount);
    void Reset();
}
