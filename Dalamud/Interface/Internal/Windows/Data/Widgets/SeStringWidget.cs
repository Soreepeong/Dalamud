using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Data;
using Dalamud.Data.SeStringEvaluation.Internal;
using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Interface.SeStringRenderer;
using Dalamud.Interface.SeStringRenderer.Internal;

using ImGuiNET;

using Lumina.Excel.GeneratedSheets2;
using Lumina.Text.Expressions;

using Serilog;

namespace Dalamud.Interface.Internal.Windows.Data.Widgets;

/// <summary>Widget for displaying SeString test.</summary>
internal class SeStringWidget : IDataWindowWidget, IDisposable
{
    private static readonly string[] WordBreakModeNames =
    {
        "Normal",
        "Break All",
        "Keep All",
        "Break Word",
    };

    private int wordBreakMode;

    private SeString seString = null!;
    private bool seStringDebug;
    private bool controlCharacterDebug;
    private byte[] seStringEncoded = null!;
    private uint[]? addonIndexToRowId;
    private List<byte[]?>? capturedAddonStrings;

    private ImGuiListClipperPtr clipperPtr;

    /// <inheritdoc/>
    public string[]? CommandShortcuts { get; init; } = { "sestringwidget" };

    /// <inheritdoc/>
    public string DisplayName { get; init; } = "SeString";

    /// <inheritdoc/>
    public bool Ready { get; set; }

    private SeStringRendererParams CurrentRenderParams => SeStringRendererParams.FromCurrentImGuiContext() with
    {
        LineWrapWidth = ImGui.GetContentRegionAvail().X,
        WordBreak = (SeStringRendererParams.WordBreakType)this.wordBreakMode,
        SeStringPayloadsDesignParams = this.seStringDebug || ImGui.IsKeyDown(ImGuiKey.LeftCtrl)
                                           ? SeStringRendererDesignParams.FromCurrentImGuiContext() with
                                           {
                                               BackColorU32 = 0xFF003300,
                                           }
                                           : null,
        ControlCharactersDesignParams = this.controlCharacterDebug || ImGui.IsKeyDown(ImGuiKey.LeftShift)
                                            ? SeStringRendererDesignParams.FromCurrentImGuiContext() with
                                            {
                                                BackColorU32 = 0xFF330000,
                                            }
                                            : null,
        TabWidth = ImGui.CalcTextSize("00000000").X,
    };

