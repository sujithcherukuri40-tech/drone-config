using PavanamDroneConfigurator.Core.Enums;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface ICalibrationService
{
    Task<bool> StartCalibrationAsync(CalibrationType type);
    Task<bool> CancelCalibrationAsync();
    CalibrationStateModel? CurrentState { get; }
    event EventHandler<CalibrationStateModel>? CalibrationStateChanged;
}
