using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IParameterService
{
    Task<List<DroneParameter>> GetAllParametersAsync();
    Task<DroneParameter?> GetParameterAsync(string name);
    Task<bool> SetParameterAsync(string name, float value);
    Task RefreshParametersAsync();
    event EventHandler? ParameterListRequested;
    event EventHandler<ParameterWriteRequest>? ParameterWriteRequested;
    event EventHandler<ParameterReadRequest>? ParameterReadRequested;
    event EventHandler<string>? ParameterUpdated;
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
