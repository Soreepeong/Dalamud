using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;

namespace Dalamud.Interface.SeStringRenderer;

/// <summary>A custom text renderer.</summary>
public interface ISeStringRenderer : IDisposable
{
    /// <summary>Gets or sets the render params.</summary>
    SeStringRendererParams Params { get; set; }

    /// <summary>Gets or sets the decorative parameters.</summary>
    SeStringRendererDesignParams DesignParams { get; set; }

    /// <summary>Gets or sets the decorative parameters for the wrap marker.</summary>
    SeStringRendererDesignParams WrapMarkerParams { get; set; }

    /// <summary>Renders the queued text. No further calls should be made.</summary>
    /// <param name="state">The final render state.</param>
    void Render(out RenderState state);

    /// <summary>Renders the queued text. No further calls should be made.</summary>
    /// <param name="state">The final render state.</param>
    /// <param name="payload">The payload being hovered, if any.</param>
    /// <returns><c>true</c> if any payload is currently being hovered.</returns>
    /// <remarks><paramref name="payload"/> is only valid until disposing this instance.</remarks>
    bool Render(out RenderState state, out SePayloadReadOnlySpan payload);

    /// <summary>Adds a line break.</summary>
    /// <param name="newLineType">The line break type. Multiple values may be specified.</param>
    /// <remarks>
    /// <para><see cref="SeStringRendererParams.AcceptedNewLines"/> still applies.</para>
    /// <para>If multiple values are specified, if at least one matches the above parameter, then a newline will be
    /// added.</para>
    /// </remarks>
    void AddNewLine(SeStringRendererParams.NewLineType newLineType = SeStringRendererParams.NewLineType.Manual);

    /// <summary>Adds the given UTF-16 char sequence to be rendered.</summary>
    /// <param name="span">The text to render.</param>
    void AddText(ReadOnlySpan<char> span);

    /// <summary>Adds the given UTF-8 byte sequence to be rendered.</summary>
    /// <param name="span">The text to render.</param>
    void AddText(ReadOnlySpan<byte> span);

    /// <summary>Adds the given SeString to be rendered.</summary>
    /// <param name="span">The SeString byte sequence.</param>
    /// <param name="context">The context for evaluating SeString components.</param>
    /// <remarks>Payloads and expressions, if any, will be evaluated immediately.</remarks>
    void AddSeString(SeStringReadOnlySpan span, ISeStringContext? context = null);

    /// <summary>Adds the given SeString to be rendered.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="span">The SeString byte sequence.</param>
    /// <param name="context">The context for evaluating SeString components.</param>
    /// <remarks>Payloads and expressions, if any, will be evaluated immediately.</remarks>
    void AddSeString<TContext>(SeStringReadOnlySpan span, ref TContext context)
        where TContext : struct, ISeStringContext;

    /// <summary>Draws an icon from the GFD file.</summary>
    /// <param name="iconId">The icon ID.</param>
    void AddGfdIcon(uint iconId);

    /// <summary>Sets the active link payload.</summary>
    /// <param name="payload">The payload. Specify <c>default</c> to end the link without starting a new one.</param>
    /// <remarks>Once set, the following rendered text will be the current interaction range.</remarks>
    void SetActiveLinkPayload(SePayloadReadOnlySpan payload);
}
