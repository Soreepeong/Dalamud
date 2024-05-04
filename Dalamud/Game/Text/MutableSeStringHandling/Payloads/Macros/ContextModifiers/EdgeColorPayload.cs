using Dalamud.Game.Text.MutableSeStringHandling.Expressions.Nullary;

using Lumina.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets the edge glow color, and then adjusts the color stack.</summary>
public sealed class EdgeColorPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="EdgeColorPayload"/> class.</summary>
    public EdgeColorPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.EdgeColor)
    {
    }

    /// <summary>Gets or sets the color in RGBA32.</summary>
    public IMutableSeExpression? EdgeColor
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <inheritdoc/>
    public override bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        if (this.EdgeColor is StackColorSeExpression)
        {
            if (!base.EvaluateToSpan(context, span, out bytesWritten))
                return false;
            if (context.TryPopColor(out var color))
                context.EdgeColor = color;
        }
        else
        {
            var color = this.EdgeColor?.EvaluateAsUInt(context) ?? 0u;
            var colorLength = SeExpressionUtilities.CalculateLengthUInt(color);

            var nb = 3 + SeExpressionUtilities.CalculateLengthInt(colorLength) + colorLength;
            if (span.Length < nb)
            {
                bytesWritten = 0;
                return false;
            }

            bytesWritten = 0;
            bytesWritten += SeExpressionUtilities.WriteRaw(span[bytesWritten..], 0x02);
            bytesWritten += SeExpressionUtilities.WriteRaw(span[bytesWritten..], (byte)this.MacroCode);
            bytesWritten += SeExpressionUtilities.EncodeInt(span[bytesWritten..], colorLength);
            bytesWritten += SeExpressionUtilities.EncodeUInt(span[bytesWritten..], color);
            bytesWritten += SeExpressionUtilities.WriteRaw(span[bytesWritten..], 0x03);

            context.PushColor(color);
            context.EdgeColor = color;
        }
        
        return true;
    }
}
