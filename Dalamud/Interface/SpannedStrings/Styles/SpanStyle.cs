using System.Numerics;

using Dalamud.Interface.SpannedStrings.Enums;
using Dalamud.Interface.SpannedStrings.Internal;

using ImGuiNET;

namespace Dalamud.Interface.SpannedStrings.Styles;

/// <summary>Decorative parameters.</summary>
public struct SpanStyle
{
    /// <summary>The default font to use.</summary>
    public FontHandleVariantSet Font;

    /// <summary>Whether to render the text in italics.</summary>
    public bool Italic;

    /// <summary>Whether to render the text in bold.</summary>
    public bool Bold;

    /// <summary>The background color.</summary>
    /// <remarks>Pixel layout is RGBA; in little endian uint32, it is 0xAABBGGRR.</remarks>
    public Rgba32 BackColorU32;

    /// <summary>The shadow color.</summary>
    /// <remarks>Pixel layout is RGBA; in little endian uint32, it is 0xAABBGGRR.</remarks>
    public Rgba32 ShadowColorU32;

    /// <summary>The border color.</summary>
    /// <remarks>Pixel layout is RGBA; in little endian uint32, it is 0xAABBGGRR.</remarks>
    public Rgba32 EdgeColorU32;

    /// <summary>The foreground color.</summary>
    /// <remarks>Pixel layout is RGBA; in little endian uint32, it is 0xAABBGGRR.</remarks>
    public Rgba32 ForeColorU32;

    /// <summary>The border width.</summary>
    /// <remarks>Currently, only the integer part is effective.</remarks>
    public float BorderWidth;

    /// <summary>The shadow offset.</summary>
    /// <remarks>If <see cref="Vector2.Zero"/>, then shadow is turned off.</remarks>
    public Vector2 ShadowOffset;

    /// <summary>The font size.</summary>
    /// <remarks>If not set (<c>0</c> or less), then the current font size will be used.</remarks>
    public float FontSize;

    /// <summary>The line height, relative to <see cref="FontSize"/>.</summary>
    /// <remarks>
    /// If not set (<c>0</c> or less), then <c>1.0</c> is assumed.
    /// If you want to use <c>0</c>, then use <see cref="float.Epsilon"/> instead.
    /// </remarks>
    public float LineHeight;

    /// <summary>The Horizontal offset, relative to <see cref="FontSize"/>.</summary>
    /// <remarks>
    /// <c>1</c> will shift the text rightwards by <see cref="FontSize"/>.<br />
    /// <c>-1</c> will shift the text leftwards by <see cref="FontSize"/>.
    /// </remarks>
    public float HorizontalOffset;

    /// <summary>The horizontal alignment. Applicable for the current whole line.</summary>
    /// <remarks>If changed multiple times in a line, the last value wins.</remarks>
    public HorizontalAlignment HorizontalAlignment;

    /// <summary>The vertical offset, relative to <see cref="FontSize"/>.</summary>
    /// <remarks>
    /// <c>1</c> will shift the text downwards by <see cref="FontSize"/>.<br />
    /// <c>-1</c> will shift the text upwards by <see cref="FontSize"/>.
    /// </remarks>
    public float VerticalOffset;

    /// <summary>The vertical alignment, in case of a line consisting of texts of mixed line heights.</summary>
    public VerticalAlignment VerticalAlignment;

    /// <summary>Gets the empty span style. Nothing will be drawn.</summary>
    public static SpanStyle Empty => default;

    /// <summary>Gets the style from current ImGui context.</summary>
    public static SpanStyle FromContext => new()
    {
        ForeColorU32 = ApplyOpacity(ImGui.GetColorU32(ImGuiCol.Text), ImGui.GetStyle().Alpha),
    };

