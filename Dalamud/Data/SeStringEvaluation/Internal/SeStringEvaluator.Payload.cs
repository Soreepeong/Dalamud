using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Interface.SeStringRenderer.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

using Lumina.Excel.GeneratedSheets2;
using Lumina.Text.Expressions;

namespace Dalamud.Data.SeStringEvaluation.Internal;

/// <summary>Evaluator for SeString components.</summary>
internal partial class SeStringEvaluator
{
    /// <inheritdoc cref="ISeStringEvaluator.ResolveStringPayload{TContext}"/>
    public unsafe bool ResolveStringPayload<TContext>(ref TContext context, SePayloadReadOnlySpan payload)
        where TContext : ISeStringContext
    {
        if (payload.IsInvalid)
            return false;

        if (context.HandlePayload(payload, ref context))
            return true;

        if (payload.IsText)
            return Produce(ref context, payload.Body);

        // Note: "x" means that nothing must come after. We ignore any extra expressions.
        switch (payload.MacroCode)
        {
            case MacroCode.SetResetTime:
            {
                DateTime date;
                if (payload.TryGetExpression(out var eHour, out var eWeekday)
                    && this.TryResolveInt(ref context, eHour, out var eHourVal)
                    && this.TryResolveInt(ref context, eWeekday, out var eWeekdayVal))
                {
                    var t = DateTime.UtcNow.AddDays(((eWeekdayVal - (int)DateTime.UtcNow.DayOfWeek) + 7) % 7);
                    date = new DateTime(t.Year, t.Month, t.Day, eHourVal, 0, 0, DateTimeKind.Utc).ToLocalTime();
                }
                else if (payload.TryGetExpression(out eHour)
                         && this.TryResolveInt(ref context, eHour, out eHourVal))
                {
                    var t = DateTime.UtcNow;
                    date = new DateTime(t.Year, t.Month, t.Day, eHourVal, 0, 0, DateTimeKind.Utc).ToLocalTime();
                }
                else
                {
                    return false;
                }

                context.UpdatePlaceholder((byte)ExpressionType.Millisecond, (uint)date.Millisecond);
                context.UpdatePlaceholder((byte)ExpressionType.Second, (uint)date.Second);
                context.UpdatePlaceholder((byte)ExpressionType.Minute, (uint)date.Minute);
                context.UpdatePlaceholder((byte)ExpressionType.Hour, (uint)date.Hour);
                context.UpdatePlaceholder((byte)ExpressionType.Day, (uint)date.Day);
                context.UpdatePlaceholder((byte)ExpressionType.Weekday, (uint)date.DayOfWeek);
                context.UpdatePlaceholder((byte)ExpressionType.Month, (uint)date.Month);
                context.UpdatePlaceholder((byte)ExpressionType.Year, (uint)date.Year);
                return true;
            }

            case MacroCode.SetTime:
            {
                if (payload.TryGetExpression(out var eTime)
                    && this.TryResolveUInt(ref context, eTime, out var eTimeVal))
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(eTimeVal).LocalDateTime;
                    context.UpdatePlaceholder((byte)ExpressionType.Millisecond, (uint)DateTime.Now.Millisecond); // ?
                    context.UpdatePlaceholder((byte)ExpressionType.Second, (uint)date.Second);
                    context.UpdatePlaceholder((byte)ExpressionType.Minute, (uint)date.Minute);
                    context.UpdatePlaceholder((byte)ExpressionType.Hour, (uint)date.Hour);
                    context.UpdatePlaceholder((byte)ExpressionType.Day, (uint)date.Day);
                    context.UpdatePlaceholder((byte)ExpressionType.Weekday, (uint)date.DayOfWeek);
                    context.UpdatePlaceholder((byte)ExpressionType.Month, (uint)date.Month);
                    context.UpdatePlaceholder((byte)ExpressionType.Year, (uint)date.Year);
                    return true;
                }

                return false;
            }

            case MacroCode.If:
            {
                return
                    payload.TryGetExpression(out var eCond, out var eTrue, out var eFalse)
                    && this.ResolveStringExpression(
                        ref context,
                        this.TryResolveBool(ref context, eCond, out var eCondVal) && eCondVal
                            ? eTrue
                            : eFalse);
            }

            case MacroCode.Switch:
            {
                var cond = -1;
                foreach (var e in payload)
                {
                    switch (cond)
                    {
                        case -1:
                            cond = this.TryResolveUInt(ref context, e, out var eVal) ? (int)eVal : 0;
                            break;
                        case > 1:
                            cond--;
                            break;
                        default:
                            return this.ResolveStringExpression(ref context, e);
                    }
                }

                return false;
            }

            case MacroCode.NewLine:
                context.ProduceNewLine();
                return true;

            case MacroCode.Icon:
            case MacroCode.Icon2: // ?
            {
                if (!payload.TryGetExpression(out var eIcon)
                    || !this.TryResolveUInt(ref context, eIcon, out var eIconValue))
                    return false;
                context.DrawIcon(eIconValue);
                return true;
            }

            case MacroCode.Color:
            {
                if (!payload.TryGetExpression(out var eColor))
                    return false;
                if (eColor.TryGetPlaceholderExpression(out var ph) && ph == (int)ExpressionType.StackColor)
                    context.PopForeColor();
                else if (this.TryResolveUInt(ref context, eColor, out var eColorVal))
                    context.PushForeColor(eColorVal | 0xFF000000u);
                return true;
            }

            case MacroCode.EdgeColor:
            {
                if (!payload.TryGetExpression(out var eColor))
                    return false;
                if (eColor.TryGetPlaceholderExpression(out var ph) && ph == (int)ExpressionType.StackColor)
                    context.PopBorderColor();
                else if (this.TryResolveUInt(ref context, eColor, out var eColorVal))
                    context.PushBorderColor(eColorVal | 0xFF000000u);
                return true;
            }

            case MacroCode.SoftHyphen:
                return Produce(ref context, "\u00AD"u8, "\u00AD");

            case MacroCode.Bold:
            {
                if (payload.TryGetExpression(out var eEnable)
                    && this.TryResolveBool(ref context, eEnable, out var eEnableVal))
                {
                    context.SetBold(eEnableVal);
                    return true;
                }

                return false;
            }

            case MacroCode.Italic:
            {
                if (payload.TryGetExpression(out var eEnable)
                    && this.TryResolveBool(ref context, eEnable, out var eEnableVal))
                {
                    context.SetItalic(eEnableVal);
                    return true;
                }

                return false;
            }

            case MacroCode.NonBreakingSpace:
                return Produce(ref context, "\u00A0"u8, "\u00A0");

            case MacroCode.Hyphen:
                return Produce(ref context, '-');

            case MacroCode.Num:
            {
                if (payload.TryGetExpression(out var eInt) && this.TryResolveInt(ref context, eInt, out var eIntVal))
                    return this.Produce(ref context, "{0}", eIntVal);
                return Produce(ref context, '0');
            }

            case MacroCode.Hex:
            {
                if (payload.TryGetExpression(out var eUInt) &&
                    this.TryResolveUInt(ref context, eUInt, out var eUIntVal))
                    return this.Produce(ref context, "0x{0:X08}", eUIntVal);
                return Produce(ref context, "0x00000000"u8, "0x00000000");
            }

            case MacroCode.Kilo:
            {
                if (payload.TryGetExpression(out var eInt, out var eSep)
                    && this.TryResolveInt(ref context, eInt, out var eIntVal))
                {
                    if (eIntVal == int.MinValue)
                    {
                        Produce(ref context, "-2"u8, "-2");
                        // 2147483648
                        this.ResolveStringExpression(ref context, eSep);
                        Produce(ref context, "147"u8, "147");
                        this.ResolveStringExpression(ref context, eSep);
                        Produce(ref context, "483"u8, "483");
                        this.ResolveStringExpression(ref context, eSep);
                        Produce(ref context, "648"u8, "648");
                        return true;
                    }

                    if (eIntVal < 0)
                    {
                        Produce(ref context, '-');
                        eIntVal = -eIntVal;
                    }

                    var anyDigitPrinted = false;
                    for (var i = 1_000_000_000; i > 0; i /= 10)
                    {
                        var digit = (eIntVal / i) % 10;
                        switch (anyDigitPrinted)
                        {
                            case false when digit == 0:
                                continue;
                            case true when i % 3 == 0:
                                this.ResolveStringExpression(ref context, eSep);
                                break;
                        }

                        anyDigitPrinted = Produce(ref context, (char)('0' + digit));
                    }

                    return true;
                }

                return Produce(ref context, '0');
            }

            case MacroCode.Sec:
            {
                if (payload.TryGetExpression(out var eInt) && this.TryResolveUInt(ref context, eInt, out var eIntVal))
                    return this.Produce(ref context, "{0:00}", eIntVal);
                return Produce(ref context, "00"u8, "00");
            }

            case MacroCode.Float:
            {
                if (!payload.TryGetExpression(out var eValue, out var eRadix, out var eSeparator)
                    || !this.TryResolveInt(ref context, eValue, out var eValueVal)
                    || !this.TryResolveInt(ref context, eRadix, out var eRadixVal))
                    return false;
                var (integerPart, fractionalPart) = int.DivRem(eValueVal, eRadixVal);
                if (fractionalPart < 0)
                {
                    integerPart--;
                    fractionalPart += eRadixVal;
                }

                this.Produce(ref context, "{0}", integerPart);
                this.ResolveStringExpression(ref context, eSeparator);

                // brain fried code
                Span<byte> fractionalDigits = stackalloc byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                var pos = fractionalDigits.Length - 1;
                for (var r = eRadixVal; r > 1; r /= 10)
                {
                    fractionalDigits[pos--] = (byte)('0' + (fractionalPart % 10));
                    fractionalPart /= 10;
                }

                Produce(ref context, fractionalDigits[(pos + 1)..]);
                return true;
            }

            case MacroCode.Sheet:
            {
                if (!payload.TryGetExpression(out var eSheetName, out var eRowId, out var eColIndex)
                    || !this.TryResolveUInt(ref context, eRowId, out var eRowIdValue)
                    || !this.TryResolveUInt(ref context, eColIndex, out var eColIndexValue)
                    || this.ExpressionToString(ref context, eSheetName) is not { } sheetName)
                    return false;

                return this.sheets[(int)context.SheetLanguage].ProduceFromSheet(
                    ref context,
                    sheetName,
                    eRowIdValue,
                    (int)eColIndexValue,
                    payload.Body[(eSheetName.Body.Length + eRowId.Body.Length + eColIndex.Body.Length)..]);
            }

            case MacroCode.String:
            {
                return payload.TryGetExpression(out var eStr) && this.ResolveStringExpression(ref context, eStr);
            }

            case MacroCode.ColorType:
            {
                if (payload.TryGetExpression(out var eColorType)
                    && this.TryResolveUInt(ref context, eColorType, out var eColorTypeVal))
                {
                    if (eColorTypeVal == 0)
                        context.PopForeColor();
                    else if (this.dataManager.GetExcelSheet<UIColor>()?.GetRow(eColorTypeVal) is { } row)
                        context.PushForeColor((row.UIForeground >> 8) | (row.UIForeground << 24));
                    return true;
                }

                return false;
            }

            case MacroCode.EdgeColorType:
            {
                if (payload.TryGetExpression(out var eColorType) &&
                    this.TryResolveUInt(ref context, eColorType, out var eColorTypeVal))
                {
                    if (eColorTypeVal == 0)
                        context.PopBorderColor();
                    else if (this.dataManager.GetExcelSheet<UIColor>()?.GetRow(eColorTypeVal) is { } row)
                        context.PushBorderColor((row.UIGlow >> 8) | (row.UIGlow << 24));
                    return true;
                }

                return false;
            }

            case MacroCode.LevelPos:
            {
                if (!payload.TryGetExpression(out var eLevel)
                    || !this.TryResolveUInt(ref context, eLevel, out var eLevelVal))
                    goto invalidLevelPos;
                if (this.sheets[(int)context.SheetLanguage].Level.GetRow(eLevelVal) is not { } level)
                    goto invalidLevelPos;
                if (level.Map.Value?.PlaceName.Value is not { } placeName)
                    goto invalidLevelPos;
                if (this.sheets[(int)context.SheetLanguage].Addon.GetRow(1637) is not { } levelFormatRow)
                    goto invalidLevelPos;

                var addonSeString = levelFormatRow.Text.RawData.AsSeStringSpan();
                var mapPosX = ConvertRawToMapPosX(level.Map.Value, level.X);
                var mapPosY = ConvertRawToMapPosY(level.Map.Value, level.Z); // Z is [sic]

                var paramLength = 0;
                paramLength += 1; // 0xFF
                paramLength += SeStringExpressionUtilities.CalculateLengthInt(placeName.Name.RawData.Length);
                paramLength += placeName.Name.RawData.Length;
                paramLength += SeStringExpressionUtilities.CalculateLengthUInt(mapPosX);
                paramLength += SeStringExpressionUtilities.CalculateLengthUInt(mapPosY);

                var subParams = stackalloc byte[paramLength];
                var subParamsSpan = new Span<byte>(subParams, paramLength);
                subParamsSpan = SeStringExpressionUtilities.WriteRaw(subParamsSpan, 0xFF);
                subParamsSpan = SeStringExpressionUtilities.EncodeInt(subParamsSpan, placeName.Name.RawData.Length);
                subParamsSpan = SeStringExpressionUtilities.WriteRaw(subParamsSpan, placeName.Name.RawData);
                subParamsSpan = SeStringExpressionUtilities.EncodeUInt(subParamsSpan, mapPosX);
                subParamsSpan = SeStringExpressionUtilities.EncodeUInt(subParamsSpan, mapPosY);
                if (!subParamsSpan.IsEmpty)
                    goto invalidLevelPos;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                fixed (TContext* pcontext = &context)
                {
                    var helper = new ParameterEvaluationHelper<TContext>(this, pcontext, subParams, paramLength);
                    return this.ResolveString(ref helper, addonSeString);
                }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

            invalidLevelPos:
                Produce(ref context, "??? ( ???  , ??? )");
                return false;

                // "41 0F BF C0 66 0F 6E D0 B8"
                static uint ConvertRawToMapPos(Map map, short offset, float value)
                {
                    var scale = map.SizeFactor / 100.0f;
                    return (uint)(10 - (int)(((((value + offset) * scale) + 1024f) * -0.2f) / scale));
                }

                static uint ConvertRawToMapPosX(Map map, float x)
                    => ConvertRawToMapPos(map, map.OffsetX, x);

                static uint ConvertRawToMapPosY(Map map, float y)
                    => ConvertRawToMapPos(map, map.OffsetY, y);
            }

            default:
                return false;
        }
    }
}
