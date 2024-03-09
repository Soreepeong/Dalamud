using System.Numerics;

using ImGuiNET;

namespace Dalamud.Interface.SeStringRenderer;

/// <summary>Represents a render state.</summary>
public record struct RenderState
{
    /// <summary>Gets or sets the first drawing screen offset.</summary>
    public Vector2 StartScreenOffset { get; set; }

    /// <summary>Gets or sets the final drawing relative offset.</summary>
    /// <remarks>Relativity begins from the cursor position at the construction of this struct.</remarks>
    public Vector2 Offset { get; set; }

    /// <summary>Gets or sets the left top relative offset of the text rendered so far.</summary>
    /// <remarks>Relativity begins from the cursor position at the construction of this struct.</remarks>
    public Vector2 BoundsLeftTop { get; set; }

    /// <summary>Gets or sets the right bottom relative offset of the text rendered so far.</summary>
    /// <remarks>Relativity begins from the cursor position at the construction of this struct.</remarks>
    public Vector2 BoundsRightBottom { get; set; }

    /// <summary>Gets or sets the total number of lines so far, including new lines from word wrapping.</summary>
    public int LastLineIndex { get; set; }

    /// <summary>Gets or sets the clicked mouse button.</summary>
    public ImGuiMouseButton ClickedMouseButton { get; set; }
}
