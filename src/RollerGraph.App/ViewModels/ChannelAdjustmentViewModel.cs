using CommunityToolkit.Mvvm.ComponentModel;
using RollerGraph.Core.Adjustments;

namespace RollerGraph.App.ViewModels;

/// <summary>
/// Editable wrapper around a <see cref="ChannelAdjustment"/>. Exposed in the
/// settings dialog so the user can tweak per-channel factor / offset and/or
/// supply a math expression.
/// </summary>
public sealed partial class ChannelAdjustmentViewModel : ObservableObject
{
    [ObservableProperty]
    private double _factor;

    [ObservableProperty]
    private double _offset;

    [ObservableProperty]
    private string? _expression;

    [ObservableProperty]
    private string? _expressionError;

    public string ChannelName { get; }
    public string Unit { get; }

    public ChannelAdjustmentViewModel(string channelName, string unit) : this(channelName, unit, ChannelAdjustment.Identity)
    {
    }

    public ChannelAdjustmentViewModel(string channelName, string unit, ChannelAdjustment source)
    {
        ChannelName = channelName;
        Unit = unit;
        _factor = source.Factor;
        _offset = source.Offset;
        _expression = source.Expression;
    }

    /// <summary>
    /// Builds an immutable <see cref="ChannelAdjustment"/>. If the expression
    /// is non-empty and invalid, <see cref="ExpressionError"/> is set and
    /// the linear factor/offset form is returned instead.
    /// </summary>
    public ChannelAdjustment ToAdjustment()
    {
        ExpressionError = null;
        if (!string.IsNullOrWhiteSpace(Expression))
        {
            var err = ExpressionParser.Validate(Expression);
            if (err is not null)
            {
                ExpressionError = err;
                // Fall back to linear so partial typos don't nuke the user's data.
                return new ChannelAdjustment { Factor = Factor, Offset = Offset, Expression = null };
            }
            return new ChannelAdjustment { Factor = Factor, Offset = Offset, Expression = Expression };
        }
        return new ChannelAdjustment
        {
            Factor = Factor == 0 ? 1.0 : Factor,
            Offset = Offset,
            Expression = null,
        };
    }

    partial void OnExpressionChanged(string? value)
    {
        ExpressionError = ExpressionParser.Validate(value);
    }
}
