using System.Diagnostics.CodeAnalysis;
using System.Numerics;

using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.ImGuiSeStringRenderer.Internal;
using Dalamud.Interface.Utility.Internal;
using Dalamud.Utility.Text;

using ImGuiNET;

namespace Dalamud.Interface.Utility;

/// <summary>
/// Class containing various helper methods for use with ImGui inside Dalamud.
/// </summary>
public static partial class ImGuiHelpers
{
    /// <summary>
    /// Print out text that can be copied when clicked.
    /// </summary>
    /// <param name="text">The text to show.</param>
    /// <param name="textCopy">The text to copy when clicked.</param>
    public static void ClickToCopyText(string text, string? textCopy = null)
    {
        textCopy ??= text;
        ImGui.Text($"{text}");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (textCopy != text) ImGui.SetTooltip(textCopy);
        }

        if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{textCopy}");
    }

    /// <summary>Draws a SeString.</summary>
    /// <param name="sss">SeString to draw.</param>
    /// <param name="style">Initial rendering style.</param>
    /// <param name="imGuiId">ImGui ID, if link functionality is desired.</param>
    /// <param name="buttonFlags">Button flags to use on link interaction.</param>
    /// <returns>Interaction result of the rendered text.</returns>
    /// <remarks>This function is experimental. Report any issues to GitHub issues or to Discord #dalamud-dev channel.
    /// The function definition is stable; only in the next API version a function may be removed.</remarks>
    [Experimental("SeStringRenderer")]
    public static SeStringDrawResult SeStringWrapped(
        ReadOnlySpan<byte> sss,
        scoped in SeStringDrawParams style = default,
        ImGuiId imGuiId = default,
        ImGuiButtonFlags buttonFlags = ImGuiButtonFlags.MouseButtonDefault) =>
        Service<SeStringRenderer>.Get().Draw(sss, style, imGuiId, buttonFlags);

    /// <summary>Draws a SeString.</summary>
    /// <param name="sss">SeString to draw.</param>
    /// <param name="style">Initial rendering style.</param>
    /// <param name="imGuiId">ImGui ID, if link functionality is desired.</param>
    /// <param name="buttonFlags">Button flags to use on link interaction.</param>
    /// <returns>Interaction result of the rendered text.</returns>
    /// <remarks>This function is experimental. Report any issues to GitHub issues or to Discord #dalamud-dev channel.
    /// The function definition is stable; only in the next API version a function may be removed.</remarks>
    [Experimental("SeStringRenderer")]
    public static SeStringDrawResult SeStringWrapped(
        Utf8InterpolatedStringHandler sss,
        scoped in SeStringDrawParams style = default,
        ImGuiId imGuiId = default,
        ImGuiButtonFlags buttonFlags = ImGuiButtonFlags.MouseButtonDefault)
    {
        using (sss)
            return Service<SeStringRenderer>.Get().Draw(sss.Formatted, style, imGuiId, buttonFlags);
    }

    /// <summary>Creates and caches a SeString from a text macro representation, and then draws it.</summary>
    /// <param name="text">SeString text macro representation.
    /// Newline characters will be normalized to <see cref="NewLinePayload"/>.</param>
    /// <param name="style">Initial rendering style.</param>
    /// <param name="imGuiId">ImGui ID, if link functionality is desired.</param>
    /// <param name="buttonFlags">Button flags to use on link interaction.</param>
    /// <returns>Interaction result of the rendered text.</returns>
    /// <remarks>This function is experimental. Report any issues to GitHub issues or to Discord #dalamud-dev channel.
    /// The function definition is stable; only in the next API version a function may be removed.</remarks>
    [Experimental("SeStringRenderer")]
    public static SeStringDrawResult CompileSeStringWrapped(
        string text,
        scoped in SeStringDrawParams style = default,
        ImGuiId imGuiId = default,
        ImGuiButtonFlags buttonFlags = ImGuiButtonFlags.MouseButtonDefault) =>
        Service<SeStringRenderer>.Get().CompileAndDrawWrapped(text, style, imGuiId, buttonFlags);

    /// <summary>Write unformatted text.</summary>
    /// <param name="text">The text to write.</param>
    public static void SafeText(string text) => SafeText($"{text}");

    /// <inheritdoc cref="SafeText(string)"/>
    public static void SafeText(Utf8InterpolatedStringHandler text)
    {
        using (text)
            SafeTextWrapped(text.Formatted);
    }

    /// <inheritdoc cref="SafeText(string)"/>
    public static unsafe void SafeText(ReadOnlySpan<byte> text)
    {
        fixed (byte* pText = text)
            ImGuiNative.igTextUnformatted(pText, pText + text.Length);
    }

    /// <summary>Write unformatted text wrapped.</summary>
    /// <param name="text">The text to write.</param>
    public static void SafeTextWrapped(string text) => SafeTextWrapped($"{text}");

    /// <inheritdoc cref="SafeTextWrapped(string)"/>
    public static void SafeTextWrapped(Utf8InterpolatedStringHandler text)
    {
        using (text)
            SafeTextWrapped(text.Formatted);
    }

    /// <inheritdoc cref="SafeTextWrapped(string)"/>
    public static unsafe void SafeTextWrapped(ReadOnlySpan<byte> text)
    {
        // TextWrappedV has a special handling in case the format string is "%s\0".
        // terminating \0 is implicit in C# UTF-8 literals.
        fixed (byte* pFormat = "%s"u8)
        {
            fixed (byte* pText = text)
                CImGuiImports.TextWrappedV(pFormat, &pText);
        }
    }

    /// <summary>Write unformatted text wrapped.</summary>
    /// <param name="color">The color of the text.</param>
    /// <param name="text">The text to write.</param>
    public static void SafeTextColoredWrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        SafeTextWrapped(text);
        ImGui.PopStyleColor();
    }

    /// <inheritdoc cref="SafeTextColoredWrapped(Vector4, string)"/>
    public static void SafeTextColoredWrapped(Vector4 color, Utf8InterpolatedStringHandler text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        SafeTextWrapped(text);
        ImGui.PopStyleColor();
    }

    /// <inheritdoc cref="SafeTextColoredWrapped(Vector4, string)"/>
    public static void SafeTextColoredWrapped(Vector4 color, ReadOnlySpan<byte> text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        SafeTextWrapped(text);
        ImGui.PopStyleColor();
    }
}
