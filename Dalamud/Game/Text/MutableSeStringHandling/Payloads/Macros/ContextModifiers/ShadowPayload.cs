namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets how a text shadow is displayed.</summary>
public sealed class ShadowPayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="ShadowPayload"/> class.</summary>
    public ShadowPayload()
        : base(2, 2, (int)Lumina.Text.Payloads.MacroCode.Shadow)
    {
    }

    /// <summary>Gets or sets the first argument.</summary>
    // TODO: find out what it does
    public IMutableSeExpression? Arg1
    {
        get => this.ExpressionAt(0);
        set => this.ExpressionAt(0) = value;
    }

    /// <summary>Gets or sets the second argument.</summary>
    // TODO: find out what it does
    public IMutableSeExpression? Arg2
    {
        get => this.ExpressionAt(1);
        set => this.ExpressionAt(1) = value;
    }
    
    // TODO: implement evaluation
}
