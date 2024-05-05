namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets the scale.</summary>
public sealed class ScalePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="ScalePayload"/> class.</summary>
    public ScalePayload()
        : base(1, 1, (int)Lumina.Text.Payloads.MacroCode.Scale)
    {
    }

    /// <summary>Gets or sets the argument.</summary>
    // TODO: find out what it does
    public IMutableSeExpression? Arg
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }
    
    // TODO: implement evaluation
}
