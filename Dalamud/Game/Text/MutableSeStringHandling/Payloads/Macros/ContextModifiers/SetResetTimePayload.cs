using Dalamud.Game.Text.MutableSeStringHandling.Expressions;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets the contextual time storage value to the calculated reset time.</summary>
public sealed class SetResetTimePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="SetResetTimePayload"/> class.</summary>
    public SetResetTimePayload()
        : base(1, 2, (int)Lumina.Text.Payloads.MacroCode.SetResetTime)
    {
    }

    /// <summary>Gets or sets the hour expression.</summary>
    public IMutableSeExpression? Hour
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the optional weekday expression.</summary>
    public IMutableSeExpression? Weekday
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }

    /// <summary>Sets <see cref="Hour"/> with the given integer value.</summary>
    /// <param name="hourValue">The integer value.</param>
    public void SetHour(int hourValue) => this.Hour = new IntegerSeExpression(hourValue);

    /// <summary>Sets <see cref="Weekday"/> with the given integer value.</summary>
    /// <param name="weekdayValue">The integer value.</param>
    public void SetWeekday(int weekdayValue) => this.Weekday = new IntegerSeExpression(weekdayValue);

    /// <summary>Clears <see cref="Weekday"/>.</summary>
    public void ClearWeekday() => this.Weekday = null;

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb) =>
        this.UpdateContext(context);

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        this.UpdateContext(context);
        bytesWritten = 0;
        return true;
    }

    private void UpdateContext(ISeStringEvaluationContext context)
    {
        var hour = this.Hour?.EvaluateAsInt(context) ?? 0;
        var t = DateTime.UtcNow;
        if (this.Weekday is not null)
            t = t.AddDays(((this.Weekday.EvaluateAsInt(context) - (int)DateTime.UtcNow.DayOfWeek) + 7) % 7);
        context.ContextualTime = new DateTime(t.Year, t.Month, t.Day, hour, 0, 0, DateTimeKind.Utc).ToLocalTime();
    }
}
