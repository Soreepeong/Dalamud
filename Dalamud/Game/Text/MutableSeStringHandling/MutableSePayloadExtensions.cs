using System.Text;

using Dalamud.Game.Text.MutableSeStringHandling.Payloads;

using Lumina.Text.Payloads;

using TextPayload = Dalamud.Game.Text.MutableSeStringHandling.Payloads.TextPayload;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>Extension methods associated with <see cref="IMutableSePayload"/>.</summary>
public static class MutableSePayloadExtensions
{
    /// <summary>Appends a payload into an instance of <see cref="Lumina.Text.SeStringBuilder"/>.</summary>
    /// <param name="builder">The SeString builder.</param>
    /// <param name="payload">The payload to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static Lumina.Text.SeStringBuilder AppendDalamud(
        this Lumina.Text.SeStringBuilder builder,
        IMutableSePayload payload)
    {
        builder.BeginMacro((MacroCode)payload.MacroCode);
        foreach (var e in payload.Expressions)
            builder.AppendDalamud(e);
        builder.EndMacro();
        return builder;
    }

    /// <summary>Appends a payload into an instance of <see cref="StringBuilder"/> in an encoded form.</summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="payload">The payload to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static StringBuilder AppendEncoded(
        this StringBuilder builder,
        IMutableSePayload payload)
    {
        switch (payload)
        {
            case TextPayload textSePayload:
                return builder.Append(textSePayload.Text);

            case InvalidPayload invalidSePayload:
                var sp = invalidSePayload.Data.Span;
                builder.EnsureCapacity(builder.Length + (4 * sp.Length));
                foreach (var b in sp)
                    builder.Append($"\\x{b:X02}");
                return builder;
        }

        var macroCode = (MacroCode)payload.MacroCode;
        builder.Append('<');
        if (Enum.IsDefined(macroCode))
            builder.Append(macroCode);
        else
            builder.Append($"?{payload.MacroCode:X02}");

        if (payload.Expressions.Count > 0)
        {
            builder.Append('(');
            for (var i = 0; i < payload.Expressions.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.AppendEncoded(payload.Expressions[i]);
            }

            builder.Append(')');
        }

        return builder.Append('>');
    }
}
