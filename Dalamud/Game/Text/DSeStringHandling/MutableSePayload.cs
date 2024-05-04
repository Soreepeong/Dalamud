using Dalamud.Game.Text.DSeStringHandling.Payloads;
using Dalamud.Game.Text.DSeStringHandling.Payloads.Macros;

using Lumina.Text.ReadOnly;

namespace Dalamud.Game.Text.DSeStringHandling;

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
    public static IMutableSePayload FromLumina(Lumina.Text.Payloads.BasePayload payload) =>
        payload.IsTextPayload ? FromText(payload.RawString) : From(payload.Data) ?? new TextSePayload();

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
                return new InvalidSePayload(payload.Body);
            case ReadOnlySePayloadType.Text:
                return new TextSePayload(payload.Body);
            case ReadOnlySePayloadType.Macro:
                break;
        }

        switch (payload.MacroCode)
        {
            default:
                var rp = new FreeformSePayload((int)payload.MacroCode);
                foreach (var expression in payload)
                    rp.Add(MutableSeExpression.FromLumina(expression));
                return rp;
        }
    }

    /// <summary>Creates a text payload from the given zero-terminated UTF-8 string.</summary>
    /// <param name="sz">A zero-terminated UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(byte* sz) => new(sz);

    /// <summary>Creates a text payload from the given UTF-8 string.</summary>
    /// <param name="text">A UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(byte[] text) => new(text.AsSpan());

    /// <summary>Creates a text payload from the given UTF-8 string.</summary>
    /// <param name="text">A UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(ReadOnlySpan<byte> text) => new(text);

    /// <summary>Creates a text payload from the given UTF-8 string.</summary>
    /// <param name="text">A UTF-8 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(ReadOnlyMemory<byte> text) => new(text.Span);

    /// <summary>Creates a text payload from the given zero-terminated UTF-16 string.</summary>
    /// <param name="sz">A zero-terminated UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(char* sz) => new(sz);

    /// <summary>Creates a text payload from the given UTF-16 string.</summary>
    /// <param name="text">A UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(char[] text) => new(text.AsSpan());

    /// <summary>Creates a text payload from the given UTF-16 string.</summary>
    /// <param name="text">A UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(ReadOnlySpan<char> text) => new(text);

    /// <summary>Creates a text payload from the given UTF-16 string.</summary>
    /// <param name="text">A UTF-16 string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(ReadOnlyMemory<char> text) => new(text.Span);

    /// <summary>Creates a text payload from the given string.</summary>
    /// <param name="text">A string.</param>
    /// <returns>A new text payload.</returns>
    public static TextSePayload FromText(string? text) => new(text);
}
