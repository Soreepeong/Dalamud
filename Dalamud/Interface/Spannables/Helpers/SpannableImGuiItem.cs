using System.Numerics;
using System.Runtime.CompilerServices;

using Dalamud.Interface.Spannables.Internal;
using Dalamud.Utility.Numerics;

namespace Dalamud.Interface.Spannables.Helpers;

/// <summary>Helper methods for implementing ImGui items.</summary>
public static class SpannableImGuiItem
{
    /// <summary>Declare item bounding box for clipping and interaction.
    /// Note that the size can be different than the one provided to ItemSize(). Typically, widgets that spread over
    /// available surface declare their minimum size requirement to ItemSize() and provide a larger region to ItemAdd()
    /// which is used drawing/interaction.</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="imGuiGlobalId">The ImGui global Id.</param>
    /// <param name="rcHover">The hit-test boundary.</param>
    /// <param name="rcNav">The nav-test boundary.</param>
    /// <param name="rcDisplay">The display boundary.</param>
    /// <param name="hovered">Whether the control is hovered.</param>
    /// <param name="noNav">Whether to disable navigation/focus.</param>
    /// <param name="noNavDefaultFocus">Whether to not apply default focus.</param>
    /// <param name="disabled">Whether the item is disabled.</param>
    public static unsafe void ItemAdd(
        Spannable measurement,
        uint imGuiGlobalId,
        RectVector4 rcHover,
        RectVector4 rcNav,
        RectVector4 rcDisplay,
        bool hovered,
        bool noNav,
        bool noNavDefaultFocus,
        bool disabled)
    {
        if (imGuiGlobalId == 0)
            return;
        ref var context = ref ImGuiInternals.ImGuiContext.Instance;
        rcHover = RectVector4.TransformLossy(rcHover, measurement.FullTransformation);
        rcNav = RectVector4.TransformLossy(rcNav, measurement.FullTransformation);
        rcDisplay = RectVector4.TransformLossy(rcDisplay, measurement.FullTransformation);
        ImGuiInternals.ImGuiItemAdd(
            &rcHover,
            imGuiGlobalId,
            &rcNav,
            ImGuiInternals.ImGuiItemFlags.Inputable
            | (noNav
                   ? ImGuiInternals.ImGuiItemFlags.NoNav | ImGuiInternals.ImGuiItemFlags.NoTabStop
                   : ImGuiInternals.ImGuiItemFlags.None)
            | (noNavDefaultFocus ? ImGuiInternals.ImGuiItemFlags.NoNavDefaultFocus : ImGuiInternals.ImGuiItemFlags.None)
            | (disabled ? ImGuiInternals.ImGuiItemFlags.Disabled : ImGuiInternals.ImGuiItemFlags.None));
        context.LastItemData.DisplayRect = rcDisplay;
        if (hovered)
            context.LastItemData.StatusFlags &= ImGuiInternals.ImGuiItemStatusFlags.HoveredRect;
        else
            context.LastItemData.StatusFlags &= ~ImGuiInternals.ImGuiItemStatusFlags.HoveredRect;
    }

    /// <summary>Marks the specified inner ID as active (pressed).</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="imGuiGlobalId">The ImGui global Id.</param>
    /// <param name="useWheel">Whether to take wheel inputs, preventing window from handling wheel events.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void SetActive(Spannable measurement, uint imGuiGlobalId, bool useWheel = false)
    {
        if (imGuiGlobalId != 0)
            ImGuiInternals.ImGuiSetActiveId(imGuiGlobalId, ImGuiInternals.ImGuiContext.Instance.CurrentWindow);

        if (useWheel)
            ImGuiInternals.ImGuiContext.Instance.ActiveIdUsingMouseWheel = 1;
    }

    /// <summary>Marks the specified inner ID as focused.</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="imGuiGlobalId">The ImGui global Id.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void SetFocused(Spannable measurement, uint imGuiGlobalId)
    {
        if (imGuiGlobalId != 0)
            ImGuiInternals.ImGuiSetFocusedId(imGuiGlobalId, ImGuiInternals.ImGuiContext.Instance.CurrentWindow);
    }

    /// <summary>Marks the specified inner ID as hovered.</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="imGuiGlobalId">The ImGui global Id.</param>
    /// <param name="useWheel">Whether to take wheel inputs, preventing window from handling wheel events.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void SetHovered(Spannable measurement, uint imGuiGlobalId, bool useWheel = false)
    {
        if (imGuiGlobalId != 0)
            ImGuiInternals.ImGuiSetHoveredId(imGuiGlobalId);
        if (useWheel)
            ImGuiInternals.ImGuiContext.Instance.HoveredIdUsingMouseWheel = 1;
    }

    /// <summary>Clears the active item ID.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ClearActive() =>
        ImGuiInternals.ImGuiSetActiveId(0, ImGuiInternals.ImGuiContext.Instance.CurrentWindow);

    /// <summary>Determines if some other item is active.</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="mouseLocalLocation">Local mouse location.</param>
    /// <param name="rc">The rect in local coordinates.</param>
    /// <returns><c>true</c> if something else is active.</returns>
    public static unsafe bool IsItemHoverable(Spannable measurement, Vector2 mouseLocalLocation, in RectVector4 rc)
    {
        ref var g = ref ImGuiInternals.ImGuiContext.Instance;
        var rcGlobal = RectVector4.TransformLossy(rc, measurement.FullTransformation);
        var prevHover = g.HoveredId;
        var prevHoverDisabled = g.HoveredIdDisabled;
        if (ImGuiInternals.ImGuiItemHoverable(&rcGlobal, measurement.ImGuiGlobalId) == 0)
            return false;
        if (!rc.Contains(mouseLocalLocation))
        {
            ImGuiInternals.ImGuiSetHoveredId(prevHover);
            g.HoveredIdDisabled = prevHoverDisabled;
            return false;
        }

        return true;
    }
}