    /// <inheritdoc/>
    public void Load()
    {
        unsafe
        {
            if (this.clipperPtr.NativePtr is null)
                this.clipperPtr = ImGuiNative.ImGuiListClipper_ImGuiListClipper();
        }

        this.addonIndexToRowId = null;
        this.capturedAddonStrings = null;

        var ssb = new SeStringBuilder();
        for (var i = 1; i <= 12; i++)
        {
            ssb.AddText($"{i - 1}(");
            ssb.Add(
                new RawPayload(
                    new byte[] { 2, (byte)MacroCode.String, 3, (byte)ExpressionType.ObjectParameter, (byte)i, 3 }));
            ssb.AddText(") ");
        }

        for (var i = 13; i <= 65; i++)
        {
            if (i == 55)
                continue;
            ssb.Add(
                new RawPayload(
                    new byte[] { 2, (byte)MacroCode.Color, 3, (byte)ExpressionType.PlayerParameter, (byte)i, 3 }));
            ssb.AddText($"color{i - 1}(");
            ssb.Add(
                new RawPayload(
                    new byte[] { 2, (byte)MacroCode.Hex, 3, (byte)ExpressionType.PlayerParameter, (byte)i, 3 }));
            ssb.AddText(") ");
            ssb.Add(new RawPayload(new byte[] { 2, (byte)MacroCode.Color, 2, (byte)ExpressionType.StackColor, 3 }));
        }

        ssb.Add(NewLinePayload.Payload);

        ssb.AddText("Plain");
        ssb.Add(new RawPayload(new byte[] { 2, (byte)MacroCode.NonBreakingSpace, 1, 3 }));
        ssb.AddItalics("Italic");
        ssb.Add(new RawPayload(new byte[] { 2, (byte)MacroCode.Hyphen, 1, 3 }));
        ssb.AddUiForeground(504);
        ssb.AddUiGlow(504);
        ssb.AddText("Glow");
        ssb.Add(new RawPayload(new byte[] { 2, (byte)MacroCode.SoftHyphen, 1, 3 }));
        ssb.AddText("504");
        ssb.AddUiGlowOff();
        ssb.AddUiForegroundOff();

        ssb.Add(NewLinePayload.Payload);
        ssb.Add(NewLinePayload.Payload);
        ssb.Add(new DalamudLinkPayload { CommandId = 12345, Plugin = "InvalidPlugin" });
        ssb.AddText("Link");
        ssb.Add(NewLinePayload.Payload);
        ssb.AddItalics("Link+Italic");
        ssb.Add(NewLinePayload.Payload);
        ssb.AddUiForeground(57);
        ssb.AddUiGlow(57);
        ssb.AddItalics("Link+Glow 57");
        ssb.AddUiGlowOff();
        ssb.AddUiForegroundOff();
        ssb.Add(RawPayload.LinkTerminator);

        ssb.Add(NewLinePayload.Payload);
        ssb.AddText("(((");
        ssb.Add(new DalamudLinkPayload { CommandId = 6544665, Plugin = "Asdf" });
        ssb.AddText("Link");
        ssb.Add(RawPayload.LinkTerminator);
        ssb.AddText(")))");
        ssb.Add(NewLinePayload.Payload);

        ssb.Add(NewLinePayload.Payload);
        ssb.Add(NewLinePayload.Payload);
        ssb.AddText("Word Break Test\n");
        ssb.AddText(
            "glkniohael 5yhaeil5u yhㅁㄷ ㅓ6교ㅓnaerluyhaerulyhakgnad.lㄱㄷㅇㅎ ㅇㄹ ㅗㅠㅠㅇㅋㄱ ㅎkfnaeurkghdclkn gvsㄷ5며ㅛㅗ술dlk slkghhoiaer gadrgnakejlyh er5;lyhaeynouyaeㅈㅁ4ㄱㅎㅈㄱ륭ㅍ5iog;ngvfkdnb ksjf hnl oyi5yhaoityjhae4oitaoeirfgl;ai g;aerghaeril aeih ;aer haerh ;oeta h;aer5, yhoaejhnedrtkhjnsert6o;nsriyhumrp0y8h5 hmgperih nsl;kjhse;o bhselbhnsetp jnsrk jert h가나다ㅐㅏㅁ3ㅗ5ㅛ댜ㅣㅁㄴ5 후sletk hnbrsil gbhsekhgrdㅈㄴ슈ㅑㅣㅠㅛㅡㅎ샼젹ㄴ 휸ㄷ45ㅑㅕㅗㅠㄱ냐;ㅐㅕㅀㅍ ");

        this.seString = ssb.Build();
        this.seStringEncoded = this.seString.Encode();

        this.Ready = true;
    }

    /// <inheritdoc/>
    public unsafe void Dispose()
    {
        if (this.clipperPtr.NativePtr is not null)
        {
            this.clipperPtr.Destroy();
            this.clipperPtr = default;
        }
    }