    /// <summary>Updates the struct according to the spanned record.</summary>
    /// <param name="data">The data.</param>
    /// <param name="record">The spanned record.</param>
    /// <param name="recordData">The attached data.</param>
    /// <param name="initialStyle">The initial style to revert to.</param>
    /// <param name="fontUpdated">Whether any of the font parameters have been updated.</param>
    /// <param name="colorUpdated">Whether any of the decorative parameters have been updated.</param>
    internal void UpdateFrom(
        in SpannedStringData data,
        in SpannedRecord record,
        ReadOnlySpan<byte> recordData,
        in SpanStyle initialStyle,
        out bool fontUpdated,
        out bool colorUpdated)
    {
        fontUpdated = colorUpdated = false;
        if (record.IsRevert)
        {
            switch (record.Type)
            {
                case SpannedRecordType.FontHandleSetIndex:
                    this.Font = initialStyle.Font;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.FontSize:
                    this.FontSize = initialStyle.FontSize;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.LineHeight:
                    this.LineHeight = initialStyle.LineHeight;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.HorizontalOffset:
                    this.HorizontalOffset = initialStyle.HorizontalOffset;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.HorizontalAlignment:
                    this.HorizontalAlignment = initialStyle.HorizontalAlignment;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.VerticalOffset:
                    this.VerticalOffset = initialStyle.VerticalOffset;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.VerticalAlignment:
                    this.VerticalAlignment = initialStyle.VerticalAlignment;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.Italic:
                    this.Italic = initialStyle.Italic;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.Bold:
                    this.Bold = initialStyle.Bold;
                    fontUpdated = true;
                    return;

                case SpannedRecordType.BackColor:
                    this.BackColorU32 = initialStyle.BackColorU32;
                    colorUpdated = true;
                    return;

                case SpannedRecordType.ShadowColor:
                    this.ShadowColorU32 = initialStyle.ShadowColorU32;
                    colorUpdated = true;
                    return;

                case SpannedRecordType.EdgeColor:
                    this.EdgeColorU32 = initialStyle.EdgeColorU32;
                    colorUpdated = true;
                    return;

                case SpannedRecordType.ForeColor:
                    this.ForeColorU32 = initialStyle.ForeColorU32;
                    colorUpdated = true;
                    return;

                case SpannedRecordType.BorderWidth:
                    this.BorderWidth = initialStyle.BorderWidth;
                    colorUpdated = true;
                    return;

                case SpannedRecordType.ShadowOffset:
                    this.ShadowOffset = initialStyle.ShadowOffset;
                    colorUpdated = true;
                    return;

                default:
                    return;
            }
        }

        switch (record.Type)
        {
            case SpannedRecordType.FontHandleSetIndex
                when SpannedRecordCodec.TryDecodeFontHandleSetIndex(recordData, out var index):
                this.Font = data.TryGetFontSetAt(index, out var newFontSet) ? newFontSet : initialStyle.Font;
                fontUpdated = true;
                return;

            case SpannedRecordType.FontSize
                when SpannedRecordCodec.TryDecodeFontSize(recordData, out this.FontSize):
                fontUpdated = true;
                return;

            case SpannedRecordType.LineHeight
                when SpannedRecordCodec.TryDecodeLineHeight(recordData, out this.LineHeight):
                fontUpdated = true;
                return;

            case SpannedRecordType.HorizontalOffset
                when SpannedRecordCodec.TryDecodeHorizontalOffset(recordData, out this.HorizontalOffset):
                fontUpdated = true;
                return;

            case SpannedRecordType.HorizontalAlignment
                when SpannedRecordCodec.TryDecodeHorizontalAlignment(recordData, out this.HorizontalAlignment):
                fontUpdated = true;
                return;

            case SpannedRecordType.VerticalOffset
                when SpannedRecordCodec.TryDecodeVerticalOffset(recordData, out this.VerticalOffset):
                fontUpdated = true;
                return;

            case SpannedRecordType.VerticalAlignment
                when SpannedRecordCodec.TryDecodeVerticalAlignment(recordData, out this.VerticalAlignment):
                fontUpdated = true;
                return;

            case SpannedRecordType.Italic
                when SpannedRecordCodec.TryDecodeItalic(recordData, out var value):
                this.Italic =
                    value switch
                    {
                        BoolOrToggle.On => true,
                        BoolOrToggle.Off => false,
                        BoolOrToggle.NoChange => initialStyle.Italic,
                        _ => !initialStyle.Italic,
                    };
                fontUpdated = true;
                return;

            case SpannedRecordType.Bold
                when SpannedRecordCodec.TryDecodeBold(recordData, out var value):
                this.Bold =
                    value switch
                    {
                        BoolOrToggle.On => true,
                        BoolOrToggle.Off => false,
                        BoolOrToggle.NoChange => initialStyle.Bold,
                        _ => !initialStyle.Bold,
                    };
                fontUpdated = true;
                return;

            case SpannedRecordType.BackColor
                when SpannedRecordCodec.TryDecodeBackColor(recordData, out this.BackColorU32):
                colorUpdated = true;
                return;

            case SpannedRecordType.ShadowColor
                when SpannedRecordCodec.TryDecodeShadowColor(recordData, out this.ShadowColorU32):
                colorUpdated = true;
                return;

            case SpannedRecordType.EdgeColor
                when SpannedRecordCodec.TryDecodeEdgeColor(recordData, out this.EdgeColorU32):
                colorUpdated = true;
                return;

            case SpannedRecordType.ForeColor
                when SpannedRecordCodec.TryDecodeForeColor(recordData, out this.ForeColorU32):
                colorUpdated = true;
                return;

            case SpannedRecordType.BorderWidth
                when SpannedRecordCodec.TryDecodeBorderWidth(recordData, out this.BorderWidth):
                colorUpdated = true;
                return;

            case SpannedRecordType.ShadowOffset
                when SpannedRecordCodec.TryDecodeShadowOffset(recordData, out this.ShadowOffset):
                colorUpdated = true;
                return;
        }
    }

    /// <summary>Adjusts the color by the given opacity.</summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity.</param>
    /// <returns>The adjusted color.</returns>
    private static uint ApplyOpacity(uint color, float opacity)
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