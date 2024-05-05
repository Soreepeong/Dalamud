namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets the edge glow text color from UIColor sheet, and then adjusts the color stack.</summary>
public sealed class EdgeColorTypePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="EdgeColorTypePayload"/> class.</summary>
    public EdgeColorTypePayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.EdgeColorType)
    {
    }

    /// <summary>Gets or sets the color type, which is a row ID in the UIColor sheet.</summary>
    public IMutableSeExpression? ColorType
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    // TODO: implement evaluation
}
