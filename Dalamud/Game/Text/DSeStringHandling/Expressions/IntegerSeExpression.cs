using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

using Lumina.Text;

namespace Dalamud.Game.Text.DSeStringHandling.Expressions;

/// <summary>A SeString expression representing a literal integer value.</summary>
public sealed class IntegerSeExpression : IMutableSeExpression
{
    /// <summary>Initializes a new instance of the <see cref="IntegerSeExpression"/> class.</summary>
    public IntegerSeExpression()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="IntegerSeExpression"/> class.</summary>
    /// <param name="value">The initial value.</param>
    public IntegerSeExpression(int value) => this.IntValue = value;

    /// <summary>Initializes a new instance of the <see cref="IntegerSeExpression"/> class.</summary>
    /// <param name="value">The initial value.</param>
    public IntegerSeExpression(uint value) => this.UIntValue = value;

    /// <summary>Gets or sets the integer value.</summary>
    public int IntValue { get; set; }

    /// <summary>Gets or sets the unsigned integer value.</summary>
    public uint UIntValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked((uint)this.IntValue);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.IntValue = unchecked((int)value);
    }

    /// <summary>Gets or sets the boolean value.</summary>
    [SuppressMessage(
        "StyleCop.CSharp.DocumentationRules",
        "SA1623:Property summary documentation should match accessors",
        Justification = "lol")]
    public bool BoolValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.IntValue != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.IntValue = value ? 1 : 0;
    }

    /// <inheritdoc/>
    string? IMutableSeExpression.NativeName => null;

    /// <inheritdoc/>
    public unsafe byte Marker
    {
        get
        {
            var bufStorage = default(ulong);
            var buf = (byte*)bufStorage;
            SeExpressionUtilities.EncodeInt(new(buf, sizeof(ulong)), this.IntValue);
            return buf![0];
        }
    }

    /// <inheritdoc/>
    public int CalculateByteCount(bool allowOverestimation) =>
        allowOverestimation ? 5 : SeExpressionUtilities.CalculateLengthInt(this.IntValue);

    /// <inheritdoc/>
    public byte[] ToBytes()
    {
        var buf = new byte[this.CalculateByteCount(false)];
        this.WriteToSpan(buf);
        return buf;
    }

    /// <inheritdoc/>
    public int WriteToSpan(Span<byte> span) => SeExpressionUtilities.EncodeInt(span, this.IntValue);

    /// <inheritdoc/>
    public unsafe void WriteToStream(Stream stream)
    {
        var bufStorage = default(ulong);
        var buf = (byte*)&bufStorage;
        stream.Write(new(buf, this.WriteToSpan(new(buf, sizeof(ulong)))));
    }

    /// <inheritdoc/>
    public override string ToString() => this.IntValue.ToString();
}