    /// <inheritdoc/>
    public void Draw()
    {
        ImGui.Combo("Word Break", ref this.wordBreakMode, WordBreakModeNames, WordBreakModeNames.Length);
        ImGui.Checkbox("Show Payload Dump", ref this.seStringDebug);
        ImGui.Checkbox("Show Control Characters", ref this.controlCharacterDebug);

        if (ImGui.CollapsingHeader("AddonTest"))
        {
            var context = new DelegateSeStringContext(
                Service<DataManager>.Get().Language,
                uintDelegate: static parameterIndex => parameterIndex + 1,
                stringDelegate: static (paramIndex, arg) =>
                {
                    arg.ProduceSeString(
                        new byte[] { 0x02, 0x13, 0x06, 0xFE, 0xFF, 0x50, 0xFF, 0x50, 0x03 });
                    arg.ProduceString($"(arg{paramIndex})");
                    arg.ProduceSeString(new byte[] { 0x02, 0x13, 0x02, 0xEC, 0x03 });
                    return true;
                },
                handlePayloadDelegate: HandlePayload,
                recursiveEvaluator: Service<SeStringEvaluator>.Get());

            var sheet = Service<DataManager>.Get().GetExcelSheet<Addon>()!;
            if (this.addonIndexToRowId is null)
            {
                this.addonIndexToRowId = new uint[sheet.RowCount];
                var i = 0;
                foreach (var row in sheet)
                    this.addonIndexToRowId[i++] = row.RowId;
            }

            var rendererParams = this.CurrentRenderParams with
            {
                WordBreak = SeStringRendererParams.WordBreakType.KeepAll,
                AcceptedNewLines = SeStringRendererParams.NewLineType.None,
            };

            this.capturedAddonStrings ??= new();
            this.clipperPtr.Begin(this.addonIndexToRowId.Length, ImGui.GetTextLineHeight());
            var basePos = ImGui.GetCursorPos();
            while (this.clipperPtr.Step())
            {
                for (var i = this.clipperPtr.DisplayStart; i < this.clipperPtr.DisplayEnd; i++)
                {
                    ImGui.SetCursorPos(basePos + new Vector2(0, i * ImGui.GetTextLineHeight()));
                    using var ctr = Service<SeStringRendererFactory>.Get().RentAsItem("AddonTestLinkLabel");
                    ctr.Params = rendererParams;
                    ctr.AddText($"{this.addonIndexToRowId[i]}:\t");

                    while (this.capturedAddonStrings.Count <= i)
                        this.capturedAddonStrings.Add(null);

                    ref var ss = ref CollectionsMarshal.AsSpan(this.capturedAddonStrings)[i];
                    var row = sheet.GetRow(this.addonIndexToRowId[i]);
                    ss ??= row is not null
                               ? Service<SeStringEvaluator>.Get().CaptureParameters(ref context, row.Text.RawData)
                               : Array.Empty<byte>();

                    ctr.AddSeString(ss, context);
                    ctr.Render(out var state);

                    var mouseRel = ImGui.GetMousePos() - state.StartScreenOffset;
                    if (mouseRel.X >= state.BoundsLeftTop.X
                        && mouseRel.Y >= state.BoundsLeftTop.Y
                        && mouseRel.X < state.BoundsRightBottom.X
                        && mouseRel.Y < state.BoundsRightBottom.Y)
                    {
                        ImGui.BeginTooltip();
                        using (var ctr2 = Service<SeStringRendererFactory>.Get().RentAsDummy())
                        {
                            ctr2.Params = this.CurrentRenderParams with
                            {
                                LineWrapWidth = rendererParams.LineWrapWidth,
                            };
                            if (row is not null)
                                ctr2.AddSeString(row.Text.RawData, context);
                        }

                        ImGui.EndTooltip();
                    }
                }
            }

            this.clipperPtr.End();
        }

        if (ImGui.CollapsingHeader("LogKindTest"))
        {
            using var ctr = Service<SeStringRendererFactory>.Get().RentAsItem("LogKindTestLinkLabel");
            ctr.Params = this.CurrentRenderParams with
            {
                TabWidth = ImGui.CalcTextSize("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA").X,
            };
            foreach (var row in Service<DataManager>.Get().GetExcelSheet<LogKind>()!)
            {
                if (row.Format.RawData.IsEmpty)
                    continue;
                ctr.AddText($"{row.RowId}: ");
                ctr.AddSeString(
                    row.Format.RawData,
                    new DelegateSeStringContext(
                        Service<DataManager>.Get().Language,
                        stringDelegate: static (paramIndex, arg) =>
                        {
                            arg.ProduceString(
                                paramIndex switch
                                {
                                    0 => "Firstname Lastname",
                                    1 => "Message",
                                    _ => $"Unexpected{paramIndex}",
                                });
                            return true;
                        }));
                ctr.AddText("\t");
            }

            if (ctr.Render(out var state, out var payload))
            {
                ImGui.SetTooltip(Payload.Decode(new(new MemoryStream(payload.Envelope.ToArray()))).ToString());
                if (state.ClickedMouseButton is ImGuiMouseButton.Left)
                {
                    Log.Information(
                        "Payload interacted: {payload}",
                        Payload.Decode(new(new MemoryStream(payload.Envelope.ToArray()))));
                }
            }
        }

        if (ImGui.CollapsingHeader("MiscTest"))
        {
            using var ctr = Service<SeStringRendererFactory>.Get().RentAsItem("MiscTestLinkLabel");
            ctr.Params = this.CurrentRenderParams;
            ctr.AddSeString(this.seStringEncoded);
            if (ctr.Render(out var state, out var payload))
            {
                ImGui.SetTooltip(Payload.Decode(new(new MemoryStream(payload.Envelope.ToArray()))).ToString());
                if (state.ClickedMouseButton is ImGuiMouseButton.Left)
                {
                    Log.Information(
                        "Payload interacted: {payload}",
                        Payload.Decode(new(new MemoryStream(payload.Envelope.ToArray()))));
                }
            }
        }
    }

