namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ObjectProducers;

/// <summary>Displays an icon, without considering remapped gamepad buttons.</summary>
public sealed class IconPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="IconPayload"/> class.</summary>
    public IconPayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Icon)
    {
    }

    /// <summary>Gets or sets the icon.</summary>
    public IMutableSeExpression? Icon
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }
}
