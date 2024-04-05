using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Interface.Utility;
using Dalamud.Utility;

using ImGuiNET;

namespace Dalamud.Interface.Spannables.Rendering;

/// <summary>
/// An initial render context.
/// </summary>
public readonly struct RenderContext
{
    /// <summary>The resolved ImGui global ID, or 0 if no ImGui state management is used.</summary>
    public readonly uint ImGuiGlobalId;

    /// <summary>Render scale.</summary>
    /// <remarks>Defaults to <see cref="ImGuiHelpers.GlobalScale"/>.</remarks>
    public readonly float RenderScale;

    /// <summary>Target draw list. Can be null.</summary>
    public readonly ImDrawListPtr DrawListPtr;

    /// <summary>Size of the spannable being rendered.</summary>
    /// <remarks>Default value is <c>new Vector2(ImGui.GetColumnWidth(), float.PositiveInfinity)</c>.</remarks>
    public readonly Vector2 Size;

    /// <summary>Offset to start drawing in screen offset.</summary>
    public readonly Vector2 ScreenOffset;

    /// <summary>Transformation matrix.</summary>
    /// <remarks>Defaults to <see cref="Matrix4x4.Identity"/>.</remarks>
    public readonly Matrix4x4 Transformation;

    /// <summary>Whether to put a dummy after rendering.</summary>
    public readonly bool PutDummyAfterRender;

    /// <summary>Whether to handle interactions.</summary>
    /// <remarks>Default value is to enable interaction handling. Will turn to <c>false</c> if <see cref="DrawListPtr"/>
    /// is empty.</remarks>
    public readonly bool UseInteraction;

    /// <summary>Initializes a new instance of the <see cref="RenderContext"/> struct.</summary>
    /// <param name="imGuiLabel">The ImGui label in UTF-16 for tracking interaction state.</param>
    /// <param name="options">The options.</param>
    public unsafe RenderContext(ReadOnlySpan<char> imGuiLabel, in Options options = default)
        : this(ImGui.GetWindowDrawList(), true, options)
    {
        if (imGuiLabel.IsEmpty)
            return;

        Span<byte> buf = stackalloc byte[Encoding.UTF8.GetByteCount(imGuiLabel)];
        Encoding.UTF8.GetBytes(imGuiLabel, buf);
        fixed (byte* p = buf)
            this.ImGuiGlobalId = ImGuiNative.igGetID_StrStr(p, p + buf.Length);
    }

    /// <summary>Initializes a new instance of the <see cref="RenderContext"/> struct.</summary>
    /// <param name="imGuiLabel">The ImGui label in UTF-8 for tracking interaction state.</param>
    /// <param name="options">The options.</param>
    public unsafe RenderContext(ReadOnlySpan<byte> imGuiLabel, in Options options = default)
        : this(ImGui.GetWindowDrawList(), true, options)
    {
        if (imGuiLabel.IsEmpty)
            return;

        fixed (byte* p = imGuiLabel)
            this.ImGuiGlobalId = ImGuiNative.igGetID_StrStr(p, p + imGuiLabel.Length);
    }

    /// <summary>Initializes a new instance of the <see cref="RenderContext"/> struct.</summary>
    /// <param name="imGuiId">The numeric ImGui ID.</param>
    /// <param name="options">The options.</param>
    public unsafe RenderContext(nint imGuiId, in Options options = default)
        : this(ImGui.GetWindowDrawList(), true, options)
    {
        if (imGuiId == nint.Zero)
            return;

        this.ImGuiGlobalId = ImGuiNative.igGetID_Ptr((void*)imGuiId);
    }

    /// <summary>Initializes a new instance of the <see cref="RenderContext"/> struct.</summary>
    /// <param name="measureOnly">Whether to only do a measure pass, without drawing.</param>
    /// <param name="options">The options.</param>
    public RenderContext(bool measureOnly, in Options options = default)
        : this(ImGui.GetWindowDrawList(), !measureOnly, options)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RenderContext"/> struct.</summary>
    /// <param name="drawListPtr">The pointer to the draw list.</param>
    /// <param name="options">The options.</param>
    public RenderContext(ImDrawListPtr drawListPtr, in Options options = default)
        : this(drawListPtr, false, options)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RenderContext"/> struct.</summary>
    /// <param name="drawListPtr">The target draw list.</param>
    /// <param name="putDummyAfterRender">Whether to put a dummy after render.</param>
    /// <param name="options">The options.</param>
    /// <returns>A reference of this instance after the initialize operation is completed.</returns>
    /// <exception cref="InvalidOperationException">Called outside the main thread. If called from the main thread,
    /// but not during the drawing context, the behavior is undefined and may crash.</exception>
    private RenderContext(ImDrawListPtr drawListPtr, bool putDummyAfterRender, in Options options)
    {
        ThreadSafety.DebugAssertMainThread();

        this.UseInteraction = options.UseLinks ?? true;
        this.Size = options.Size ?? new(ImGui.GetColumnWidth(), float.PositiveInfinity);
        this.RenderScale = options.Scale ?? ImGuiHelpers.GlobalScale;
        this.DrawListPtr = drawListPtr;
        this.PutDummyAfterRender = putDummyAfterRender;
        this.ScreenOffset = options.ScreenOffset ?? ImGui.GetCursorScreenPos();
        this.Transformation = options.Transformation ?? Matrix4x4.Identity;

        this.UseInteraction &= this.UseDrawing;
    }

    /// <summary>Gets a value indicating whether to actually draw.</summary>
    public unsafe bool UseDrawing
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.DrawListPtr.NativePtr is not null;
    }

    /// <summary>The initial options that may be set to <c>null</c> to use the default values.</summary>
    public struct Options
    {
        /// <inheritdoc cref="RenderContext.UseInteraction"/>
        public bool? UseLinks { get; set; }

        /// <inheritdoc cref="RenderContext.RenderScale"/>
        public float? Scale { get; set; }

        /// <inheritdoc cref="RenderContext.Size"/>
        public Vector2? Size { get; set; }

        /// <inheritdoc cref="RenderContext.ScreenOffset"/>
        public Vector2? ScreenOffset { get; set; }

        /// <inheritdoc cref="RenderContext.Transformation"/>
        public Matrix4x4? Transformation { get; set; }
    }
}