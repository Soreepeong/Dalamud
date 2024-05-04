namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ObjectProducers;

/// <summary>Displays an icon, taking consideration of remapped gamepad buttons.</summary>
public sealed class Icon2Payload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="Icon2Payload"/> class.</summary>
    public Icon2Payload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Icon2)
    {
    }

    /// <summary>Gets or sets the icon.</summary>
    public IMutableSeExpression? Icon
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }
}
