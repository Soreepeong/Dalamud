using Dalamud.Game.Text.MutableSeStringHandling.Payloads;
using Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros;
using Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Conditionals;
using Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ContextModifiers;
using Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.ObjectProducers;
using Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.Singletons;
using Dalamud.Game.Text.MutableSeStringHandling.Payloads.Macros.TextProducers;

using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

using TextPayload = Dalamud.Game.Text.MutableSeStringHandling.Payloads.TextPayload;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>Utilities for <see cref="IMutableSePayload"/>.</summary>
public static unsafe class MutableSePayload
{
    /// <summary>Gets a SeString payload at the given memory address.</summary>
    /// <param name="sz">Pointer to a SeString payload.</param>
    /// <returns>The payload, or <c>null</c> if none was found.</returns>
    public static IMutableSePayload? From(byte* sz)
    {
        foreach (var p in new ReadOnlySeStringSpan(sz))
            return FromLumina(p);
        return null;
    }

    /// <summary>Gets the first payload in the given bytes.</summary>
    /// <param name="bytes">Bytes to look for payloads.</param>
    /// <returns>The payload, or <c>null</c> if none was found.</returns>
    public static IMutableSePayload? From(ReadOnlySpan<byte> bytes)
    {
        foreach (var p in new ReadOnlySeStringSpan(bytes))
            return FromLumina(p);
        return null;
    }

    /// <summary>Gets a mutable payload from the given Lumina payload.</summary>
    /// <param name="payload">The payload to convert.</param>
    /// <returns>The converted payload.</returns>
    public static IMutableSePayload FromLumina(BasePayload payload) =>
        payload.IsTextPayload ? FromText(payload.RawString) : From(payload.Data) ?? new TextPayload();

    /// <summary>Gets a mutable payload from the given Lumina payload.</summary>
    /// <param name="payload">The payload to convert.</param>
    /// <returns>The converted payload.</returns>
    public static IMutableSePayload FromLumina(ReadOnlySePayload payload) =>
        FromLumina(payload.AsSpan());

    /// <summary>Gets a mutable payload from the given Lumina payload.</summary>
    /// <param name="payload">The payload to convert.</param>
    /// <returns>The converted payload.</returns>
    public static IMutableSePayload FromLumina(ReadOnlySePayloadSpan payload)
    {
        switch (payload.Type)
        {
            case ReadOnlySePayloadType.Invalid:
            default:
                return new InvalidPayload(payload.Body);
            case ReadOnlySePayloadType.Text:
                return new TextPayload(payload.Body);
            case ReadOnlySePayloadType.Macro:
                break;
        }

        IMutableSePayload p = payload.MacroCode switch
        {
            MacroCode.SetResetTime => new SetResetTimePayload(),
            MacroCode.SetTime => new SetTimePayload(),
            MacroCode.If => new IfPayload(),
            MacroCode.Switch => new SwitchPayload(),
            MacroCode.PcName => new PcNamePayload(),
            MacroCode.IfPcGender => new IfPcGenderPayload(),
            MacroCode.IfPcName => new IfPcNamePayload(),
            MacroCode.Josa => new JosaPayload(),
            MacroCode.Josaro => new JosaroPayload(),
            MacroCode.IfSelf => new IfSelfPayload(),
            MacroCode.NewLine => NewLinePayload.Instance,
            MacroCode.Wait => new WaitPayload(),
            MacroCode.Icon => new IconPayload(),
            MacroCode.Color => new ColorPayload(),
            MacroCode.EdgeColor => new EdgeColorPayload(),
            MacroCode.ShadowColor => new ShadowColorPayload(),
            MacroCode.SoftHyphen => SoftHyphenPayload.Instance,
            MacroCode.Key => KeyPayload.Instance,
            MacroCode.Scale => new ScalePayload(),
            MacroCode.Bold => new BoldPayload(),
            MacroCode.Italic => new ItalicPayload(),
            MacroCode.Edge => new EdgePayload(),
            MacroCode.Shadow => new ShadowPayload(),
            MacroCode.NonBreakingSpace => NonBreakingSpacePayload.Instance,
            MacroCode.Icon2 => new Icon2Payload(),
            MacroCode.Hyphen => HyphenPayload.Instance,
            MacroCode.Num => new NumPayload(),
            MacroCode.Hex => new HexPayload(),
            MacroCode.Kilo => new KiloPayload(),
            MacroCode.Byte => new BytePayload(),
            MacroCode.Sec => new SecPayload(),
            // MacroCode.Time => new TimePayload(),
            MacroCode.Float => new FloatPayload(),
            // MacroCode.Link => new LinkPayload(),
            // MacroCode.Sheet => new SheetPayload(),
            MacroCode.String => new StringPayload(),
            MacroCode.Caps => new CapsPayload(),
            MacroCode.Head => new HeadPayload(),
            // MacroCode.Split => new SplitPayload(),
            MacroCode.HeadAll => new HeadAllPayload(),
            // MacroCode.Fixed => new FixedPayload(),
            MacroCode.Lower => new LowerPayload(),
            // MacroCode.JaNoun => new JaNounPayload(),
            // MacroCode.EnNoun => new EnNounPayload(),
            // MacroCode.DeNoun => new DeNounPayload(),
            // MacroCode.FrNoun => new FrNounPayload(),
            // MacroCode.ChNoun => new ChNounPayload(),
            MacroCode.LowerHead => new LowerHeadPayload(),
            MacroCode.ColorType => new ColorTypePayload(),
            MacroCode.EdgeColorType => new EdgeColorTypePayload(),
            // MacroCode.Digit => new DigitPayload(),
            // MacroCode.Ordinal => new OrdinalPayload(),
            // MacroCode.Sound => new SoundPayload(),
            // MacroCode.LevelPos => new LevelPosPayload(),
            _ => new FreeFormPayload((int)payload.MacroCode).WithExpressionsFromLumina(payload),
        };

        (p as FixedFormPayload)?.WithExpressionsFromLumina(payload);
        return p;
    }

    /// <summary>Creates a text payload from the given zero-terminated UTF-8 string.</summary>
    /// <param name="sz">A zero-terminated UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(byte* sz) => new(sz);

    /// <summary>Creates a text payload from the given UTF-8 string.</summary>
    /// <param name="text">A UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(byte[] text) => new(text.AsSpan());

    /// <summary>Creates a text payload from the given UTF-8 string.</summary>
    /// <param name="text">A UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(ReadOnlySpan<byte> text) => new(text);

    /// <summary>Creates a text payload from the given UTF-8 string.</summary>
    /// <param name="text">A UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(ReadOnlyMemory<byte> text) => new(text.Span);

    /// <summary>Creates a text payload from the given zero-terminated UTF-16 string.</summary>
    /// <param name="sz">A zero-terminated UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(char* sz) => new(sz);

    /// <summary>Creates a text payload from the given UTF-16 string.</summary>
    /// <param name="text">A UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(char[] text) => new(text.AsSpan());

    /// <summary>Creates a text payload from the given UTF-16 string.</summary>
    /// <param name="text">A UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(ReadOnlySpan<char> text) => new(text);

    /// <summary>Creates a text payload from the given UTF-16 string.</summary>
    /// <param name="text">A UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(ReadOnlyMemory<char> text) => new(text.Span);

    /// <summary>Creates a text payload from the given string.</summary>
    /// <param name="text">A string.</param>
    /// <returns>A new text payload.</returns>
    public static TextPayload FromText(string? text) => new(text);
}
