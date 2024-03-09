namespace Dalamud.Game.Text.SeStringHandling.SeStringSpan;

/// <summary>Represents a SeString expression byte sequence.</summary>
public readonly ref struct SeExpressionReadOnlySpan
{
    /// <summary>The data.</summary>
    public readonly ReadOnlySpan<byte> Body;

    /// <summary>Initializes a new instance of the <see cref="SeExpressionReadOnlySpan"/> struct.</summary>
    /// <param name="body">The data.</param>
    public SeExpressionReadOnlySpan(ReadOnlySpan<byte> body) => this.Body = body;

    /// <inheritdoc cref="SeExpressionReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeExpressionReadOnlySpan(Span<byte> body) => this.Body = body;

    /// <inheritdoc cref="SeExpressionReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeExpressionReadOnlySpan(ReadOnlyMemory<byte> body) => this.Body = body.Span;

    /// <inheritdoc cref="SeExpressionReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeExpressionReadOnlySpan(Memory<byte> body) => this.Body = body.Span;

    /// <inheritdoc cref="SeExpressionReadOnlySpan(ReadOnlySpan{byte})"/>
    public SeExpressionReadOnlySpan(byte[] body) => this.Body = body;

    public static implicit operator ReadOnlySpan<byte>(SeExpressionReadOnlySpan s) => s.Body;

    public static implicit operator SeExpressionReadOnlySpan(ReadOnlySpan<byte> s) => new(s);

    public static implicit operator SeExpressionReadOnlySpan(Span<byte> s) => new(s);

    public static implicit operator SeExpressionReadOnlySpan(Memory<byte> s) => new(s);

    public static implicit operator SeExpressionReadOnlySpan(byte[] s) => new(s);

    /// <summary>Attempts to get an integer value from this expression.</summary>
    /// <param name="value">The parsed integer value.</param>
    /// <returns><c>true</c> if successful.</returns>
    public bool TryGetUInt(out uint value) =>
        SeStringExpressionUtilities.TryDecodeUInt(this.Body, out value, out _);

    /// <inheritdoc cref="TryGetUInt(out uint)"/>
    public bool TryGetInt(out int value) =>
        SeStringExpressionUtilities.TryDecodeInt(this.Body, out value, out _);

    /// <summary>Attempts to get a SeString value from this expression.</summary>
    /// <param name="value">The parsed <see cref="SeStringReadOnlySpan"/> value.</param>
    /// <returns><c>true</c> if successful.</returns>
    public bool TryGetString(out SeStringReadOnlySpan value) =>
        SeStringExpressionUtilities.TryDecodeString(this.Body, out value, out _);

    /// <summary>Attempts to get a placeholder type from this expression.</summary>
    /// <param name="expressionType">The parsed expression type.</param>
    /// <returns><c>true</c> if successful.</returns>
    public bool TryGetPlaceholderExpression(out byte expressionType) =>
        SeStringExpressionUtilities.TryDecodeNullary(this.Body, out expressionType, out _);

    /// <summary>Attempts to get a paramter expression from this expression.</summary>
    /// <param name="expressionType">The parsed expression type.</param>
    /// <param name="operand">The operand.</param>
    /// <returns><c>true</c> if successful.</returns>
    public bool TryGetParameterExpression(out byte expressionType, out SeExpressionReadOnlySpan operand) =>
        SeStringExpressionUtilities.TryDecodeUnary(this.Body, out expressionType, out operand, out _);

    /// <summary>Attempts to get a binary expression from this expression.</summary>
    /// <param name="expressionType">The parsed expression type.</param>
    /// <param name="operand1">The operand 1.</param>
    /// <param name="operand2">The operand 2.</param>
    /// <returns><c>true</c> if successful.</returns>
    public bool TryGetBinaryExpression(
        out byte expressionType,
        out SeExpressionReadOnlySpan operand1,
        out SeExpressionReadOnlySpan operand2) =>
        SeStringExpressionUtilities.TryDecodeBinary(this.Body, out expressionType, out operand1, out operand2, out _);
}
