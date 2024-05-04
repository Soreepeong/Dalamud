using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

/// <summary>Produces the name of the specified target object, or <c>???</c> if the name could not be resolved.
/// </summary>
public sealed class PcNamePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="PcNamePayload"/> class.</summary>
    public PcNamePayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.PcName)
    {
    }

    /// <summary>Gets or sets the expression that evaluates to the target object ID.</summary>
    public IMutableSeExpression? ObjectId
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        var e = context.GetObjectName(this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000));
        if (e is null)
            ssb.Append("???"u8);
        else
            e.EvaluateToSeStringBuilder(context, ssb);
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        var e = context.GetObjectName(this.ObjectId?.EvaluateAsInt(context) ?? unchecked((int)0xE0000000));
        if (e is null)
        {
            if (span.Length < 3)
            {
                bytesWritten = 0;
                return false;
            }
            
            "???"u8.CopyTo(span);
            bytesWritten = 3;
            return true;
        }

        return e.EvaluateToSpan(context, span, out bytesWritten);
    }
}
