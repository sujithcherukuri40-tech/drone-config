using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.UI.ViewModels;

public class AirframeOption
{
    public string Name { get; }
    public string Category { get; }
    public int FrameClass { get; }
    public int? FrameType { get; }
    public string? Description { get; }

    public AirframeOption(string name, string category, int frameClass, int? frameType, string? description = null)
    {
        Name = name;
        Category = category;
        FrameClass = frameClass;
        FrameType = frameType;
        Description = description;
    }
}

public partial class AirframePageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    private bool _isSyncingSelectionFromParams;
    private bool _showingApplyResult;

    [ObservableProperty]
    private ObservableCollection<AirframeOption> _airframes = new();

    [ObservableProperty]
    private AirframeOption? _selectedAirframe;

    [ObservableProperty]
    private string _statusMessage = "Connect to configure airframe.";

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isPageEnabled;

    public bool IsInteractionEnabled => IsPageEnabled && !IsApplying;
    public bool CanApply => IsInteractionEnabled && SelectedAirframe != null;

    public AirframePageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;

        LoadAirframes();
        UpdateAvailability();
    }

    partial void OnSelectedAirframeChanged(AirframeOption? value)
    {
        OnPropertyChanged(nameof(CanApply));
        if (_isSyncingSelectionFromParams)
        {
            return;
        }

        if (value != null && IsPageEnabled && !IsApplying)
        {
            _showingApplyResult = false;
            StatusMessage = $"Ready to apply {value.Name}";
        }
        else if (value == null && IsPageEnabled && !IsApplying)
        {
            _showingApplyResult = false;
            StatusMessage = "Select an airframe and click Apply.";
        }
    }

    partial void OnIsApplyingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(IsInteractionEnabled));
    }

    partial void OnIsPageEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(IsInteractionEnabled));
    }

    [RelayCommand]
    private async Task ApplyAirframeAsync()
    {
        if (!IsInteractionEnabled)
        {
            StatusMessage = "Airframe selection is disabled until connected with parameters downloaded.";
            return;
        }

        if (SelectedAirframe == null)
        {
            StatusMessage = "Select an airframe to apply.";
            return;
        }

        try
        {
            IsApplying = true;
            StatusMessage = $"Applying {SelectedAirframe.Name}...";

            var frameClassResult = await _parameterService.SetParameterAsync("FRAME_CLASS", SelectedAirframe.FrameClass);
            var frameTypeResult = true;
            if (!frameClassResult)
            {
                frameTypeResult = false;
            }
            else if (SelectedAirframe.FrameType.HasValue)
            {
                frameTypeResult = await _parameterService.SetParameterAsync("FRAME_TYPE", SelectedAirframe.FrameType.Value);
            }

            if (frameClassResult && frameTypeResult)
            {
                StatusMessage = $"Applied {SelectedAirframe.Name} successfully.";
                _showingApplyResult = true;
            }
            else
            {
                StatusMessage = $"Failed to apply {SelectedAirframe.Name}.";
                _showingApplyResult = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying airframe: {ex.Message}";
            _showingApplyResult = true;
        }
        finally
        {
            IsApplying = false;
            UpdateAvailability();
        }
    }

    private void LoadAirframes()
    {
        Airframes = new ObservableCollection<AirframeOption>(new[]
        {
            new AirframeOption("Quad X", "Multicopter", frameClass: 1, frameType: 1, "Four motors in X configuration"),
            new AirframeOption("Quad +", "Multicopter", frameClass: 1, frameType: 0, "Four motors in + configuration"),
            new AirframeOption("Hexa X", "Multicopter", frameClass: 2, frameType: 1, "Six motors in X configuration"),
            new AirframeOption("Plane", "Plane", frameClass: 0, frameType: null, "Conventional fixed-wing layout"),
        });
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(UpdateAvailability);
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateAvailability);
    }

    private void UpdateAvailability()
    {
        var connected = _connectionService.IsConnected;
        var parametersReady = _parameterService.IsParameterDownloadComplete;
        IsPageEnabled = connected && parametersReady;

        if (!connected)
        {
            StatusMessage = "Not connected. Connect to the vehicle to select an airframe.";
            _showingApplyResult = false;
            return;
        }

        if (!parametersReady)
        {
            var expected = _parameterService.ExpectedParameterCount.HasValue
                ? _parameterService.ExpectedParameterCount.Value.ToString()
                : "?";
            StatusMessage = _parameterService.IsParameterDownloadInProgress
                ? $"Waiting for parameters... {_parameterService.ReceivedParameterCount}/{expected}"
                : "Parameter download not complete.";
            _showingApplyResult = false;
            return;
        }

        if (!IsApplying && !_showingApplyResult)
        {
            _ = SyncAirframeFromParametersAsync(forceStatusUpdate: true);
        }
    }

    private async Task SyncAirframeFromParametersAsync(bool forceStatusUpdate = false)
    {
        if (!_connectionService.IsConnected || !_parameterService.IsParameterDownloadComplete || IsApplying)
        {
            return;
        }

        var frameClassParam = await _parameterService.GetParameterAsync("FRAME_CLASS");
        var frameTypeParam = await _parameterService.GetParameterAsync("FRAME_TYPE");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_showingApplyResult && !forceStatusUpdate)
            {
                return;
            }

            if (frameClassParam == null)
            {
                if (!IsApplying)
                {
                    StatusMessage = "Waiting for FRAME_CLASS parameter...";
                }
                if (SelectedAirframe != null)
                {
                    SetSelectedAirframeFromSync(null);
                }
                return;
            }

            if (!TryParseParameterValue(frameClassParam.Value, out var frameClassValue))
            {
                if (!IsApplying)
                {
                    StatusMessage = "FRAME_CLASS parameter is not numeric.";
                }
                if (SelectedAirframe != null)
                {
                    SetSelectedAirframeFromSync(null);
                }
                return;
            }

            int? frameTypeValue = null;
            if (frameTypeParam != null)
            {
                if (TryParseParameterValue(frameTypeParam.Value, out var parsedFrameType))
                {
                    frameTypeValue = parsedFrameType;
                }
                else
                {
                    if (!IsApplying)
                    {
                        StatusMessage = "FRAME_TYPE parameter is not numeric.";
                    }
                    if (SelectedAirframe != null)
                    {
                        SetSelectedAirframeFromSync(null);
                    }
                    return;
                }
            }

            var candidates = Airframes.Where(a => a.FrameClass == frameClassValue);

            var match = FindMatchingAirframe(candidates, frameTypeValue);

            if (match != null)
            {
                var selectionMatches = SelectedAirframe != null &&
                                       SelectedAirframe.FrameClass == match.FrameClass &&
                                       SelectedAirframe.FrameType == match.FrameType &&
                                       SelectedAirframe.Name == match.Name;

                if (!selectionMatches)
                {
                    SetSelectedAirframeFromSync(match);
                }

                if (!IsApplying)
                {
                    StatusMessage = $"Current airframe: {match.Name}";
                }
            }
            else
            {
                if (SelectedAirframe != null)
                {
                    SetSelectedAirframeFromSync(null);
                }

                if (!IsApplying)
                {
                    StatusMessage = frameTypeValue.HasValue
                        ? "Current parameters don't match a known airframe."
                        : "Waiting for FRAME_TYPE parameter...";
                }
            }
        });
    }

    private void SetSelectedAirframeFromSync(AirframeOption? airframe)
    {
        _isSyncingSelectionFromParams = true;
        SelectedAirframe = airframe;
        _isSyncingSelectionFromParams = false;
    }

    private static bool TryParseParameterValue(float value, out int parsed)
    {
        parsed = (int)value;
        return Math.Abs(value - parsed) < float.Epsilon;
    }

    private static AirframeOption? FindMatchingAirframe(IEnumerable<AirframeOption> candidates, int? frameTypeValue)
    {
        var filtered = candidates as IList<AirframeOption> ?? candidates.ToArray();
        if (filtered.Count == 0)
        {
            return null;
        }

        if (frameTypeValue.HasValue)
        {
            var typedMatch = filtered.FirstOrDefault(a => a.FrameType == frameTypeValue);
            if (typedMatch != null)
            {
                return typedMatch;
            }
        }

        var hasFrameTypeValues = filtered.Any(a => a.FrameType.HasValue);
        if (!frameTypeValue.HasValue && !hasFrameTypeValues)
        {
            return filtered.First();
        }

        if (filtered.Count == 1)
        {
            var single = filtered[0];
            if (!single.FrameType.HasValue)
            {
                return single;
            }
        }

        return null;
    }
}
