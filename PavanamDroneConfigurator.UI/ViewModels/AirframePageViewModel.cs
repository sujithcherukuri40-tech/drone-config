using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

public partial class AirframePageViewModel : ViewModelBase, IDisposable
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isSyncing;
    private bool _disposed;
    private int? _lastFrameType;

    private const float FloatTolerance = 0.0001f;

    // ---- Catalog (matches ArduPilot) ----
    private static readonly IReadOnlyList<FrameClassOption> FrameClassCatalog =
    [
        new(0, "Plane"),
        new(1, "Quad"),
        new(2, "Hexa"),
        new(3, "Octa"),
        new(4, "OctaQuad"),
        new(5, "Y6"),
        new(6, "Heli"),
        new(7, "Tri"),
        new(8, "SingleCopter"),
        new(9, "CoaxCopter"),
        new(10, "Twin"),
        new(11, "Heli Dual"),
        new(12, "DodecaHexa"),
        new(13, "Y4"),
        new(14, "Deca")
    ];

    private static readonly IReadOnlyList<FrameTypeOption> FrameTypeCatalog =
    [
        new(0, "Plus"),
        new(1, "X"),
        new(2, "V"),
        new(3, "H"),
        new(4, "V-Tail")
    ];

    // ---- UI State ----
    [ObservableProperty] private ObservableCollection<FrameClassOption> _frameClasses = new(FrameClassCatalog);
    [ObservableProperty] private ObservableCollection<FrameTypeOption> _frameTypes = new();
    [ObservableProperty] private FrameClassOption? _selectedFrameClass;
    [ObservableProperty] private FrameTypeOption? _selectedFrameType;
    [ObservableProperty] private string _statusMessage = "Connect to vehicle.";
    [ObservableProperty] private bool _isApplying;
    [ObservableProperty] private bool _isPageEnabled;

    /// <summary>
    /// Indicates whether the Frame Class dropdown should be enabled for user interaction.
    /// True when page is enabled and not currently applying changes.
    /// </summary>
    public bool IsInteractionEnabled => IsPageEnabled && !IsApplying;

    /// <summary>
    /// Indicates whether the Frame Type dropdown should be enabled for user interaction.
    /// True when IsInteractionEnabled and FrameTypes collection has options.
    /// FrameTypes is only populated when FRAME_CLASS has a value other than 0 (Plane),
    /// since planes don't have a frame type parameter.
    /// </summary>
    public bool IsFrameTypeEnabled => IsInteractionEnabled && FrameTypes.Count > 0;

    public bool CanUpdate =>
        IsPageEnabled &&
        !IsApplying &&
        SelectedFrameClass != null &&
        (_lastFrameType == null || SelectedFrameType != null);

    // ---- ctor ----
    public AirframePageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterUpdated += OnParameterUpdated;
        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;
        _parameterService.ParameterDownloadCompleted += OnParameterDownloadCompleted;

        UpdateAvailability();
    }

    // ---- Event handlers ----

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(UpdateAvailability);
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        if (_parameterService.IsParameterDownloadComplete)
        {
            Dispatcher.UIThread.Post(() => _ = SyncFromCacheAsync(true));
        }
        else if (_parameterService.IsParameterDownloadInProgress)
        {
            var expected = _parameterService.ExpectedParameterCount;
            var expectedStr = expected.HasValue ? expected.Value.ToString() : "?";
            Dispatcher.UIThread.Post(() => StatusMessage = $"Downloading parameters… {_parameterService.ReceivedParameterCount}/{expectedStr}");
        }
    }

    private void OnParameterUpdated(object? sender, string paramName)
    {
        if (paramName.Equals("FRAME_CLASS", StringComparison.OrdinalIgnoreCase) ||
            paramName.Equals("FRAME_TYPE", StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.UIThread.Post(() => _ = SyncFromCacheAsync(false));
        }
    }

    /// <summary>
    /// Authoritative handler for parameter download completion.
    /// When the full parameter set is downloaded successfully, immediately sync from cache.
    /// </summary>
    private void OnParameterDownloadCompleted(object? sender, bool success)
    {
        if (success && _parameterService.IsParameterDownloadComplete)
        {
            Dispatcher.UIThread.Post(() => _ = SyncFromCacheAsync(true));
        }
    }

    // ---- Core sync logic ----

    private async Task SyncFromCacheAsync(bool forceStatus)
    {
        if (!_connectionService.IsConnected || !_parameterService.IsParameterDownloadComplete)
            return;

        await _syncLock.WaitAsync();
        try
        {
            _isSyncing = true;

            var classParam = await _parameterService.GetParameterAsync("FRAME_CLASS");
            var typeParam = await _parameterService.GetParameterAsync("FRAME_TYPE");

            var classValue = ParseInt(classParam);
            var typeValue = ParseInt(typeParam);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Frame Class
                SelectedFrameClass = EnsureFrameClass(classValue);

                // Frame Types (only if applicable)
                FrameTypes.Clear();
                if (classValue.HasValue && classValue.Value != 0)
                {
                    foreach (var t in FrameTypeCatalog)
                        FrameTypes.Add(t);

                    SelectedFrameType = EnsureFrameType(typeValue);
                }
                else
                {
                    SelectedFrameType = null;
                }

                _lastFrameType = typeValue;

                IsPageEnabled = true;
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(IsInteractionEnabled));
                OnPropertyChanged(nameof(IsFrameTypeEnabled));

                // PDRL-style status messages
                if (forceStatus)
                {
                    if (SelectedFrameClass != null)
                    {
                        StatusMessage = SelectedFrameType != null
                            ? $"Current airframe: {SelectedFrameClass.DisplayName} {SelectedFrameType.DisplayName}"
                            : $"Current airframe: {SelectedFrameClass.DisplayName}";
                    }
                    else if (classParam == null)
                    {
                        StatusMessage = "FRAME_CLASS not available in cache.";
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            _isSyncing = false;
            _syncLock.Release();
        }
    }

    // ---- Apply ----

    [RelayCommand]
    private async Task UpdateFrameAsync()
    {
        if (!CanUpdate)
            return;

        try
        {
            IsApplying = true;
            OnPropertyChanged(nameof(IsInteractionEnabled));
            OnPropertyChanged(nameof(IsFrameTypeEnabled));
            StatusMessage = "Applying airframe…";

            if (!await _parameterService.SetParameterAsync("FRAME_CLASS", SelectedFrameClass!.Value))
            {
                StatusMessage = "FRAME_CLASS write failed.";
                return;
            }

            if (SelectedFrameType != null)
            {
                if (!await _parameterService.SetParameterAsync("FRAME_TYPE", SelectedFrameType.Value))
                {
                    StatusMessage = "FRAME_TYPE write failed.";
                    return;
                }
            }

            StatusMessage = "Airframe updated successfully.";
        }
        finally
        {
            IsApplying = false;
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(IsInteractionEnabled));
            OnPropertyChanged(nameof(IsFrameTypeEnabled));
        }
    }

    // ---- Helpers ----

    private static int? ParseInt(DroneParameter? p)
    {
        if (p == null) return null;
        var rounded = (int)Math.Round(p.Value);
        return Math.Abs(p.Value - rounded) < FloatTolerance ? rounded : null;
    }

    private FrameClassOption? EnsureFrameClass(int? value)
    {
        if (!value.HasValue) return null;
        return FrameClasses.FirstOrDefault(f => f.Value == value)
               ?? new FrameClassOption(value.Value, $"Unknown ({value.Value})");
    }

    private FrameTypeOption? EnsureFrameType(int? value)
    {
        if (!value.HasValue) return null;
        return FrameTypes.FirstOrDefault(t => t.Value == value)
               ?? new FrameTypeOption(value.Value, $"Unknown ({value.Value})");
    }

    private void UpdateAvailability()
    {
        if (!_connectionService.IsConnected)
        {
            IsPageEnabled = false;
            StatusMessage = "Connect to vehicle.";
            OnPropertyChanged(nameof(IsInteractionEnabled));
            OnPropertyChanged(nameof(IsFrameTypeEnabled));
            return;
        }

        if (_parameterService.IsParameterDownloadComplete)
        {
            _ = SyncFromCacheAsync(true);
        }
        else if (_parameterService.IsParameterDownloadInProgress)
        {
            var expected = _parameterService.ExpectedParameterCount;
            var expectedStr = expected.HasValue ? expected.Value.ToString() : "?";
            StatusMessage = $"Downloading parameters… {_parameterService.ReceivedParameterCount}/{expectedStr}";
        }
        else
        {
            StatusMessage = "Waiting for parameter download…";
        }
    }

    // ---- Cleanup ----

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        _parameterService.ParameterUpdated -= OnParameterUpdated;
        _parameterService.ParameterDownloadProgressChanged -= OnParameterDownloadProgressChanged;
        _parameterService.ParameterDownloadCompleted -= OnParameterDownloadCompleted;
    }
}