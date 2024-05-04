using System.Text;

namespace Dalamud.Game.Text.DSeStringHandling;

/// <summary>Extension methods associated with <see cref="IMutableSePayload"/>.</summary>
public static class MutableSePayloadExtensions
{
    /// <summary>Appends a payload into an instance of <see cref="Lumina.Text.SeStringBuilder"/>.</summary>
    /// <param name="builder">The SeString builder.</param>
    /// <param name="payload">The payload to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static Lumina.Text.SeStringBuilder Append(
        this Lumina.Text.SeStringBuilder builder,
        IMutableSePayload payload)
    {
        builder.BeginMacro((Lumina.Text.Payloads.MacroCode)payload.MacroCode);
        foreach (var e in payload.Expressions)
            e.AppendToBuilder(builder);
        builder.EndMacro();
        return builder;
    }
    
    /// <summary>Appends a payload into an instance of <see cref="SeStringHandling.SeStringBuilder"/>.</summary>
    /// <param name="builder">The SeString builder.</param>
    /// <param name="payload">The payload to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static SeStringHandling.SeStringBuilder Append(
        this SeStringHandling.SeStringBuilder builder,
        IMutableSePayload payload)
    {
        throw new NotImplementedException();
    }
    
    /// <summary>Appends a payload into an instance of <see cref="StringBuilder"/> in an encoded form.</summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="payload">The payload to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static StringBuilder AppendEncoded(
        this StringBuilder builder,
        IMutableSePayload payload)
    {
        throw new NotImplementedException();
    }
}
