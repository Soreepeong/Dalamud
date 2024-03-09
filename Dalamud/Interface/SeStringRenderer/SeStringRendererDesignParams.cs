using System.Numerics;

using Dalamud.Interface.Utility;

using ImGuiNET;

namespace Dalamud.Interface.SeStringRenderer;

/// <summary>Decorative parameters.</summary>
public record struct SeStringRendererDesignParams
{
    /// <summary>Gets or sets the default font to use.</summary>
    public ImFontPtr Font { get; set; }

    /// <summary>Gets or sets the italic font to use.</summary>
    /// <remarks>If not set, faux italic font of <see cref="Font"/> will be used.</remarks>
    public ImFontPtr FontItalic { get; set; }

    /// <summary>Gets or sets a value indicating whether to render the text in italics.</summary>
    public bool Italic { get; set; }

    /// <summary>Gets or sets the background color.</summary>
    public uint BackColorU32 { get; set; }

    /// <summary>Gets or sets the border color.</summary>
    public uint BorderColorU32 { get; set; }

    /// <summary>Gets or sets the foreground color.</summary>
    public uint ForeColorU32 { get; set; }

    /// <summary>Gets or sets the border width.</summary>
    /// <remarks>Currently, only the integer part is effective.</remarks>
    public float BorderWidth { get; set; }

    /// <inheritdoc cref="BackColorU32"/>
    public Vector4 BackColor
    {
        get => new(
            (this.BackColorU32 & 0xFF) / 255f,
            ((this.BackColorU32 >> 8) & 0xFF) / 255f,
            ((this.BackColorU32 >> 16) & 0xFF) / 255f,
            (this.BackColorU32 >> 24) / 255f);
        set => this.BackColorU32 =
                   (uint)Math.Clamp(value.X * 255, 0f, 255f) |
                   ((uint)Math.Clamp(value.Y * 255, 0f, 255f) << 8) |
                   ((uint)Math.Clamp(value.Z * 255, 0f, 255f) << 16) |
                   ((uint)Math.Clamp(value.W * 255, 0f, 255f) << 24);
    }

    /// <inheritdoc cref="BorderColorU32"/>
    public Vector4 BorderColor
    {
        get => new(
            (this.BorderColorU32 & 0xFF) / 255f,
            ((this.BorderColorU32 >> 8) & 0xFF) / 255f,
            ((this.BorderColorU32 >> 16) & 0xFF) / 255f,
            (this.BorderColorU32 >> 24) / 255f);
        set => this.BorderColorU32 =
                   (uint)Math.Clamp(value.X * 255, 0f, 255f) |
                   ((uint)Math.Clamp(value.Y * 255, 0f, 255f) << 8) |
                   ((uint)Math.Clamp(value.Z * 255, 0f, 255f) << 16) |
                   ((uint)Math.Clamp(value.W * 255, 0f, 255f) << 24);
    }

    /// <inheritdoc cref="ForeColorU32"/>
    public Vector4 ForeColor
    {
        get => new(
            (this.ForeColorU32 & 0xFF) / 255f,
            ((this.ForeColorU32 >> 8) & 0xFF) / 255f,
            ((this.ForeColorU32 >> 16) & 0xFF) / 255f,
            (this.ForeColorU32 >> 24) / 255f);
        set => this.ForeColorU32 =
                   (uint)Math.Clamp(value.X * 255, 0f, 255f) |
                   ((uint)Math.Clamp(value.Y * 255, 0f, 255f) << 8) |
                   ((uint)Math.Clamp(value.Z * 255, 0f, 255f) << 16) |
                   ((uint)Math.Clamp(value.W * 255, 0f, 255f) << 24);
    }

    /// <summary>Gets the effective font.</summary>
    internal ImFontPtr EffectiveFont => this.Italic && this.FontItalic.IsNotNullAndLoaded()
                                            ? this.FontItalic
                                            : this.Font;

    /// <summary>Gets a value indicating whether the effective font should be italicized by shearing.</summary>
    internal bool EffectiveIsFakeItalic => this.Italic && !this.FontItalic.IsNotNullAndLoaded();

    /// <summary>Creates a new instance of <see cref="SeStringRendererDesignParams"/> struct, using the current ImGui
    /// context and the recommended default values.</summary>
    /// <returns>A new instance of <see cref="SeStringRendererDesignParams"/>.</returns>
    public static SeStringRendererDesignParams FromCurrentImGuiContext() => new()
    {
        Font = ImGui.GetFont(),
        FontItalic = default,
        Italic = false,
        BackColorU32 = 0,
        BorderColorU32 = 0,
        ForeColorU32 = ApplyOpacity(ImGui.GetColorU32(ImGuiCol.Text), ImGui.GetStyle().Alpha),
        BorderWidth = 1f,
    };

    /// <summary>Adjusts the color by the given opacity.</summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The adjusted color.</returns>
    internal static uint ApplyOpacity(uint color, float opacity)
    {
        if (opacity >= 1f)
            return color;
        if (opacity <= 0f)
            return color & 0xFFFFFFu;

        // Dividing and multiplying by 256, to use flooring. Range is [0, 1).
        var a = (uint)(((color >> 24) / 256f) * opacity * 256f);
        return (color & 0xFFFFFFu) | (a << 24);
    }
}
