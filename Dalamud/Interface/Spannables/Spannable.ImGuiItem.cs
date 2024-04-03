using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Interface.Spannables.EventHandlers;
using Dalamud.Interface.Spannables.Helpers;
using Dalamud.Interface.Spannables.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Utility.Numerics;

using ImGuiNET;

namespace Dalamud.Interface.Spannables;

/// <summary>Base class for <see cref="Spannable"/>s.</summary>
public abstract partial class Spannable
{
    private bool wasCapturingMouse;
    private bool wasFocused;

    /// <summary>Gets or sets the available slot index for inner ID, for use with
    /// <see cref="SpannableExtensions.GetGlobalIdFromInnerId"/>.</summary>
    protected int InnerIdAvailableSlot { get; set; }

    /// <summary>Gets a value indicating whether this item does not support navigation(focus).</summary>
    protected virtual bool ImGuiItemNoNav => false;

    /// <summary>Gets a value indicating whether this item should not be given a default focus.</summary>
    protected virtual bool ImGuiItemNoNavDefaultFocus => true;

    /// <summary>Gets a value indicating whether this item is disabled.</summary>
    protected virtual bool ImGuiItemDisabled => false;

    /// <summary>Gets an ImGui-reported rect for use with hover testing.</summary>
    /// <remarks>The actual test is done with <see cref="HitTest"/>.</remarks>
    protected virtual RectVector4 ImGuiHoverRect => this.Boundary;

    /// <summary>Gets an ImGui-reported rect for use with navigation(focus) testing.</summary>
    protected virtual RectVector4 ImGuiNavRect => this.Boundary;

    /// <summary>Gets an ImGui-reported display rect.</summary>
    protected virtual RectVector4 ImGuiDisplayRect => this.Boundary;

    /// <summary>Gets a value indicating whether this ImGui item is focused.</summary>
    protected bool ImGuiIsFocused =>
        ImGuiInternals.ImGuiContext.Instance.NavId == this.GetGlobalIdFromInnerId(this.selfInnerId);

