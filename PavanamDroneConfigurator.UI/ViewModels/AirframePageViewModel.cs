using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.UI.ViewModels;

public class FrameClassOption
{
    public int Value { get; }
    public string DisplayName { get; }

    public FrameClassOption(int value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}

public class FrameTypeOption
{
    public int Value { get; }
    public string DisplayName { get; }

    public FrameTypeOption(int value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }
}

public partial class AirframePageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    private bool _isSyncingFromParameters;
    private int? _lastFrameTypeValue;

    private static readonly IReadOnlyList<FrameClassOption> FrameClassCatalog = new List<FrameClassOption>
    {
        new(0, "Quad"),
        new(1, "Hexa"),
        new(2, "Octa"),
        new(3, "OctaQuad"),
        new(4, "Y6"),
        new(5, "Heli"),
        new(6, "Tri"),
        new(7, "SingleCopter"),
        new(8, "CoaxCopter"),
        new(9, "Twin"),
        new(10, "Heli Dual"),
        new(11, "DodecaHexa"),
        new(12, "Y4"),
        new(13, "Deca"),
    };

    private static readonly IReadOnlyList<FrameTypeOption> FrameTypeCatalog = new List<FrameTypeOption>
    {
        new(0, "Plus"),
        new(1, "X"),
        new(2, "V"),
        new(3, "H"),
        new(4, "V-Tail"),
    };

    [ObservableProperty]
    private ObservableCollection<FrameClassOption> _frameClasses = new(FrameClassCatalog);

    [ObservableProperty]
    private ObservableCollection<FrameTypeOption> _frameTypes = new(FrameTypeCatalog);

    [ObservableProperty]
    private FrameClassOption? _selectedFrameClass;

    [ObservableProperty]
    private FrameTypeOption? _selectedFrameType;

    [ObservableProperty]
    private string _statusMessage = "Connect and download parameters to configure frame.";

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isPageEnabled;

    public bool IsInteractionEnabled => IsPageEnabled && !IsApplying;
    public bool IsFrameTypeEnabled => IsInteractionEnabled && FrameTypes.Count > 0;
    public bool CanUpdate => IsInteractionEnabled && SelectedFrameClass != null && (!FrameTypes.Any() || SelectedFrameType != null);

    public AirframePageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;

