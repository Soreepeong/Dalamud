using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace Dalamud.Game.Text.MutableSeStringHandling;

/// <summary>A mutable SeString.</summary>
public sealed class MutableSeString : List<IMutableSePayload>
{
    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-16 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static implicit operator MutableSeString(string? value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-16 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static implicit operator MutableSeString(ReadOnlyMemory<char> value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-16 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static implicit operator MutableSeString(ReadOnlySpan<char> value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-16 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static implicit operator MutableSeString(char[] value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-16 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static unsafe implicit operator MutableSeString(char* value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-8 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static implicit operator MutableSeString(ReadOnlyMemory<byte> value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-8 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>    
    public static implicit operator MutableSeString(ReadOnlySpan<byte> value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-8 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>    
    public static implicit operator MutableSeString(byte[] value) => [MutableSePayload.FromText(value)];

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> class from a UTF-8 string.</summary>
    /// <param name="value">The initial string value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>    
    public static unsafe implicit operator MutableSeString(byte* value) => [MutableSePayload.FromText(value)];

    /// <summary>Gets a SeString at the given memory address.</summary>
    /// <param name="sz">Pointer to a SeString.</param>
    /// <returns>The string, or <c>null</c> if none was found.</returns>
    public static unsafe MutableSeString From(byte* sz) => FromLumina(new ReadOnlySeStringSpan(sz));

    /// <summary>Gets a SeString from the given memory bytes.</summary>
    /// <param name="bytes">Bytes to interpret as a SeString.</param>
    /// <returns>The string, or <c>null</c> if none was found.</returns>
    public static MutableSeString From(ReadOnlySpan<byte> bytes) => FromLumina(new ReadOnlySeStringSpan(bytes));

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> from <see cref="Lumina.Text.SeString"/>.
    /// </summary>
    /// <param name="value">The Lumina SeString value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static MutableSeString FromLumina(SeString? value) => FromLumina(value?.AsReadOnly() ?? default);

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> from
    /// <see cref="Lumina.Text.ReadOnly.ReadOnlySeString"/>.</summary>
    /// <param name="value">The Lumina SeString value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static MutableSeString FromLumina(ReadOnlySeString value) => FromLumina(value.AsSpan());

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> from
    /// <see cref="Lumina.Text.ReadOnly.ReadOnlySeStringSpan"/>.</summary>
    /// <param name="value">The Lumina SeString value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static MutableSeString FromLumina(ReadOnlySeStringSpan value)
    {
        var res = new MutableSeString();
        foreach (var p in value)
            res.Add(MutableSePayload.FromLumina(p));
        return res;
    }

    /// <summary>Adds a text payload.</summary>
    /// <param name="text">Text to add.</param>
    public void AddText(string? text) => this.Add(MutableSePayload.FromText(text));

    /// <summary>Adds a UTF-16 text payload.</summary>
    /// <param name="text">Text to add.</param>
    public void AddText(ReadOnlySpan<char> text) => this.Add(MutableSePayload.FromText(text));

    /// <summary>Adds a UTF-8 text payload.</summary>
    /// <param name="text">Text to add.</param>
    public void AddText(ReadOnlySpan<byte> text) => this.Add(MutableSePayload.FromText(text));

    /// <summary>Inserts a text payload.</summary>
    /// <param name="index">Index to insert into.</param>
    /// <param name="text">Text to insert.</param>
    public void InsertText(int index, string? text) => this.Insert(index, MutableSePayload.FromText(text));

    /// <summary>Inserts a UTF-16 text payload.</summary>
    /// <param name="index">Index to insert into.</param>
    /// <param name="text">Text to insert.</param>
    public void InsertText(int index, ReadOnlySpan<char> text) => this.Insert(index, MutableSePayload.FromText(text));

    /// <summary>Inserts a UTF-8 text payload.</summary>
    /// <param name="index">Index to insert into.</param>
    /// <param name="text">Text to insert.</param>
    public void InsertText(int index, ReadOnlySpan<byte> text) => this.Insert(index, MutableSePayload.FromText(text));

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var p in this)
            sb.AppendEncoded(p);
        return sb.ToString();
    }

    /// <summary>Evaluates this SeString into a boolean.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated value.</returns>
    public unsafe bool EvaluateAsBool(ISeStringEvaluationContext context)
    {
        var bufStorage = default(Vector4);
        var buf = new Span<byte>(&bufStorage, sizeof(Vector4));
        if (this.EvaluateToSpan(context, buf, out var len) is not true)
            return false;

        buf = buf[..len];
        return buf.IsEmpty
               || buf.SequenceEqual("0"u8)
               || buf.SequenceEqual("false"u8);
    }

    /// <summary>Evaluates this SeString into an integer.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated value.</returns>
    public unsafe int EvaluateAsInt(ISeStringEvaluationContext context)
    {
        var bufStorage = default(Vector4);
        var buf = new Span<byte>(&bufStorage, sizeof(Vector4));
        if (this.EvaluateToSpan(context, buf, out var len) is not true)
            return 0;

        buf = buf[..len];
        return int.TryParse(buf, out var parsed) ? parsed : 0;
    }

    /// <summary>Evaluates this SeString into an unsigned integer.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The evaluated value.</returns>
    public unsafe uint EvaluateAsUInt(ISeStringEvaluationContext context)
    {
        var bufStorage = default(Vector4);
        var buf = new Span<byte>(&bufStorage, sizeof(Vector4));
        if (this.EvaluateToSpan(context, buf, out var len) is not true)
            return 0;

        buf = buf[..len];
        return uint.TryParse(buf, out var parsed) ? parsed : 0u;
    }

    /// <summary>Evaluates this SeString using the given context to a SeString that is not dependent on context.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="ssb">An instance of <see cref="SeStringBuilder"/> to write the evaluation result to.</param>
    public void EvaluateToSeStringBuilder(ISeStringEvaluationContext context, SeStringBuilder ssb)
    {
        foreach (var p in this)
            p.EvaluateToSeStringBuilder(context, ssb);
    }

    /// <summary>Evaluates this SeString using the given context to a SeString that is not dependent on context.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="span">The span to write the evaluation result to.</param>
    /// <param name="bytesWritten">Number of bytes written to <paramref name="span"/>.</param>
    /// <returns><c>true</c> if the evaluation result is fully stored in <paramref name="span"/>.</returns>
    public bool EvaluateToSpan(ISeStringEvaluationContext context, Span<byte> span, out int bytesWritten)
    {
        bytesWritten = 0;
        foreach (var p in this)
        {
            var r = p.EvaluateToSpan(context, span[bytesWritten..], out var len);
            bytesWritten += len;
            if (!r)
                return false;
        }

        return true;
    }

    /// <summary>Calculates the number of bytes required to encode this SeString.</summary>
    /// <param name="allowOverestimation">Allow returning a value that may be larger than the exact number of bytes
    /// required, for faster calculation.</param>
    /// <returns>Number of bytes required.</returns>
    public int CalculateByteCount(bool allowOverestimation)
    {
        var n = 0;
        foreach (var p in this)
            n += p.CalculateByteCount(allowOverestimation);
        return n;
    }

    /// <summary>Encodes this SeString into a byte array.</summary>
    /// <returns>The encoded bytes.</returns>
    public byte[] ToBytes()
    {
        var buf = new byte[this.CalculateByteCount(false)];
        this.WriteToSpan(buf);
        return buf;
    }

    /// <summary>Encodes this SeString into a byte span.</summary>
    /// <param name="span">The span to write this SeString to.</param>
    /// <returns>Number of bytes written.</returns>
    /// <remarks>The length of <paramref name="span"/> should be at least <see cref="CalculateByteCount"/>.</remarks>
    public int WriteToSpan(Span<byte> span)
    {
        var spanBefore = span;
        foreach (var p in this)
            span = span[p.WriteToSpan(span)..];
        return spanBefore.Length - span.Length;
    }

    /// <summary>Encodes this SeString into a stream.</summary>
    /// <param name="stream">The stream to write this SeString to.</param>
    public void WriteToStream(Stream stream)
    {
        foreach (var p in this)
            p.WriteToStream(stream);
    }

    /// <summary>Helper for lazy construction of <see cref="MutableSeString"/> from bytes.</summary>
    public readonly ref struct Lazy
    {
        private readonly ref MutableSeString? instance;

        /// <summary>Initializes a new instance of the <see cref="Lazy"/> struct.</summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="instance">The instance storage.</param>
        public Lazy(ReadOnlySpan<byte> bytes, ref MutableSeString? instance)
        {
            this.Bytes = bytes;
            this.instance = ref instance;
        }

        /// <summary>Gets a value indicating whether the SeString bytes is parsed.</summary>
        public bool IsParsed => this.instance is not null;

        /// <summary>Gets the parsed instance of <see cref="MutableSeString"/>.</summary>
        public MutableSeString Value => this.instance ??= From(this.Bytes);

        /// <summary>Gets the underlying bytes.</summary>
        public ReadOnlySpan<byte> Bytes { get; }
    }
}
