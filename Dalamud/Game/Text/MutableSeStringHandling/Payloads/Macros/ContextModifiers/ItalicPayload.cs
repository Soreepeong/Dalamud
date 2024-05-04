using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Italicizes any following text.</summary>
public sealed class ItalicPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="ItalicPayload"/> class.</summary>
    public ItalicPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Italic)
    {
    }

    /// <summary>Gets or sets an expression that evaluates to a value indicating whether to italicize any following
    /// text.</summary>
    public IMutableSeExpression? Enabled
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        base.EvaluateToSeStringBuilder(context, ssb);
        this.UpdateContext(context);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (!base.EvaluateToSpan(context, span, out bytesWritten))
            return false;
        this.UpdateContext(context);
        return true;
    }

    private void UpdateContext(ISeStringEvaluationContext context) =>
        context.Italicize = this.Enabled?.EvaluateAsBool(context) ?? false;
}
