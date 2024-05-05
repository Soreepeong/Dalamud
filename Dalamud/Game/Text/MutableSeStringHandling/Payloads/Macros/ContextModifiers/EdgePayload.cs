namespace Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;

/// <summary>Sets how a text edge glow is displayed.</summary>
public sealed class EdgePayload : FixedFormPayload
{
    /// <summary>Initializes a new instance of the <see cref="EdgePayload"/> class.</summary>
    public EdgePayload()
        : base(2, 2, (int)Lumina.Text.Payloads.MacroCode.Edge)
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
