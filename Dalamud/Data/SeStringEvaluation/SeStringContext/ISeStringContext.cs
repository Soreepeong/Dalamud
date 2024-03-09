using Dalamud.Game.Text.SeStringHandling.SeStringSpan;

namespace Dalamud.Data.SeStringEvaluation.SeStringContext;

/// <summary>SeString local parameter context provider.</summary>
/// <remarks>Only implement the features you need.</remarks>
public interface ISeStringContext
{
    /// <summary>Gets a value indicating whether the provider prefers to have
    /// <see cref="ProduceString(ReadOnlySpan{byte})"/> called more than
    /// <see cref="ProduceString(ReadOnlySpan{char})"/>.</summary>
    /// <remarks>Either one may be called regardless of this configuration.</remarks>
    bool PreferProduceInChar => true;

    /// <summary>Gets the sheet language.</summary>
    ClientLanguage SheetLanguage => ClientLanguage.English;

    /// <summary>Attempts to get the number for the given placeholder.</summary>
    /// <param name="exprType">The placeholder expression type.</param>
    /// <param name="value">The resolved value.</param>
    /// <returns><c>true</c> if the parameter is retrieved.</returns>
    bool TryGetPlaceholderNum(byte exprType, out uint value)
    {
        value = 0;
        return false;
    }

    /// <summary>Attempts to produce the string for the given placeholder.</summary>
    /// <param name="exprType">The placeholder expression type.</param>
    /// <param name="targetContext">The target context.</param>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <returns><c>true</c> if the parameter is produced.</returns>
    bool TryProducePlaceholder<TContext>(byte exprType, ref TContext targetContext)
        where TContext : ISeStringContext => false;

    /// <summary>Updates the placeholder.</summary>
    /// <param name="exprType">The placeholder expression type.</param>
    /// <param name="value">The new value.</param>
    void UpdatePlaceholder(byte exprType, uint value) => Nop();

    /// <summary>Attempts to get the <c>uint</c> local parameter at the given index.</summary>
    /// <param name="parameterIndex">The zero-based parameter index.</param>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c> if the parameter is retrieved.</returns>
    bool TryGetLNum(uint parameterIndex, out uint value)
    {
        value = default;
        return false;
    }

    /// <summary>Attempts to produce the string parameter at the given index.</summary>
    /// <param name="parameterIndex">The zero-based parameter index.</param>
    /// <param name="targetContext">The target context.</param>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <returns><c>true</c> if the parameter is produced.</returns>
    bool TryProduceLStr<TContext>(uint parameterIndex, ref TContext targetContext)
        where TContext : ISeStringContext => false;

    /// <summary>Attempts to get the <c>uint</c> local parameter at the given index.</summary>
    /// <param name="parameterIndex">The zero-based parameter index.</param>
    /// <param name="value">The value.</param>
    /// <returns><c>true</c> if the parameter is retrieved.</returns>
    /// <remarks>Return <c>false</c> to use the default handler.</remarks>
    bool TryGetGNum(uint parameterIndex, out uint value)
    {
        value = default;
        return false;
    }

    /// <summary>Attempts to produce the string parameter at the given index.</summary>
    /// <param name="parameterIndex">The zero-based parameter index.</param>
    /// <param name="targetContext">The target context.</param>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <returns><c>true</c> if the parameter is produced.</returns>
    /// <remarks>Return <c>false</c> to use the default handler.</remarks>
    bool TryProduceGStr<TContext>(uint parameterIndex, ref TContext targetContext)
        where TContext : ISeStringContext => false;

    /// <summary>Handles a payload.</summary>
    /// <param name="payload">The payload to handle.</param>
    /// <param name="targetContext">The target context.</param>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <returns><c>true</c> to suppress further handling of the payload.</returns>
    bool HandlePayload<TContext>(SePayloadReadOnlySpan payload, ref TContext targetContext) 
        where TContext : ISeStringContext => false;

    /// <summary>Produces a string.</summary>
    /// <param name="value">The produced value.</param>
    void ProduceString(ReadOnlySpan<byte> value) => Nop();

    /// <summary>Produces a string.</summary>
    /// <param name="value">The produced value.</param>
    void ProduceString(ReadOnlySpan<char> value) => Nop();

    /// <summary>Produces a string.</summary>
    /// <param name="value">The produced value.</param>
    void ProduceSeString(SeStringReadOnlySpan value) => Nop();

    /// <summary>Notifies of an error during evaulation.</summary>
    /// <param name="msg">The error message.</param>
    void ProduceError(string msg) => Nop();

    /// <summary>Produces a newline.</summary>
    /// <remarks>Defaults to calling <see cref="ProduceString(ReadOnlySpan{byte})"/> or
    /// <see cref="ProduceString(ReadOnlySpan{char})"/> with <c>"\n"</c> according to <see cref="PreferProduceInChar"/>.
    /// </remarks>
    void ProduceNewLine()
    {
        if (this.PreferProduceInChar)
            this.ProduceString("\n");
        else
            this.ProduceString("\n"u8);
    }

    /// <summary>Pushes a foreground color value into a foreground color stack.</summary>
    /// <param name="colorBgra">The color to push.</param>
    void PushForeColor(uint colorBgra) => Nop();

    /// <summary>Pops a foreground color value from a foreground color stack.</summary>
    void PopForeColor() => Nop();

    /// <summary>Pushes a border color value a the border color stack.</summary>
    /// <param name="colorBgra">The color to push.</param>
    void PushBorderColor(uint colorBgra) => Nop();

    /// <summary>Pops a border color value a the border color stack.</summary>
    void PopBorderColor() => Nop();

    /// <summary>Sets whether to activate italic from now on.</summary>
    /// <param name="useItalic">Whether to use italic font.</param>
    void SetItalic(bool useItalic) => Nop();

    /// <summary>Sets whether to activate bold from now on.</summary>
    /// <param name="useBold">Whether to use bold font.</param>
    void SetBold(bool useBold) => Nop();

    /// <summary>Draws an icon specified in the GFD file.</summary>
    /// <param name="iconId">The icon ID.</param>
    void DrawIcon(uint iconId) => Nop();

    /// <summary>Does nothing.</summary>
    private static void Nop()
    {
    }
}