    private static bool HandlePayload(
        SePayloadReadOnlySpan payload,
        DelegateSeStringContext.DelegateSeStringCallbackArg args)
    {
        switch (payload.MacroCode)
        {
            case MacroCode.SetTime:
            {
                var now = DateTime.Now;
                args.Context.UpdatePlaceholder((byte)ExpressionType.Millisecond, (uint)now.Millisecond);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Second, (uint)now.Second);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Minute, (uint)now.Minute);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Hour, (uint)now.Hour);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Day, (uint)now.Day);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Weekday, (uint)now.DayOfWeek);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Month, (uint)now.Month);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Year, (uint)now.Year);
                return true;
            }

            case MacroCode.SetResetTime:
            {
                DateTime date;
                if (payload.TryGetExpression(out var eHour, out var eWeekday)
                    && args.Context.RecursiveEvaluator?.TryResolveInt(ref args.Context, eHour, out var eHourVal) is true
                    && args.Context.RecursiveEvaluator?.TryResolveInt(ref args.Context, eWeekday, out var eWeekdayVal) is true)
                {
                    var t = DateTime.UtcNow.AddDays(((eWeekdayVal - (int)DateTime.UtcNow.DayOfWeek) + 7) % 7);
                    date = new DateTime(t.Year, t.Month, t.Day, eHourVal, 0, 0, DateTimeKind.Utc).ToLocalTime();
                }
                else if (payload.TryGetExpression(out eHour)
                         && args.Context.RecursiveEvaluator?.TryResolveInt(ref args.Context, eHour, out eHourVal) is true)
                {
                    var t = DateTime.UtcNow;
                    date = new DateTime(t.Year, t.Month, t.Day, eHourVal, 0, 0, DateTimeKind.Utc).ToLocalTime();
                }
                else
                {
                    return false;
                }

                args.Context.UpdatePlaceholder((byte)ExpressionType.Millisecond, (uint)date.Millisecond);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Second, (uint)date.Second);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Minute, (uint)date.Minute);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Hour, (uint)date.Hour);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Day, (uint)date.Day);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Weekday, (uint)date.DayOfWeek);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Month, (uint)date.Month);
                args.Context.UpdatePlaceholder((byte)ExpressionType.Year, (uint)date.Year);
                return true;
            }

            default:
                return false;
        }
    }
}
