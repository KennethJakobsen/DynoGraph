using RollerGraph.Core.Models;

namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Applies per-channel adjustments (Speed, NM, HP) to a <see cref="Sample"/>.
/// Compiled once at construction for runtime efficiency.
/// </summary>
public sealed class SampleAdjuster
{
    public static readonly SampleAdjuster Identity = new(
        ChannelAdjustment.Identity, ChannelAdjustment.Identity, ChannelAdjustment.Identity);

    private readonly Func<double, double> _speed;
    private readonly Func<double, double> _nm;
    private readonly Func<double, double> _hp;

    public SampleAdjuster(ChannelAdjustment speed, ChannelAdjustment nm, ChannelAdjustment hp)
    {
        ArgumentNullException.ThrowIfNull(speed);
        ArgumentNullException.ThrowIfNull(nm);
        ArgumentNullException.ThrowIfNull(hp);
        _speed = speed.Compile();
        _nm = nm.Compile();
        _hp = hp.Compile();
    }

    /// <summary>
    /// Builds a <see cref="SampleAdjuster"/> from the three channel adjustments
    /// on <paramref name="settings"/>. If any expression fails to compile
    /// (e.g. a bad expression persisted in settings), returns
    /// <see cref="Identity"/> instead of throwing - so a corrupt settings file
    /// can never crash the app.
    /// </summary>
    public static SampleAdjuster FromSettingsOrIdentity(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            return new SampleAdjuster(
                settings.SpeedAdjustment,
                settings.NmAdjustment,
                settings.HpAdjustment);
        }
        catch (ExpressionException)
        {
            return Identity;
        }
    }

    /// <summary>Returns a new <see cref="Sample"/> with adjustments applied.</summary>
    public Sample Adjust(Sample s) => s with
    {
        SpeedKmh = _speed(s.SpeedKmh),
        Nm = _nm(s.Nm),
        Hp = _hp(s.Hp),
    };
}