        UpdateAvailability();
    }

    partial void OnSelectedFrameClassChanged(FrameClassOption? value)
    {
        OnPropertyChanged(nameof(CanUpdate));
        var preferredType = _isSyncingFromParameters ? _lastFrameTypeValue : null;
        BuildFrameTypeOptions(preferredType);

        if (_isSyncingFromParameters)
        {
            return;
        }

        SelectedFrameType = null;

        if (IsInteractionEnabled)
        {
            StatusMessage = value != null
                ? $"Frame class selected: {value.DisplayName} ({value.Value})."
                : "Select a frame class.";
        }
    }

    partial void OnSelectedFrameTypeChanged(FrameTypeOption? value)
    {
        OnPropertyChanged(nameof(CanUpdate));

        if (_isSyncingFromParameters)
        {
            return;
        }

        if (IsInteractionEnabled && value != null)
        {
            StatusMessage = $"Frame type selected: {value.DisplayName} ({value.Value}).";
        }
    }

    partial void OnIsApplyingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(IsInteractionEnabled));
        OnPropertyChanged(nameof(IsFrameTypeEnabled));
    }

    partial void OnIsPageEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(IsInteractionEnabled));
        OnPropertyChanged(nameof(IsFrameTypeEnabled));
    }

    partial void OnFrameTypesChanged(ObservableCollection<FrameTypeOption> value)
    {
        OnPropertyChanged(nameof(IsFrameTypeEnabled));
        OnPropertyChanged(nameof(CanUpdate));
    }

    [RelayCommand]
    private async Task UpdateFrameAsync()
    {
        if (!IsInteractionEnabled)
        {
            StatusMessage = "Frame updates require connection and downloaded parameters.";
            return;
        }

        if (SelectedFrameClass == null)
        {
            StatusMessage = "Select a frame class before updating.";
            return;
        }

        try
        {
            IsApplying = true;
            StatusMessage = $"Writing FRAME_CLASS = {SelectedFrameClass.Value}...";
            var frameClassResult = await _parameterService.SetParameterAsync("FRAME_CLASS", SelectedFrameClass.Value);

            if (!frameClassResult)
            {
                StatusMessage = "FRAME_CLASS was not confirmed. No changes applied.";
                return;
            }

            var frameTypeResult = true;
            if (SelectedFrameType != null)
            {
                StatusMessage = $"FRAME_CLASS confirmed. Writing FRAME_TYPE = {SelectedFrameType.Value}...";
                frameTypeResult = await _parameterService.SetParameterAsync("FRAME_TYPE", SelectedFrameType.Value);
            }

            if (frameClassResult && frameTypeResult)
            {
                StatusMessage = "Frame parameters updated after confirmation.";
            }
            else
            {
                StatusMessage = "FRAME_TYPE was not confirmed. Frame update incomplete.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating frame: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
            await SyncFromParametersAsync(forceStatusUpdate: true);
        }
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
            StatusMessage = "Connect to a vehicle to edit FRAME_CLASS and FRAME_TYPE.";
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
            return;
        }

        if (!IsApplying)
        {
            _ = SyncFromParametersAsync(forceStatusUpdate: false);
        }
    }

    private async Task SyncFromParametersAsync(bool forceStatusUpdate)
    {
        if (!_connectionService.IsConnected || !_parameterService.IsParameterDownloadComplete || IsApplying)
        {
            return;
        }

        var frameClassParam = await _parameterService.GetParameterAsync("FRAME_CLASS");
        var frameTypeParam = await _parameterService.GetParameterAsync("FRAME_TYPE");

        var frameClassValue = TryParseParameterValue(frameClassParam);
        var frameTypeValue = TryParseParameterValue(frameTypeParam);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _isSyncingFromParameters = true;
            _lastFrameTypeValue = frameTypeValue;

            var selectedClass = EnsureFrameClassOption(frameClassValue);
            SelectedFrameClass = selectedClass;

            BuildFrameTypeOptions(frameTypeValue);
            SelectedFrameType = frameTypeValue.HasValue
                ? FrameTypes.FirstOrDefault(t => t.Value == frameTypeValue.Value)
                : null;

            if (forceStatusUpdate || !IsApplying)
            {
                if (frameClassValue.HasValue)
                {
                    var typeText = frameTypeValue.HasValue ? frameTypeValue.Value.ToString() : "unset";
                    StatusMessage = $"FRAME_CLASS={frameClassValue.Value}, FRAME_TYPE={typeText}.";
                }
                else
                {
                    StatusMessage = "FRAME_CLASS not available in cache.";
                }
            }

            _isSyncingFromParameters = false;
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(IsFrameTypeEnabled));
        });
    }

    private void BuildFrameTypeOptions(int? currentTypeValue)
    {
        var options = new List<FrameTypeOption>(FrameTypeCatalog);

        if (currentTypeValue.HasValue && options.All(o => o.Value != currentTypeValue.Value))
        {
            options.Add(new FrameTypeOption(currentTypeValue.Value, $"Unknown ({currentTypeValue.Value})"));
        }

        FrameTypes = new ObservableCollection<FrameTypeOption>(options);
    }

    private FrameClassOption? EnsureFrameClassOption(int? frameClassValue)
    {
        if (!frameClassValue.HasValue)
        {
            return null;
        }

        var existing = FrameClasses.FirstOrDefault(c => c.Value == frameClassValue.Value);
        if (existing != null)
        {
            return existing;
        }

        var option = new FrameClassOption(frameClassValue.Value, $"Unknown ({frameClassValue.Value})");
        FrameClasses.Add(option);
        return option;
    }

    private static int? TryParseParameterValue(DroneParameter? parameter)
    {
        if (parameter == null)
        {
            return null;
        }

        var parsed = (int)parameter.Value;
        return Math.Abs(parameter.Value - parsed) < float.Epsilon ? parsed : null;
    }
}
