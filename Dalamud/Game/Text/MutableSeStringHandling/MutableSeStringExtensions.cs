using System.Text;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>Extension methods associated with <see cref="MutableSeString"/>.</summary>
public static class MutableSeStringExtensions
{
    /// <summary>Appends a string into an instance of <see cref="Lumina.Text.SeStringBuilder"/>.</summary>
    /// <param name="builder">The SeString builder.</param>
    /// <param name="string">The string to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static Lumina.Text.SeStringBuilder AppendDalamud(
        this Lumina.Text.SeStringBuilder builder,
        MutableSeString? @string)
    {
        if (@string is not null)
        {
            foreach (var payload in @string)
                builder.AppendDalamud(payload);
        }

        return builder;
    }

    /// <summary>Appends a string into an instance of <see cref="StringBuilder"/> in an encoded form.</summary>
    /// <param name="builder">The string builder.</param>
    /// <param name="string">The string to append.</param>
    /// <returns><paramref name="builder"/> for method chaining, after the append operation is completed.</returns>
    public static StringBuilder AppendEncoded(
        this StringBuilder builder,
        MutableSeString @string)
    {
        foreach (var payload in @string)
            builder.AppendEncoded(payload);
        return builder;
    }
}
