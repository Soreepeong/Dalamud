using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Utility.Enumerators;

namespace Dalamud.Utility;

/// <summary>Extension methods for <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.</summary>
public static class SpanExtensions
{
    /// <summary>Creates an enumerable that can be used to iterate unicode codepoints in a span.</summary>
    /// <param name="span">The span to reinterpret.</param>
    /// <returns>The enumerable/enumerator-like ref struct.</returns>
    public static Utf8SpanEnumerator AsUtf8Enumerable(this ReadOnlySpan<byte> span) => new(span);

    /// <inheritdoc cref="AsUtf8Enumerable(ReadOnlySpan{byte})"/>
    public static Utf8SpanEnumerator AsUtf8Enumerable(this Span<byte> span) => new(span);

    /// <summary>Creates a thin wrapper for interpreting as a SeString.</summary>
    /// <param name="span">The span to reinterpret.</param>
    /// <returns>The wrapper <see cref="SeStringReadOnlySpan"/> instance.</returns>
    public static SeStringReadOnlySpan AsSeStringSpan(this ReadOnlySpan<byte> span) => new(span);

    /// <inheritdoc cref="AsSeStringSpan(ReadOnlySpan{byte})"/>
    public static SeStringReadOnlySpan AsSeStringSpan(this Span<byte> span) => new(span);
}
