using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Data.SeStringEvaluation.SeStringContext.Internal;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

using ImGuiNET;

using Lumina.Text.Expressions;

namespace Dalamud.Interface.SeStringRenderer.Internal;

/// <summary>A custom text renderer implementation.</summary>
internal sealed unsafe class SeStringRenderer : ISeStringRenderer
{
    [SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1313:Parameter names should begin with lower-case letter",
        Justification = "no")]
    private readonly record struct PayloadRangeToRenderCoordinates(
        int DataBegin,
        int DataEnd,
        int Line,
        float Left,
        float Right);

    [SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1313:Parameter names should begin with lower-case letter",
        Justification = "no")]
    private readonly record struct ByteOffsetToPayloadRange(
        int StreamIndex,
        int Offset,
        int DataBegin,
        int DataEnd)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int streamIndex, int offset) =>
            this.StreamIndex != streamIndex ? this.StreamIndex.CompareTo(streamIndex) : this.Offset.CompareTo(offset);
    }

    [SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1313:Parameter names should begin with lower-case letter",
        Justification = "no")]
    private readonly record struct ByteOffsetToDesignParam(
        int StreamIndex,
        int Offset,
        SeStringRendererDesignParams Param)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int streamIndex, int offset) =>
            this.StreamIndex != streamIndex ? this.StreamIndex.CompareTo(streamIndex) : this.Offset.CompareTo(offset);
    }

    [SuppressMessage(
        "StyleCop.CSharp.NamingRules",
        "SA1313:Parameter names should begin with lower-case letter",
        Justification = "no")]
    private readonly record struct ByteOffsetToGfdIcon(
        int StreamIndex,
        int Offset,
        uint IconId)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(int streamIndex, int offset) =>
            this.StreamIndex != streamIndex ? this.StreamIndex.CompareTo(streamIndex) : this.Offset.CompareTo(offset);
    }

    private const int ImFontFrequentKerningPairsMaxCodepoint = 128;
    private const int TotalChannels = 3;
    private const int ForeChannel = 2;
    private const int BorderChannel = 1;
    private const int BackChannel = 0;
    private const char IconSentinel = char.MaxValue;

    private static readonly BitArray WordBreakNormalBreakChars;

    private readonly SeStringRendererFactory seStringRendererFactory;

    private readonly List<MemoryStream> paragraphStreams = new();
    private readonly MemoryStream linkPayloadData = new();

    private readonly List<ByteOffsetToDesignParam> designParamsList = new();
    private readonly List<ByteOffsetToGfdIcon> iconList = new();
    private readonly List<ByteOffsetToPayloadRange> interactionOffsetList = new();
    private readonly List<PayloadRangeToRenderCoordinates> interactionRangeAccumulator = new();

    private Vector2 firstScreenOffset;
    private bool isRenderingItem;
    private uint globalId;

    private char lastChar;
    private ImDrawList* drawListPtr;
    private ImDrawListSplitter* splitterPtr;

    private bool rendered;

    static SeStringRenderer()
    {
        WordBreakNormalBreakChars = new(char.MaxValue + 1);

        // https://en.wikipedia.org/wiki/Whitespace_character
        foreach (var c in
                 "\t\n\v\f\r\x20\u0085\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2008\u2009\u200A\u2028\u2029\u205F\u3000\u180E\u200B\u200C\u200D")
            WordBreakNormalBreakChars[c] = true;

        foreach (var range in new[]
                 {
                     UnicodeRanges.HangulJamo,
                     UnicodeRanges.HangulSyllables,
                     UnicodeRanges.HangulCompatibilityJamo,
                     UnicodeRanges.HangulJamoExtendedA,
                     UnicodeRanges.HangulJamoExtendedB,
                     UnicodeRanges.CjkCompatibility,
                     UnicodeRanges.CjkCompatibilityForms,
                     UnicodeRanges.CjkCompatibilityIdeographs,
                     UnicodeRanges.CjkRadicalsSupplement,
                     UnicodeRanges.CjkSymbolsandPunctuation,
                     UnicodeRanges.CjkStrokes,
                     UnicodeRanges.CjkUnifiedIdeographs,
                     UnicodeRanges.CjkUnifiedIdeographsExtensionA,
                     UnicodeRanges.Hiragana,
                     UnicodeRanges.Katakana,
                     UnicodeRanges.KatakanaPhoneticExtensions,
                 })
        {
            for (var i = 0; i < range.Length; i++)
                WordBreakNormalBreakChars[range.FirstCodePoint + i] = true;
        }

        WordBreakNormalBreakChars[^1] = true;
    }

    /// <summary>Initializes a new instance of the <see cref="SeStringRenderer"/> class.</summary>
    /// <param name="seStringRendererFactory">Owner to return on <see cref="Dispose"/>.</param>
    public SeStringRenderer(SeStringRendererFactory seStringRendererFactory)
    {
        this.seStringRendererFactory = seStringRendererFactory;
        this.splitterPtr = ImGuiNative.ImDrawListSplitter_ImDrawListSplitter();
    }

    /// <inheritdoc/>
    public SeStringRendererParams Params { get; set; }

    /// <inheritdoc/>
    public SeStringRendererDesignParams DesignParams
    {
        get => this.designParamsList[^1].Param;
        set
        {
            if (this.designParamsList[^1].Param == value)
                return;

            var streamIndex = this.paragraphStreams.Count - 1;
            var offset = (int)this.paragraphStreams[^1].Length;
            if (this.designParamsList[^1].StreamIndex != streamIndex || this.designParamsList[^1].Offset != offset)
                this.designParamsList.Add(new(streamIndex, offset, value));
            else
                this.designParamsList[^1] = new(streamIndex, offset, value);
        }
    }

    /// <inheritdoc/>
    public SeStringRendererDesignParams WrapMarkerParams { get; set; }

    /// <summary>Gets the stack of foreground color overrides.</summary>
    /// <remarks>Purely for use from <see cref="seStringRendererFactory"/>.</remarks>
    internal Stack<uint> ForeColorStack { get; } = new();

    /// <summary>Gets the stack of border color overrides.</summary>
    /// <remarks>Purely for use from <see cref="seStringRendererFactory"/>.</remarks>
    internal Stack<uint> BorderColorStack { get; } = new();

    /// <summary>Gets the stack of background color overrides.</summary>
    /// <remarks>Purely for use from <see cref="seStringRendererFactory"/>.</remarks>
    internal Stack<uint> BackColorStack { get; } = new();

    /// <summary>Initializes this instance of the <see cref="SeStringRenderer"/> class.</summary>
    /// <param name="drawList">The draw list to render to.</param>
    /// <param name="isRenderingItem1">Put a <see cref="ImGui.Dummy"/> once <see cref="Dispose"/> is called.</param>
    /// <param name="globalId1">The global ImGuiID.</param>
    public void Initialize(ImDrawListPtr drawList, bool isRenderingItem1, uint globalId1)
    {
        ThreadSafety.DebugAssertMainThread();

        this.rendered = false;
        this.firstScreenOffset = ImGui.GetCursorScreenPos();
        this.drawListPtr = drawList;
        this.isRenderingItem = isRenderingItem1;
        this.globalId = globalId1;
        this.lastChar = '\0';

        this.interactionOffsetList.Add(new(0, 0, -1, -1));
        this.paragraphStreams.Add(this.seStringRendererFactory.RentMemoryStream());

        this.Params = SeStringRendererParams.FromCurrentImGuiContext();
        this.designParamsList.Add(new(0, 0, SeStringRendererDesignParams.FromCurrentImGuiContext()));
        this.WrapMarkerParams = this.designParamsList[^1].Param with
        {
            ForeColorU32 = SeStringRendererDesignParams.ApplyOpacity(
                this.designParamsList[^1].Param.ForeColorU32,
                0.5f),
        };
        if (this.drawListPtr is not null)
            ImGuiNative.ImDrawListSplitter_Split(this.splitterPtr, this.drawListPtr, TotalChannels);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.rendered)
            this.Render(out _, out _);

        this.interactionOffsetList.Clear();
        this.interactionRangeAccumulator.Clear();
        this.designParamsList.Clear();
        this.ForeColorStack.Clear();
        this.BorderColorStack.Clear();
        this.BackColorStack.Clear();
        this.iconList.Clear();
        this.linkPayloadData.Position = 0;
        this.linkPayloadData.SetLength(0);
        foreach (var at in this.paragraphStreams)
            this.seStringRendererFactory.Return(at);
        this.paragraphStreams.Clear();

        this.seStringRendererFactory.Return(this);
    }

    /// <summary>Clear the resources used by this instance.</summary>
    public void DisposeInternal()
    {
        if (this.splitterPtr is null)
            return;

        ImGuiNative.ImDrawListSplitter_destroy(this.splitterPtr);
        this.splitterPtr = null;
    }

    /// <inheritdoc/>
    public void AddNewLine(SeStringRendererParams.NewLineType newLineType)
    {
        if ((newLineType & this.Params.AcceptedNewLines) != 0)
            this.paragraphStreams.Add(this.seStringRendererFactory.RentMemoryStream());
    }

    /// <inheritdoc/>
    public void AddText(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
            return;

        var tms = this.Params.ControlCharactersDesignParams is not null
                      ? this.seStringRendererFactory.RentMemoryStream()
                      : null;
        while (!span.IsEmpty)
        {
            var i = 0;
            var newLineAfter = SeStringRendererParams.NewLineType.None;
            for (; i < span.Length; i++)
            {
                if (span[i] == '\r')
                {
                    if (i + 1 < span.Length && span[i + 1] == '\r')
                    {
                        newLineAfter = SeStringRendererParams.NewLineType.CrLf;
                        i += 2;
                        break;
                    }

                    newLineAfter = SeStringRendererParams.NewLineType.Cr;
                    i += 1;
                    break;
                }

                if (span[i] == '\n')
                {
                    newLineAfter = SeStringRendererParams.NewLineType.Lf;
                    i += 1;
                    break;
                }
            }

            var bc = Encoding.UTF8.GetByteCount(span[..i]);
            var ps = this.paragraphStreams[^1];
            var ms = tms ?? ps;
            var off = (int)ms.Length;
            ms.SetLength(off + bc);
            ms.Position += Encoding.UTF8.GetBytes(span[..i], ms.GetBuffer().AsSpan(off, bc));
            if (ms != ps)
                this.CopyTextWithDebugControlCharacters(ms.GetBuffer().AsSpan(off, bc), ps);

            span = span[i..];
            this.AddNewLine(newLineAfter);
        }
    }

    /// <inheritdoc/>
    public void AddText(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
            return;

        var tms = this.Params.ControlCharactersDesignParams is not null
                      ? this.seStringRendererFactory.RentMemoryStream()
                      : null;
        while (!span.IsEmpty)
        {
            var i = 0;
            var newLineAfter = SeStringRendererParams.NewLineType.None;
            for (; i < span.Length; i++)
            {
                if (span[i] == '\r')
                {
                    i += 1;
                    if (i < span.Length && span[i] == '\r')
                    {
                        i += 1;
                        newLineAfter = SeStringRendererParams.NewLineType.CrLf;
                    }
                    else
                    {
                        newLineAfter = SeStringRendererParams.NewLineType.Cr;
                    }

                    break;
                }

                if (span[i] == '\n')
                {
                    i += 1;
                    newLineAfter = SeStringRendererParams.NewLineType.Lf;
                    break;
                }
            }

            var ps = this.paragraphStreams[^1];
            if (tms is null)
                ps.Write(span[..i]);
            else
                this.CopyTextWithDebugControlCharacters(span[..i], ps);

            span = span[i..];
            this.AddNewLine(newLineAfter);
        }
    }

    /// <inheritdoc/>
    public void AddSeString(
        SeStringReadOnlySpan span,
        ISeStringContext? context = null)
    {
        var wrapper = new ManagedContextWrapper(context);
        try
        {
            this.AddSeString(span, ref wrapper);
        }
        finally
        {
            wrapper.Dispose();
        }
    }

    /// <inheritdoc/>
    public void AddSeString<TContext>(SeStringReadOnlySpan span, ref TContext context)
        where TContext : struct, ISeStringContext
    {
        var rp = this.Params;
        new SeStringDebuggableRenderer<TContext>(this, ref context).DrawString(span);
        this.Params = rp;
    }

    /// <inheritdoc/>
    public void AddGfdIcon(uint iconId)
    {
        this.iconList.Add(
            new(
                this.paragraphStreams.Count - 1,
                (int)this.paragraphStreams[^1].Length,
                iconId));

        // IconSentinel
        this.paragraphStreams[^1].Write("\uFFFF"u8);
    }

    /// <inheritdoc/>
    public void Render(out RenderState state) => this.Render(out state, out _);

    /// <inheritdoc/>
    public bool Render(out RenderState state, out SePayloadReadOnlySpan payload)
    {
        state = new()
        {
            StartScreenOffset = this.firstScreenOffset,
            Offset = new(
                0,
                (this.Params.ScaledLineHeight - this.Params.ScaledFontSize) * this.Params.LineVerticalOffsetRatio),
            BoundsLeftTop = new(float.MaxValue),
            BoundsRightBottom = new(float.MinValue),
            LastLineIndex = 0,
            ClickedMouseButton = unchecked((ImGuiMouseButton)(-1)),
        };

        if (this.rendered)
        {
            payload = default;
            return false;
        }

        var dps = CollectionsMarshal.AsSpan(this.designParamsList);
        var indexers = CollectionsMarshal.AsSpan(this.interactionOffsetList);
        var icons = CollectionsMarshal.AsSpan(this.iconList);
        for (var streamIndex = 0; streamIndex < this.paragraphStreams.Count; streamIndex++)
        {
            if (streamIndex > 0)
                this.BreakLineImmediate(ref state);

            var span = this.paragraphStreams[streamIndex].GetBuffer()
                           .AsSpan(0, (int)this.paragraphStreams[streamIndex].Length);
            var offset = 0;
            while (!span.IsEmpty)
            {
                var lastCharOffset = this.FindFirstLineBreak(ref state, span, dps, icons, streamIndex, offset);
                if (lastCharOffset > 0)
                {
                    var charRenderer = new CharRenderer(this, ref state);
                    var dpUpdated = true;
                    foreach (var c in span[..lastCharOffset].AsUtf8Enumerable())
                    {
                        while (dps.Length > 1 && dps[1].CompareTo(streamIndex, offset + c.Offset) <= 0)
                        {
                            dps = dps[1..];
                            dpUpdated = true;
                        }

                        while (indexers.Length > 1 && indexers[1].CompareTo(streamIndex, offset + c.Offset) <= 0)
                        {
                            this.AppendBackgroundRegion(ref state, ref charRenderer, indexers[0]);
                            indexers = indexers[1..];
                            dpUpdated = true;
                        }

                        if (dpUpdated)
                        {
                            charRenderer.UpdateDecorativeParams(dps[0].Param);
                            dpUpdated = false;
                        }

                        var chr = c.EffectiveChar;
                        var iconId = 0u;
                        if (c.Codepoint == IconSentinel)
                        {
                            while (!icons.IsEmpty && icons[0].CompareTo(streamIndex, offset + c.Offset) < 0)
                                icons = icons[1..];

                            if (!icons.IsEmpty
                                && icons[0].StreamIndex == streamIndex
                                && icons[0].Offset == offset + c.Offset)
                            {
                                iconId = icons[0].IconId;
                                chr = IconSentinel;
                            }
                        }

                        charRenderer.RenderChar(chr, iconId);
                    }

                    this.AppendBackgroundRegion(ref state, ref charRenderer, indexers[0]);
                    charRenderer.UpdateState();

                    span = span[lastCharOffset..];
                    offset += lastCharOffset;
                }

                if (span.IsEmpty)
                    break;

                this.RenderEllipsis(ref state, indexers[0]);
                if (this.Params.WordBreak == SeStringRendererParams.WordBreakType.KeepAll)
                    break;

                this.BreakLineImmediate(ref state);
            }
        }

        payload = default;
        if (this.drawListPtr is not null && this.isRenderingItem && this.Params.UseLink && this.globalId != 0)
        {
            var mouse = ImGui.GetMousePos();
            var mouseLine = (int)MathF.Floor((mouse.Y - this.firstScreenOffset.Y) / this.Params.ScaledLineHeight);
            var mouseRelativeX = mouse.X - this.firstScreenOffset.X;
            var linkPayloadDataBegin = -1;
            foreach (var entry in this.interactionRangeAccumulator)
            {
                if (entry.Line != mouseLine)
                    continue;
                if (entry.Left <= mouseRelativeX && mouseRelativeX < entry.Right)
                {
                    linkPayloadDataBegin = entry.DataBegin;
                    payload =
                        new(
                            this.linkPayloadData
                                .GetBuffer()
                                .AsSpan()[entry.DataBegin..entry.DataEnd]);
                    break;
                }
            }

            var lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var mmb = ImGui.IsMouseDown(ImGuiMouseButton.Middle);
            var rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
            ref var itemState = ref *(ItemStateStruct*)ImGui.GetStateStorage().GetVoidPtrRef(this.globalId, nint.Zero);
            if (itemState.IsMouseButtonDownHandled)
            {
                switch (itemState.FirstMouseButton)
                {
                    case ImGuiMouseButton.Left when !lmb && itemState.ActivePayloadOffset == linkPayloadDataBegin:
                        state.ClickedMouseButton = ImGuiMouseButton.Left;
                        itemState.IsMouseButtonDownHandled = false;
                        break;
                    case ImGuiMouseButton.Right when !rmb && itemState.ActivePayloadOffset == linkPayloadDataBegin:
                        state.ClickedMouseButton = ImGuiMouseButton.Right;
                        itemState.IsMouseButtonDownHandled = false;
                        break;
                    case ImGuiMouseButton.Middle when !mmb && itemState.ActivePayloadOffset == linkPayloadDataBegin:
                        state.ClickedMouseButton = ImGuiMouseButton.Middle;
                        itemState.IsMouseButtonDownHandled = false;
                        break;
                }

                if (!lmb && !rmb && !mmb)
                    itemState.IsMouseButtonDownHandled = false;
            }

            if (linkPayloadDataBegin == -1)
            {
                itemState.ActivePayloadOffset = -1;
            }
            else
            {
                ImGuiNative.ImDrawListSplitter_SetCurrentChannel(this.splitterPtr, this.drawListPtr, BackChannel);

                if (!itemState.IsMouseButtonDownHandled && (lmb || rmb || mmb))
                {
                    itemState.ActivePayloadOffset = linkPayloadDataBegin;
                    itemState.IsMouseButtonDownHandled = true;
                    itemState.FirstMouseButton = lmb ? ImGuiMouseButton.Left :
                                                 rmb ? ImGuiMouseButton.Right : ImGuiMouseButton.Middle;
                }

                var color =
                    itemState.IsMouseButtonDownHandled
                        ? itemState.ActivePayloadOffset == linkPayloadDataBegin
                              ? ImGui.GetColorU32(ImGuiCol.ButtonActive)
                              : 0u
                        : ImGui.GetColorU32(ImGuiCol.ButtonHovered);

                if (color != 0)
                {
                    var rounding = ImGui.GetStyle().FrameRounding;
                    foreach (var entry in this.interactionRangeAccumulator)
                    {
                        if (entry.DataBegin != linkPayloadDataBegin)
                            continue;
                        var lt = new Vector2(entry.Left, entry.Line * this.Params.ScaledLineHeight);
                        var rb = new Vector2(entry.Right, (entry.Line + 1) * this.Params.ScaledLineHeight);
                        ImGuiNative.ImDrawList_AddRectFilled(
                            this.drawListPtr,
                            this.firstScreenOffset + lt,
                            this.firstScreenOffset + rb,
                            color,
                            rounding,
                            ImDrawFlags.None);
                    }
                }
            }
        }

        if (this.drawListPtr is not null)
        {
            ImGuiNative.ImDrawListSplitter_Merge(this.splitterPtr, this.drawListPtr);
            this.drawListPtr = null;
        }

        if (this.isRenderingItem)
        {
            ImGui.SetCursorScreenPos(this.firstScreenOffset);
            if (state.BoundsRightBottom is { X: >= 0, Y: >= 0 })
                ImGui.Dummy(state.BoundsRightBottom);
        }

        this.rendered = true;
        return !payload.Envelope.IsEmpty;
    }

    /// <inheritdoc/>
    public void SetActiveLinkPayload(SePayloadReadOnlySpan payload)
    {
        if (payload.TryGetExpression(out var eLinkType)
            && eLinkType.TryGetInt(out var eLinkTypeValue)
            && eLinkTypeValue != 0xCE)
        {
            var begin = (int)this.linkPayloadData.Length;
            this.linkPayloadData.Write(payload);
            this.interactionOffsetList.Add(
                new(
                    this.paragraphStreams.Count - 1,
                    (int)this.paragraphStreams[^1].Length,
                    begin,
                    (int)this.linkPayloadData.Length));
        }
        else
        {
            this.interactionOffsetList.Add(
                new(
                    this.paragraphStreams.Count - 1,
                    (int)this.paragraphStreams[^1].Length,
                    -1,
                    -1));
        }
    }

    private void AppendBackgroundRegion(
        ref RenderState state,
        ref CharRenderer charRenderer,
        in ByteOffsetToPayloadRange indexer)
    {
        if (!(charRenderer.BoundsLeftTop.X > charRenderer.BoundsRightBottom.X)
            && !(charRenderer.BoundsLeftTop.Y > charRenderer.BoundsRightBottom.Y)
            && indexer.DataBegin != -1
            && indexer.DataEnd != -1)
        {
            if (this.interactionRangeAccumulator.Count > 0
                && this.interactionRangeAccumulator[^1] is var last
                && last.Line == state.LastLineIndex
                && last.DataBegin == indexer.DataBegin
                && last.DataEnd == indexer.DataEnd)
            {
                this.interactionRangeAccumulator[^1] =
                    last with
                    {
                        Line = last.Line,
                        Left = Math.Min(last.Left, charRenderer.BoundsLeftTop.X),
                        Right = Math.Max(last.Right, charRenderer.BoundsRightBottom.X),
                    };
            }
            else
            {
                this.interactionRangeAccumulator.Add(
                    new()
                    {
                        DataBegin = indexer.DataBegin,
                        DataEnd = indexer.DataEnd,
                        Line = state.LastLineIndex,
                        Left = charRenderer.BoundsLeftTop.X,
                        Right = charRenderer.BoundsRightBottom.X,
                    });
            }
        }

        state.BoundsLeftTop = Vector2.Min(state.BoundsLeftTop, charRenderer.BoundsLeftTop);
        state.BoundsRightBottom = Vector2.Max(state.BoundsRightBottom, charRenderer.BoundsRightBottom);
        charRenderer.BoundsLeftTop = new(float.MaxValue);
        charRenderer.BoundsRightBottom = new(float.MinValue);
    }

    /// <summary>Forces a line break.</summary>
    private void BreakLineImmediate(ref RenderState state)
    {
        var rp = this.Params;
        state.Offset = new(
            0,
            MathF.Round(
                (++state.LastLineIndex * rp.ScaledLineHeight)
                + ((rp.ScaledLineHeight - rp.ScaledFontSize) * rp.LineVerticalOffsetRatio)));
    }

    private int FindFirstLineBreak(
        ref RenderState state,
        ReadOnlySpan<byte> span,
        Span<ByteOffsetToDesignParam> dps,
        Span<ByteOffsetToGfdIcon> icons,
        int streamIndex,
        int offset)
    {
        var charWordBreaker = new CharWordBreaker(this, state);
        var dpUpdated = true;
        foreach (var c in span.AsUtf8Enumerable())
        {
            while (dps.Length > 1 && dps[1].CompareTo(streamIndex, offset + c.Offset) <= 0)
            {
                dps = dps[1..];
                dpUpdated = true;
            }

            if (dpUpdated)
            {
                charWordBreaker.UpdateDecorativeParams(dps[0].Param);
                dpUpdated = false;
            }

            var checkChar = c.EffectiveChar;
            var assumeWidth = -1f;
            if (c.Codepoint == IconSentinel)
            {
                while (!icons.IsEmpty && icons[0].CompareTo(streamIndex, offset + c.Offset) < 0)
                    icons = icons[1..];

                if (!icons.IsEmpty
                    && icons[0].StreamIndex == streamIndex
                    && icons[0].Offset == offset + c.Offset)
                {
                    if (this.seStringRendererFactory.GfdFileView.TryGetEntry(icons[0].IconId, out var entry))
                    {
                        checkChar = IconSentinel;
                        assumeWidth = MathF.Ceiling(
                            (this.Params.ScaledFontSize * this.Params.GraphicFontIconScale * entry.Width) /
                            entry.Height);
                    }
                }
            }

            var res = charWordBreaker.FindLineBreakOffset(c.Offset, c.Length, checkChar, assumeWidth);
            if (res == 0)
                return state.Offset.X == 0 ? 1 : 0;
            if (res != -1)
                return res;
        }

        return span.Length;
    }

    private void RenderEllipsis(ref RenderState state, ByteOffsetToPayloadRange indexer)
    {
        if (this.Params.WrapMarker is not { } ellipsis)
            return;

        var charRenderer = new CharRenderer(this, ref state);
        charRenderer.UpdateDecorativeParams(this.WrapMarkerParams);
        foreach (var c in ellipsis)
            charRenderer.RenderChar(c);
        this.AppendBackgroundRegion(ref state, ref charRenderer, indexer);
        charRenderer.UpdateState();
    }

    private void CopyTextWithDebugControlCharacters(ReadOnlySpan<byte> span, MemoryStream target)
    {
        if (this.Params.ControlCharactersDesignParams is not { } cdp)
        {
            target.Write(span);
            return;
        }

        foreach (var c in span.AsUtf8Enumerable())
        {
            if (c.Codepoint is <= char.MaxValue and >= 0 && char.IsControl((char)c.Codepoint))
            {
                var dp = this.DesignParams;
                this.DesignParams = cdp;
                switch ((char)c.Codepoint)
                {
                    case '\0':
                        target.Write("\\0"u8);
                        break;
                    case '\a':
                        target.Write("\\a"u8);
                        break;
                    case '\b':
                        target.Write("\\b"u8);
                        break;
                    case '\f':
                        target.Write("\\f"u8);
                        break;
                    case '\n':
                        target.Write("\\n"u8);
                        break;
                    case '\r':
                        target.Write("\\r"u8);
                        break;
                    case '\t':
                        target.Write("\\t"u8);
                        break;
                    case '\v':
                        target.Write("\\v"u8);
                        break;
                    case var t when t <= 0xFF:
                        target.WriteByte((byte)'\\');
                        target.WriteByte((byte)'x');
                        target.WriteByte("0123456789ABCDEF"u8[t >> 4]);
                        target.WriteByte("0123456789ABCDEF"u8[t & 15]);
                        break;
                    case var t:
                        target.WriteByte((byte)'\\');
                        target.WriteByte((byte)'u');
                        target.WriteByte("0123456789ABCDEF"u8[t >> 12]);
                        target.WriteByte("0123456789ABCDEF"u8[(t >> 8) & 15]);
                        target.WriteByte("0123456789ABCDEF"u8[(t >> 4) & 15]);
                        target.WriteByte("0123456789ABCDEF"u8[t & 15]);
                        break;
                }

                this.DesignParams = dp;
            }

            target.Write(span.Slice(c.Offset, c.Length));
        }
    }

    private struct CharWordBreaker
    {
        private readonly SeStringRendererParams rp;
        private readonly bool useKern;
        private readonly bool useEllipsis;
        private readonly float ellipsisWidth;
        private readonly float ellipsisX;

        private ImFontPtr font;
        private ImVectorWrapper<ImGuiHelpers.ImFontGlyphHotDataReal> hotData;
        private float topSkewDistance;
        private float scale;

        private float currX;
        private char lastChar;
        private int lastNormalBreakableOffset = -1;
        private bool breakOnFirstNormalBreakableOffset;
        private int lastEllipsisableOffset;

        public CharWordBreaker(SeStringRenderer renderer, in RenderState state)
        {
            this.currX = state.Offset.X;
            this.rp = renderer.Params;
            this.useKern = (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.NoKerning) == 0;
            this.lastChar = renderer.lastChar;

            if (renderer.Params.WrapMarker is { } ellipsis)
            {
                var wmdp = renderer.WrapMarkerParams;
                var wmFont = wmdp.EffectiveFont;
                var wmTopSkewDistance = wmdp.EffectiveIsFakeItalic ? this.rp.ScaledFontSize / 6 : 0;
                var wmHotData = wmFont.IndexedHotDataWrapped();
                var wmScale = this.rp.ScaledFontSize / wmFont.FontSize;
                this.useEllipsis = true;
                var lastEllipsisChar = '\0';
                foreach (var c in ellipsis)
                {
                    var codepoint = c >= wmHotData.Length ? wmFont.NativePtr->FallbackChar : c;
                    ref var hot = ref wmHotData[codepoint];

                    this.ellipsisX += this.GetScaledGap(wmFont, lastEllipsisChar, codepoint);
                    this.ellipsisWidth = Math.Max(
                        this.ellipsisWidth,
                        this.ellipsisX + (hot.OccupiedWidth * wmScale) + wmTopSkewDistance);
                    this.ellipsisX += hot.AdvanceX * wmScale;
                    lastEllipsisChar = c;
                }
            }
        }

        public void UpdateDecorativeParams(in SeStringRendererDesignParams dp)
        {
            this.font = dp.EffectiveFont;
            this.hotData = this.font.IndexedHotDataWrapped();
            this.topSkewDistance = dp.EffectiveIsFakeItalic ? this.rp.ScaledFontSize / 6 : 0;
            this.scale = this.rp.ScaledFontSize / this.font.FontSize;
        }

        public int FindLineBreakOffset(int offset, int num, char c, float assumeWidth = -1)
        {
            var breakable = WordBreakNormalBreakChars[c];
            if (this.breakOnFirstNormalBreakableOffset)
            {
                if (breakable)
                    return offset;
                return -1;
            }
            
            var rightBound = this.currX;
            if (c == IconSentinel && assumeWidth > 0)
            {
                rightBound += assumeWidth + this.topSkewDistance;
                this.currX += assumeWidth;
            }
            else if (c == '\t')
            {
                var tabWidth = this.rp.TabWidth;
                rightBound = MathF.Floor((rightBound + tabWidth) / tabWidth) * tabWidth;
                this.currX = rightBound;
            }
            else
            {
                var scaledGap = this.GetScaledGap(this.font, this.lastChar, c);
                c = c >= this.hotData.Length ? (char)this.font.NativePtr->FallbackChar : c;
                ref var hot = ref this.hotData[c];
                rightBound += scaledGap + (hot.OccupiedWidth * this.scale) + this.topSkewDistance;
                this.currX += scaledGap + (hot.AdvanceX * this.scale);
            }

            if (rightBound <= this.rp.LineWrapWidth)
            {
                if (rightBound + this.ellipsisWidth <= this.rp.LineWrapWidth)
                    this.lastEllipsisableOffset = offset + num;
                else
                    breakable = false;
            }
            else
            {
                switch (this.rp.WordBreak)
                {
                    case SeStringRendererParams.WordBreakType.Normal:
                        if (this.lastNormalBreakableOffset != -1)
                            return this.lastNormalBreakableOffset;

                        this.breakOnFirstNormalBreakableOffset = true;
                        this.lastChar = c;
                        return -1;

                    case SeStringRendererParams.WordBreakType.BreakAll:
                        return this.lastEllipsisableOffset;

                    case SeStringRendererParams.WordBreakType.BreakWord:
                        if (this.lastNormalBreakableOffset != -1)
                            return this.lastNormalBreakableOffset;
                        return this.lastEllipsisableOffset;

                    case SeStringRendererParams.WordBreakType.KeepAll:
                        if (this.useEllipsis)
                            return this.lastEllipsisableOffset;
                        break;
                }
            }

            this.lastChar = c;
            if (breakable)
                this.lastNormalBreakableOffset = offset + num;
            return -1;
        }

        private float GetScaledGap(ImFontPtr font1, int last, int current)
        {
            if (!this.useKern
                || last is < 0 or > ushort.MaxValue
                || current is < 0 or > ushort.MaxValue)
                return 0;

            var gap = ImGuiNative.ImFont_GetDistanceAdjustmentForPair(font1.NativePtr, (ushort)last, (ushort)current);
            return gap * (this.rp.ScaledFontSize / font1.FontSize);
        }
    }

    private ref struct CharRenderer
    {
        public Vector2 BoundsLeftTop;
        public Vector2 BoundsRightBottom;

        private readonly SeStringRenderer renderer;
        private readonly ref RenderState state;

        private readonly ImDrawList* drawListPtr;
        private readonly ImDrawListSplitter* splitterPtr;
        private readonly Vector2 firstScreenOffset;
        private readonly bool useKern;

        private ImFontPtr font;
        private ImVectorWrapper<ImGuiHelpers.ImFontGlyphHotDataReal> hotData;
        private ImVectorWrapper<ImGuiHelpers.ImFontGlyphReal> glyphs;
        private ImVectorWrapper<ushort> lookup;
        private float* frequentKerningPairsRawPtr;
        private bool fakeItalic;
        private float topSkewDistance;
        private int borderRange;
        private int numBorderDraws;
        private float scale;
        private uint backColor;
        private uint borderColor;
        private uint foreColor;
        private Vector2 offset;
        private int lastLineIndex;
        private char lastChar;

        public CharRenderer(SeStringRenderer renderer, ref RenderState state)
        {
            this.renderer = renderer;
            this.state = ref state;
            this.drawListPtr = renderer.drawListPtr;
            this.splitterPtr = renderer.splitterPtr;
            this.firstScreenOffset = renderer.firstScreenOffset;
            this.offset = state.Offset;
            this.BoundsLeftTop = new(float.MaxValue);
            this.BoundsRightBottom = new(float.MinValue);
            this.lastLineIndex = state.LastLineIndex;
            this.useKern = (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.NoKerning) == 0;
            this.lastChar = renderer.lastChar;
        }

        public void UpdateDecorativeParams(in SeStringRendererDesignParams dp)
        {
            this.font = dp.Italic && dp.FontItalic.IsNotNullAndLoaded()
                            ? dp.FontItalic
                            : dp.Font;
            this.fakeItalic = dp.Italic && !dp.FontItalic.IsNotNullAndLoaded();
            this.hotData = this.font.IndexedHotDataWrapped();
            this.glyphs = this.font.GlyphsWrapped();
            this.lookup = this.font.IndexLookupWrapped();
            this.frequentKerningPairsRawPtr = (float*)this.font.NativePtr->FrequentKerningPairs.Data;
            this.topSkewDistance = this.fakeItalic ? this.renderer.Params.ScaledFontSize / 6 : 0;
            this.scale = this.renderer.Params.ScaledFontSize / this.font.FontSize;
            this.borderRange = (int)dp.BorderWidth;
            this.numBorderDraws = (((2 * this.borderRange) + 1) * ((2 * this.borderRange) + 1)) - 1;
            if (this.renderer.drawListPtr is not null)
            {
                this.backColor = dp.BackColorU32 < 0x01000000u ? 0u : dp.BackColorU32;
                this.borderColor = dp.BorderColorU32 < 0x01000000u ? 0u : dp.BorderColorU32;
                this.foreColor = dp.ForeColorU32 < 0x01000000u ? 0u : dp.ForeColorU32;
            }
        }

        public void RenderChar(char c, uint iconId = 0u)
        {
            if (c is '\r' or '\n')
                return;

            var glyphIndex = this.lookup[c >= this.hotData.Length ? this.font.NativePtr->FallbackChar : c];
            if (glyphIndex == ushort.MaxValue)
                glyphIndex = this.lookup[this.font.NativePtr->FallbackChar];
            ref var glyph = ref this.glyphs[glyphIndex];
            ref var hot = ref this.hotData[glyph.Codepoint];

            var xy0 = glyph.XY0;
            var xy1 = glyph.XY1;
            var advX = glyph.AdvanceX;
            var ocw = hot.OccupiedWidth;
            var uv0 = glyph.UV0;
            var uv1 = glyph.UV1;
            var texId = this.font.ContainerAtlas.Textures[glyph.TextureIndex].TexID;

            if (c != IconSentinel)
                iconId = 0u;

            if (iconId != 0)
            {
                if (!this.renderer.seStringRendererFactory.GfdFileView.TryGetEntry(iconId, out var entry))
                {
                    this.RenderChar(c);
                    return;
                }

                xy0 = Vector2.Zero;
                var targetHeight =
                    MathF.Round(this.renderer.Params.ScaledFontSize * this.renderer.Params.GraphicFontIconScale);
                xy1 = new(advX = (entry.Width * targetHeight) / entry.Height, targetHeight);

                var offY = MathF.Round(
                    this.renderer.Params.GraphicFontIconVerticalOffsetRatio * this.renderer.Params.ScaledFontSize);
                xy0.Y += offY;
                xy1.Y += offY;

                var useHiRes = entry.Height < this.renderer.Params.ScaledFontSize;
                uv0 = new(entry.Left, entry.Top);
                uv1 = new(entry.Width, entry.Height);
                if (useHiRes)
                {
                    uv0 *= 2;
                    uv0.Y += 341;
                    uv1 *= 2;
                }

                uv1 += uv0;

                var icon = this.renderer.Params.GraphicFontIconMode;
                if (icon == -1 && Service<GameConfig>.Get().TryGet(
                        SystemConfigOption.PadSelectButtonIcon,
                        out uint iconTmp))
                    icon = (int)iconTmp;

                var tex = this.renderer.seStringRendererFactory.GfdTextures[
                    icon % this.renderer.seStringRendererFactory.GfdTextures.Length];
                uv0 /= tex.Size;
                uv1 /= tex.Size;
                texId = tex.ImGuiHandle;
            }
            else if (glyph.Codepoint == '\t')
            {
                var tabWidth = this.renderer.Params.TabWidth;
                var next = MathF.Floor((this.offset.X + tabWidth) / tabWidth) * tabWidth;
                advX = ocw = next - this.offset.X;
                xy0 = Vector2.Zero;
                xy1 = new(ocw, this.renderer.Params.ScaledFontSize);
            }
            else
            {
                xy0 *= this.scale;
                xy1 *= this.scale;
                advX *= this.scale;
                ocw *= this.scale;
                if (this.useKern)
                {
                    float gap;
                    if (hot.Count == 0)
                    {
                        gap = 0;
                    }
                    else if (this.lastChar < ImFontFrequentKerningPairsMaxCodepoint &&
                             glyph.Codepoint < ImFontFrequentKerningPairsMaxCodepoint)
                    {
                        gap = this.frequentKerningPairsRawPtr![
                            (this.lastChar * ImFontFrequentKerningPairsMaxCodepoint) + glyph.Codepoint];
                    }
                    else
                    {
                        gap = ImGuiNative.ImFont_GetDistanceAdjustmentForPairFromHotData(
                            this.font.NativePtr,
                            this.lastChar,
                            (ImFontGlyphHotData*)(this.hotData.Data + glyph.Codepoint));
                    }

                    this.offset.X += gap * this.scale;
                }
            }

            var glyphScreenOffset = this.firstScreenOffset + this.offset;
            var glyphVisible = iconId != 0 || (glyph.Visible && c is not ' ' and not '\t');

            if (this.backColor > 0)
            {
                ImGuiNative.ImDrawListSplitter_SetCurrentChannel(
                    this.splitterPtr,
                    this.drawListPtr,
                    BackChannel);

                ImGuiNative.ImDrawList_AddRectFilled(
                    this.drawListPtr,
                    glyphScreenOffset,
                    glyphScreenOffset + new Vector2(advX, this.renderer.Params.ScaledFontSize),
                    this.backColor,
                    0,
                    ImDrawFlags.None);
            }

            if (this.borderColor > 0 && glyphVisible)
            {
                ImGuiNative.ImDrawListSplitter_SetCurrentChannel(
                    this.splitterPtr,
                    this.drawListPtr,
                    BorderChannel);

                var push = texId != this.drawListPtr->_CmdHeader.TextureId;
                if (push)
                    ImGuiNative.ImDrawList_PushTextureID(this.drawListPtr, texId);

                var lt = glyphScreenOffset + xy0;
                var rb = glyphScreenOffset + xy1;
                var rt = new Vector2(rb.X, lt.Y);
                var lb = new Vector2(lt.X, rb.Y);
                if (this.fakeItalic)
                {
                    lt.X += this.topSkewDistance;
                    rt.X += this.topSkewDistance;
                }

                ImGuiNative.ImDrawList_PrimReserve(
                    this.drawListPtr,
                    6 * this.numBorderDraws,
                    4 * this.numBorderDraws);
                for (var x = -this.borderRange; x <= this.borderRange; x++)
                {
                    for (var y = -this.borderRange; y <= this.borderRange; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;
                        var v = new Vector2(x, y);
                        ImGuiNative.ImDrawList_PrimQuadUV(
                            this.drawListPtr,
                            lt + v,
                            rt + v,
                            rb + v,
                            lb + v,
                            uv0,
                            new(uv1.X, uv0.Y),
                            uv1,
                            new(uv0.X, uv1.Y),
                            this.borderColor);
                    }
                }

                if (push)
                    ImGuiNative.ImDrawList_PopTextureID(this.drawListPtr);
            }

            if (this.foreColor > 0 && glyphVisible)
            {
                ImGuiNative.ImDrawListSplitter_SetCurrentChannel(
                    this.splitterPtr,
                    this.drawListPtr,
                    ForeChannel);

                var push = texId != this.drawListPtr->_CmdHeader.TextureId;
                if (push)
                    ImGuiNative.ImDrawList_PushTextureID(this.drawListPtr, texId);
                ImGuiNative.ImDrawList_PrimReserve(this.drawListPtr, 6, 4);

                var lt = glyphScreenOffset + xy0;
                var rb = glyphScreenOffset + xy1;
                var rt = new Vector2(rb.X, lt.Y);
                var lb = new Vector2(lt.X, rb.Y);
                if (this.fakeItalic)
                {
                    lt.X += this.topSkewDistance;
                    rt.X += this.topSkewDistance;
                }

                ImGuiNative.ImDrawList_PrimQuadUV(
                    this.drawListPtr,
                    lt,
                    rt,
                    rb,
                    lb,
                    uv0,
                    new(uv1.X, uv0.Y),
                    uv1,
                    new(uv0.X, uv1.Y),
                    this.foreColor);

                if (push)
                    ImGuiNative.ImDrawList_PopTextureID(this.drawListPtr);
            }

            this.BoundsLeftTop = Vector2.Min(
                this.BoundsLeftTop,
                this.offset + xy0);
            this.BoundsRightBottom = Vector2.Max(
                this.BoundsRightBottom,
                this.offset + xy1 with { X = ocw } +
                new Vector2(this.topSkewDistance, 0));
            this.offset.X += advX;

            this.lastChar = c is var _ && char.IsControl(c) ? c : (char)glyph.Codepoint;
        }

        public void UpdateState()
        {
            this.renderer.lastChar = this.lastChar;
            this.state.Offset = this.offset;
            this.state.BoundsLeftTop = Vector2.Min(this.state.BoundsLeftTop, this.BoundsLeftTop);
            this.state.BoundsRightBottom = Vector2.Max(this.state.BoundsRightBottom, this.BoundsRightBottom);
            this.state.LastLineIndex = this.lastLineIndex;
        }
    }

    private readonly ref struct SeStringDebuggableRenderer<TContext>
        where TContext : ISeStringContext
    {
        private readonly SeStringRenderer renderer;
        private readonly ref TContext context;

        public SeStringDebuggableRenderer(SeStringRenderer renderer, ref TContext context)
        {
            this.renderer = renderer;
            this.context = ref context;
        }

        public void DrawString(ReadOnlySpan<byte> span)
        {
            foreach (var payload in span.AsSeStringSpan())
            {
                if (this.renderer.Params.SeStringPayloadsDesignParams is { } pdp && !payload.IsText)
                {
                    var dp = this.renderer.DesignParams;
                    this.renderer.DesignParams = pdp;

                    if (payload.IsInvalid)
                    {
                        this.renderer.AddText("<"u8);
                        this.DebugDumpSpan(payload);
                        this.renderer.AddText(">"u8);
                    }
                    else
                    {
                        this.renderer.AddText($"<{(MacroCode)payload.Type}(");
                        var exprc = 0;
                        foreach (var e in payload)
                        {
                            if (exprc++ > 0)
                                this.renderer.AddText(", "u8);
                            this.DebugDrawExpression(e, dp);
                        }

                        this.renderer.AddText(")>"u8);
                    }

                    this.renderer.DesignParams = dp;
                }

                // This also handles drawing text payloads.
                this.renderer.seStringRendererFactory.DefaultResolvePayload(this.renderer, payload, ref this.context);
            }
        }

        private void DebugDrawExpression(SeExpressionReadOnlySpan e, in SeStringRendererDesignParams strdp)
        {
            if (e.TryGetUInt(out var intVal))
            {
                this.renderer.AddText($"{intVal}");
            }
            else if (e.TryGetString(out var s))
            {
                this.renderer.AddText("str{"u8);
                var dp = this.renderer.DesignParams;
                this.renderer.DesignParams = strdp;
                this.DrawString(s);
                this.renderer.DesignParams = dp;
                this.renderer.AddText("}"u8);
            }
            else if (e.TryGetPlaceholderExpression(out var op))
            {
                this.renderer.AddText(
                    (ExpressionType)op switch
                    {
                        ExpressionType.Millisecond => "t_msec{}",
                        ExpressionType.Second => "t_sec{}",
                        ExpressionType.Minute => "t_min{}",
                        ExpressionType.Hour => "t_hour{}",
                        ExpressionType.Day => "t_day{}",
                        ExpressionType.Weekday => "t_wday{}",
                        ExpressionType.Month => "t_mon{}",
                        ExpressionType.Year => "t_year{}",
                        ExpressionType.StackColor => "StackColor{}",
                        _ => $"Nullary{op:X02}{{}}",
                    });
            }
            else if (e.TryGetParameterExpression(out op, out var operand1))
            {
                this.renderer.AddText(
                    (ExpressionType)op switch
                    {
                        ExpressionType.IntegerParameter => "lnum{",
                        ExpressionType.PlayerParameter => "gnum{",
                        ExpressionType.StringParameter => "lstr{",
                        ExpressionType.ObjectParameter => "gstr{",
                        _ => $"Unary{op:X02}{{",
                    });
                this.DebugDrawExpression(operand1, strdp);
                this.renderer.AddText("}"u8);
            }
            else if (e.TryGetBinaryExpression(out op, out operand1, out var operand2))
            {
                this.renderer.AddText(
                    (ExpressionType)op switch
                    {
                        ExpressionType.GreaterThanOrEqualTo => "gteq{",
                        ExpressionType.GreaterThan => "gt{",
                        ExpressionType.LessThanOrEqualTo => "lteq{",
                        ExpressionType.LessThan => "lt{",
                        ExpressionType.Equal => "eq{",
                        ExpressionType.NotEqual => "neq{",
                        _ => $"Binary{op:X02}{{",
                    });
                this.DebugDrawExpression(operand1, strdp);
                this.renderer.AddText(", "u8);
                this.DebugDrawExpression(operand2, strdp);
                this.renderer.AddText("}"u8);
            }
            else
            {
                this.renderer.AddText("invalid{"u8);
                this.DebugDumpSpan(e);
                this.renderer.AddText("}"u8);
            }
        }

        private void DebugDumpSpan(ReadOnlySpan<byte> data)
        {
            Span<byte> buf = stackalloc byte[2];
            buf[0] = (byte)' ';
            for (var i = 0; i < data.Length; i++)
            {
                buf[0] = "0123456789ABCDEF"u8[data[i] >> 4];
                buf[1] = "0123456789ABCDEF"u8[data[i] & 15];
                this.renderer.AddText(i == 0 ? buf : buf[1..]);
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct ItemStateStruct
    {
        [FieldOffset(0)]
        public int ActivePayloadOffset;

        [FieldOffset(4)]
        public uint Flags;

        public bool IsMouseButtonDownHandled
        {
            readonly get => (this.Flags & 1) != 0;
            set => this.Flags = (this.Flags & ~1u) | (value ? 1u : 0u);
        }

        public ImGuiMouseButton FirstMouseButton
        {
            readonly get => (ImGuiMouseButton)((this.Flags >> 1) & 3);
            set => this.Flags = (this.Flags & ~(3u << 1)) | ((uint)value << 1);
        }
    }
}