    /// <summary>Gets a value indicating whether this ImGui item can be hovered, regardless of the mouse pointer
    /// location.</summary>
    protected bool ImGuiIsHoverable
    {
        get
        {
            ref var ctx = ref ImGuiInternals.ImGuiContext.Instance;
            var gid = this.GetGlobalIdFromInnerId(this.selfInnerId);
            return (ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) || this.ShouldCapture)
                   && gid != 0
                   && (ctx.HoveredId == 0
                       || ctx.HoveredId == gid
                       || ctx.HoveredIdAllowOverlap != 0)
                   && (ctx.ActiveId == 0
                       || ctx.ActiveId == gid
                       || ctx.ActiveIdAllowOverlap != 0);
        }
    }

    /// <summary>Gets a value indicating whether this ImGui item is currently being considered as hovered.</summary>
    protected bool ImGuiIsHovered =>
        this.ImGuiGlobalId != 0
        && ImGuiInternals.ImGuiContext.Instance.HoveredId == this.GetGlobalIdFromInnerId(this.selfInnerId);

    /// <summary>Gets a value indicating whether this ImGui item is currently being considered as active.</summary>
    protected bool ImGuiIsActive =>
        this.ImGuiGlobalId != 0
        && ImGuiInternals.ImGuiContext.Instance.ActiveId == this.GetGlobalIdFromInnerId(this.selfInnerId);

    private bool ImGuiWantRootKeyboardProcessing
    {
        get
        {
            if (this.takeKeyboardInputsAlways)
                return true;
            if (this.takeKeyboardInputsOnFocus && this.ImGuiIsFocused)
                return true;

            var children = this.GetAllChildSpannables();
            for (var i = children.Count - 1; i >= 0; i--)
            {
                if (children[i]?.ImGuiWantRootKeyboardProcessing is true)
                    return true;
            }

            return false;
        }
    }

    /// <summary>Dispatches events as the root of the spannable tree.</summary>
    public unsafe void RenderPassDispatchEventsAsRoot()
    {
        ImGuiItemAddRecursive(this);

        var prevActive = ImGuiInternals.ImGuiContext.Instance.ActiveId;

        var io = ImGui.GetIO().NativePtr;

        // Always dispatch this event, for handling hovers.
        // Maybe it's a good idea to have a separate handler for setting which item is hovered, but anyway.
        this.DispatchMouseMove(io->MousePos, io->MouseDelta, false);

        var wheelDelta = new Vector2(io->MouseWheelH, io->MouseWheel);
        if (wheelDelta != Vector2.Zero)
        {
            if (this.DispatchMouseWheel(io->MousePos, io->MouseDelta, wheelDelta, false))
            {
                io->MouseWheelH = io->MouseWheel = 0;
            }
            else if (this.IsMouseHoveredIncludingChildren
                     && (
                            ImGuiInternals.ImGuiContext.Instance.HoveredIdPreviousFrameUsingMouseWheel == 1
                            || ImGuiInternals.ImGuiContext.Instance.ActiveIdUsingMouseWheel == 1))
            {
                var oldh = ImGuiInternals.ImGuiContext.Instance.HoveredIdPreviousFrameUsingMouseWheel;
                var olda = ImGuiInternals.ImGuiContext.Instance.ActiveIdUsingMouseWheel;
                ImGuiInternals.ImGuiContext.Instance.HoveredIdPreviousFrameUsingMouseWheel = 0;
                ImGuiInternals.ImGuiContext.Instance.ActiveIdUsingMouseWheel = 0;

                ImGuiInternals.ImGuiUpdateMouseWheel();

                ImGuiInternals.ImGuiContext.Instance.HoveredIdPreviousFrameUsingMouseWheel = oldh;
                ImGuiInternals.ImGuiContext.Instance.ActiveIdUsingMouseWheel = olda;
            }
        }

        for (var i = 0; i < (int)ImGuiMouseButton.COUNT; i++)
        {
            if (ImGui.IsMouseClicked((ImGuiMouseButton)i))
                this.DispatchMouseDown(io->MousePos, io->MouseDelta, (ImGuiMouseButton)i, false);
            else if (ImGui.IsMouseReleased((ImGuiMouseButton)i))
                this.DispatchMouseUp(io->MousePos, io->MouseDelta, (ImGuiMouseButton)i, false);
        }

        if (this.ImGuiWantRootKeyboardProcessing)
        {
            // This is, in fact, not a new vector
            // ReSharper disable once CollectionNeverUpdated.Local
            var inputQueueCharacters = new ImVectorWrapper<char>(&io->InputQueueCharacters);
            var inputEventsTrail = new ImVectorWrapper<ImGuiInternals.ImGuiInputEvent>(
                (ImVector*)Unsafe.AsPointer(ref ImGuiInternals.ImGuiContext.Instance.InputEventsTrail));

            inputQueueCharacters.Clear();
            io->WantTextInput = 1;

            foreach (ref var trailedEvent in inputEventsTrail.DataSpan)
            {
                switch (trailedEvent.Type)
                {
                    case ImGuiInternals.ImGuiInputEventType.Key:
                    {
                        if (trailedEvent.Key.Key is
                            ImGuiKey.Tab or ImGuiKey.Space or ImGuiKey.Enter or ImGuiKey.Escape or
                            ImGuiKey.LeftArrow or ImGuiKey.RightArrow or ImGuiKey.UpArrow or ImGuiKey.DownArrow or
                            ImGuiKey.GamepadFaceDown or ImGuiKey.GamepadFaceRight or
                            ImGuiKey.GamepadFaceLeft or ImGuiKey.GamepadFaceUp or
                            ImGuiKey.GamepadDpadLeft or ImGuiKey.GamepadDpadRight or
                            ImGuiKey.GamepadDpadUp or ImGuiKey.GamepadDpadDown or
                            ImGuiKey.GamepadL1 or ImGuiKey.GamepadR1 or
                            ImGuiKey.GamepadL2 or ImGuiKey.GamepadR2 or
                            ImGuiKey.GamepadL3 or ImGuiKey.GamepadR3 or
                            ImGuiKey.GamepadLStickLeft or ImGuiKey.GamepadLStickRight or
                            ImGuiKey.GamepadLStickUp or ImGuiKey.GamepadLStickDown)
                        {
                            if (this.TakeOverNavKey(trailedEvent.Key.Key))
                                ImGuiInternals.ImGuiNavMoveRequestCancel();
                            else
                                break;
                        }

                        if (trailedEvent.Key.Down != 0)
                            this.DispatchKeyDown(io->KeyMods, trailedEvent.Key.Key, false);
                        else
                            this.DispatchKeyUp(io->KeyMods, trailedEvent.Key.Key, false);
                        break;
                    }

                    case ImGuiInternals.ImGuiInputEventType.Text:
                    {
                        if (!Rune.TryCreate(trailedEvent.Text.Char, out var rune))
                            rune = Rune.ReplacementChar;

                        var handled = this.DispatchKeyPress(rune, false);
                        if (!handled)
                            inputQueueCharacters.Add((char)rune.Value);

                        break;
                    }
                }
            }
        }

        this.DispatchMiscMouseEvents(io->MousePos, io->MouseDelta);

        // If mouse capturing has been changed, let the new recipient(s) get a mouse move message.
        var newActive = ImGuiInternals.ImGuiContext.Instance.ActiveId;
        if (prevActive != 0 && newActive == prevActive)
            this.DispatchMouseMove(ImGui.GetIO().MousePos, Vector2.Zero, false);

        return;

        static void ImGuiItemAddRecursive(Spannable what)
        {
            what.ImGuiItemAdd();

            var children = what.GetAllChildSpannables();
            for (var i = children.Count - 1; i >= 0; i--)
            {
                if (children[i] is { } c)
                    ImGuiItemAddRecursive(c);
            }
        }
    }

    private void DispatchEffectivelyDisabled()
    {
        if (this.wasFocused && !this.ImGuiIsFocused)
        {
            this.wasFocused = false;
            var e = SpannableEventArgsPool.Rent<SpannableEventArgs>();
            e.Initialize(this, SpannableEventStep.DirectTarget);
            this.OnLostFocus(e);
            SpannableEventArgsPool.Return(e);
        }

        if (this.IsMouseHovered)
        {
            this.IsMouseHovered = false;
            var e = SpannableEventArgsPool.Rent<SpannableMouseEventArgs>();
            e.InitializeMouseEvent(new(float.PositiveInfinity), Vector2.Zero);
            e.Initialize(this, SpannableEventStep.DirectTarget);
            this.OnMouseLeave(e);
            SpannableEventArgsPool.Return(e);
        }

        if (this.wasCapturingMouse)
        {
            this.wasCapturingMouse = false;
            SpannableImGuiItem.ClearActive();
            ImGui.SetNextFrameWantCaptureMouse(false);
        }

        if (this.mouseCapturedButtonFlags != 0)
        {
            this.mouseCapturedButtonFlags = 0;
            this.clickTrackingLastClickTick.AsSpan().Clear();
            this.clickTrackingCumulativeCount.AsSpan().Clear();
            this.clickTrackingIsHeldDown.AsSpan().Clear();
            this.mousePressNextTick.AsSpan().Clear();
            this.mousePressCumulativeCount.AsSpan().Clear();
        }

        var children = this.GetAllChildSpannables();
        for (var i = children.Count - 1; i >= 0; i--)
            children[i]?.DispatchEffectivelyDisabled();
    }

    /// <summary>Places an ImGui item corresponding to this spannable.</summary>
    private void ImGuiItemAdd() =>
        SpannableImGuiItem.ItemAdd(
            this,
            this.selfInnerId,
            this.ImGuiHoverRect,
            this.ImGuiNavRect,
            this.ImGuiDisplayRect,
            false,
            this.ImGuiItemNoNav,
            this.ImGuiItemNoNavDefaultFocus,
            this.ImGuiItemDisabled);

    private void UpdateMouseCapture(bool shouldCapture)
    {
        if (this.wasCapturingMouse == shouldCapture)
            return;

        this.wasCapturingMouse = shouldCapture;
        if (shouldCapture)
        {
            SpannableImGuiItem.SetActive(this, this.selfInnerId, true);
            ImGui.SetNextFrameWantCaptureMouse(true);
        }
        else
        {
            SpannableImGuiItem.ClearActive();
            ImGui.SetNextFrameWantCaptureMouse(false);
        }
    }
}
