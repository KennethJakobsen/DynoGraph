using CommunityToolkit.Mvvm.ComponentModel;
using RollerGraph.Core.Models;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Editable view-model backing the settings dialog. <see cref="ToSettings"/>
/// returns a new immutable <see cref="Settings"/> record.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private int _baudRate;

    [ObservableProperty]
    private double _minSpeedKmh;

    [ObservableProperty]
    private int _smoothingWindow;

    [ObservableProperty]
    private double _defaultHpMax;

    [ObservableProperty]
    private double _defaultNmMax;

    [ObservableProperty]
    private double _defaultSpeedMax;

    public ChannelAdjustmentViewModel SpeedAdjustment { get; }
    public ChannelAdjustmentViewModel NmAdjustment { get; }
    public ChannelAdjustmentViewModel HpAdjustment { get; }

    public SettingsViewModel() : this(new Settings())
    {
    }

    public SettingsViewModel(Settings source)
    {
        _baudRate = source.BaudRate;
        _minSpeedKmh = source.MinSpeedKmh;
        _smoothingWindow = source.SmoothingWindow;
        _defaultHpMax = source.DefaultHpMax;
        _defaultNmMax = source.DefaultNmMax;
        _defaultSpeedMax = source.DefaultSpeedMax;
        SpeedAdjustment = new ChannelAdjustmentViewModel("Speed", "km/h", source.SpeedAdjustment);
        NmAdjustment = new ChannelAdjustmentViewModel("NM", "Nm", source.NmAdjustment);
        HpAdjustment = new ChannelAdjustmentViewModel("HP", "HP", source.HpAdjustment);
    }

    /// <summary>
    /// Builds an immutable <see cref="Settings"/> from the current edits.
    /// Values are clamped to safe ranges.
    /// </summary>
    public Settings ToSettings()
    {
        return new Settings
        {
            BaudRate = BaudRate <= 0 ? 19200 : BaudRate,
            MinSpeedKmh = MinSpeedKmh < 0 ? 0 : MinSpeedKmh,
            SmoothingWindow = SmoothingWindow < 1 ? 1 : SmoothingWindow,
            DefaultHpMax = DefaultHpMax <= 0 ? 10 : DefaultHpMax,
            DefaultNmMax = DefaultNmMax <= 0 ? 10 : DefaultNmMax,
            DefaultSpeedMax = DefaultSpeedMax <= 0 ? 50 : DefaultSpeedMax,
            SpeedAdjustment = SpeedAdjustment.ToAdjustment(),
            NmAdjustment = NmAdjustment.ToAdjustment(),
            HpAdjustment = HpAdjustment.ToAdjustment(),
        };
    }
}
