using System.Collections;

using Dalamud.Interface.SeStringRenderer.Internal;

namespace Dalamud.Game.Text.SeStringHandling.SeStringSpan;

/// <summary>Represents a payload.</summary>
public readonly ref struct SePayloadReadOnlySpan
{
    /// <summary>The type of the payload.</summary>
    /// <remarks><c>0</c> means that it is a text. <c>-1</c> means that it is an invalid byte.</remarks>
    public readonly int Type;

    /// <summary>The payload envelope, including the start byte, <see cref="Type"/> indicator, length indicator,
    /// <see cref="Body"/>, and the end byte.</summary>
    public readonly ReadOnlySpan<byte> Envelope;

    /// <summary>The payload body.</summary>
    public readonly ReadOnlySpan<byte> Body;

    /// <summary>Initializes a new instance of the <see cref="SePayloadReadOnlySpan"/> struct.</summary>
    /// <param name="type">The type of the payload.</param>
    /// <param name="envelope">The payload envelope.</param>
    /// <param name="body">The payload body.</param>
    public SePayloadReadOnlySpan(int type, ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> body)
    {
        this.Type = type;
        this.Envelope = envelope;
        this.Body = body;
    }

    /// <summary>Initializes a new instance of the <see cref="SePayloadReadOnlySpan"/> struct.</summary>
    /// <param name="envelope">The payload envelope.</param>
    public SePayloadReadOnlySpan(ReadOnlySpan<byte> envelope)
    {
        this.Envelope = this.Body = envelope;
        if (envelope.IsEmpty || envelope[0] != 0x02)
            return;

        if (envelope.Length < 3)
        {
            this.Type = -1;
            return;
        }

        this.Type = envelope[1];
        if (!SeStringExpressionUtilities.TryDecodeInt(envelope[2..], out int len, out var exprLen)
            || 1 + exprLen + len + 1 > envelope.Length)
        {
            this.Type = -1;
            return;
        }

        this.Body = envelope.Slice(1 + exprLen, len);
    }

    /// <inheritdoc cref="SePayloadReadOnlySpan(ReadOnlySpan{byte})"/>
    public SePayloadReadOnlySpan(Span<byte> envelope)
        : this((ReadOnlySpan<byte>)envelope)
    {
    }

    /// <inheritdoc cref="SePayloadReadOnlySpan(ReadOnlySpan{byte})"/>
    public SePayloadReadOnlySpan(Memory<byte> envelope)
        : this((ReadOnlySpan<byte>)envelope.Span)
    {
    }

    /// <inheritdoc cref="SePayloadReadOnlySpan(ReadOnlySpan{byte})"/>
    public SePayloadReadOnlySpan(ReadOnlyMemory<byte> envelope)
        : this(envelope.Span)
    {
    }

    /// <inheritdoc cref="SePayloadReadOnlySpan(ReadOnlySpan{byte})"/>
    public SePayloadReadOnlySpan(byte[] envelope)
        : this(envelope.AsSpan())
    {
    }

    /// <summary>Gets a value indicating whether this payload is malformed.</summary>
    /// <remarks>Whether the expressions make sense for the payload type is not checked.</remarks>
    public bool IsInvalid => this.Type == -1;

    /// <summary>Gets a value indicating whether this payload is just a raw string.</summary>
    /// <remarks>Type <c>-1</c> is not a valid value to use in an well-formed payload envelope.</remarks>
    public bool IsText => this.Type == 0;

    /// <summary>Gets the type as a <see cref="MacroCode"/>.</summary>
    internal MacroCode MacroCode => (MacroCode)this.Type;

    public static implicit operator ReadOnlySpan<byte>(SePayloadReadOnlySpan s) => s.Envelope;

    public static implicit operator SePayloadReadOnlySpan(ReadOnlySpan<byte> s) => new(s);

    public static implicit operator SePayloadReadOnlySpan(Span<byte> s) => new(s);

    public static implicit operator SePayloadReadOnlySpan(ReadOnlyMemory<byte> s) => new(s);

    public static implicit operator SePayloadReadOnlySpan(Memory<byte> s) => new(s);

    public static implicit operator SePayloadReadOnlySpan(byte[] s) => new(s);

    /// <summary>Gets the expression forming this payload.</summary>
    /// <param name="expr1">The resolved expression.</param>
    /// <returns><c>true</c> if the expression is resolved.</returns>
    public bool TryGetExpression(out SeExpressionReadOnlySpan expr1)
    {
        expr1 = default;

        var enu = this.GetEnumerator();
        if (!enu.MoveNext())
            return false;
        expr1 = enu.Current;
        return true;
    }

    /// <summary>Gets the expressions forming this payload.</summary>
    /// <param name="expr1">The resolved expression 1.</param>
    /// <param name="expr2">The resolved expression 2.</param>
    /// <returns><c>true</c> if all expressions are resolved.</returns>
    public bool TryGetExpression(out SeExpressionReadOnlySpan expr1, out SeExpressionReadOnlySpan expr2)
    {
        expr1 = expr2 = default;

        var enu = this.GetEnumerator();
        if (!enu.MoveNext())
            return false;
        expr1 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr2 = enu.Current;
        return true;
    }

    /// <summary>Gets the expressions forming this payload.</summary>
    /// <param name="expr1">The resolved expression 1.</param>
    /// <param name="expr2">The resolved expression 2.</param>
    /// <param name="expr3">The resolved expression 3.</param>
    /// <returns><c>true</c> if all expressions are resolved.</returns>
    public bool TryGetExpression(
        out SeExpressionReadOnlySpan expr1,
        out SeExpressionReadOnlySpan expr2,
        out SeExpressionReadOnlySpan expr3)
    {
        expr1 = expr2 = expr3 = default;

        var enu = this.GetEnumerator();
        if (!enu.MoveNext())
            return false;
        expr1 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr2 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr3 = enu.Current;
        return true;
    }

    /// <summary>Gets the expressions forming this payload.</summary>
    /// <param name="expr1">The resolved expression 1.</param>
    /// <param name="expr2">The resolved expression 2.</param>
    /// <param name="expr3">The resolved expression 3.</param>
    /// <param name="expr4">The resolved expression 4.</param>
    /// <returns><c>true</c> if all expressions are resolved.</returns>
    public bool TryGetExpression(
        out SeExpressionReadOnlySpan expr1,
        out SeExpressionReadOnlySpan expr2,
        out SeExpressionReadOnlySpan expr3,
        out SeExpressionReadOnlySpan expr4)
    {
        expr1 = expr2 = expr3 = expr4 = default;

        var enu = this.GetEnumerator();
        if (!enu.MoveNext())
            return false;
        expr1 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr2 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr3 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr4 = enu.Current;
        return true;
    }

    /// <summary>Gets the expressions forming this payload.</summary>
    /// <param name="expr1">The resolved expression 1.</param>
    /// <param name="expr2">The resolved expression 2.</param>
    /// <param name="expr3">The resolved expression 3.</param>
    /// <param name="expr4">The resolved expression 4.</param>
    /// <param name="expr5">The resolved expression 5.</param>
    /// <returns><c>true</c> if all expressions are resolved.</returns>
    public bool TryGetExpression(
        out SeExpressionReadOnlySpan expr1,
        out SeExpressionReadOnlySpan expr2,
        out SeExpressionReadOnlySpan expr3,
        out SeExpressionReadOnlySpan expr4,
        out SeExpressionReadOnlySpan expr5)
    {
        expr1 = expr2 = expr3 = expr4 = expr5 = default;

        var enu = this.GetEnumerator();
        if (!enu.MoveNext())
            return false;
        expr1 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr2 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr3 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr4 = enu.Current;
        if (!enu.MoveNext())
            return false;
        expr5 = enu.Current;
        return true;
    }

    /// <summary>Gets an enumerator for enumerating over the expressions under this payload.</summary>
    /// <returns>A new enumerator.</returns>
    public Enumerator GetEnumerator() => new(this.Body);

    /// <summary>Enumerator for <see cref="SePayloadReadOnlySpan"/>.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<byte> data;
        private int index;

        /// <summary>Initializes a new instance of the <see cref="Enumerator"/> struct.</summary>
        /// <param name="data">The backing data.</param>
        public Enumerator(ReadOnlySpan<byte> data) => this.data = data;

        /// <inheritdoc cref="IEnumerator.Current"/>
        public SeExpressionReadOnlySpan Current { get; private set; }

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext()
        {
            if (this.index >= this.data.Length)
                return false;

            this.index += this.Current.Body.Length;

            var subspan = this.data[this.index..];
            if (subspan.IsEmpty)
                return false;

            this.Current =
                SeStringExpressionUtilities.TryDecodeLength(subspan, out var length)
                    ? new(subspan[..length])
                    : new(subspan[..1]);
            return true;
        }
    }
}
