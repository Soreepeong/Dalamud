using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dalamud.Game.Text.DSeStringHandling;

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

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> from <see cref="Lumina.Text.SeString"/>.
    /// </summary>
    /// <param name="value">The Lumina SeString value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static MutableSeString FromLumina(Lumina.Text.SeString? value) => FromLumina(value?.AsReadOnly() ?? default);

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> from
    /// <see cref="Lumina.Text.ReadOnly.ReadOnlySeString"/>.</summary>
    /// <param name="value">The Lumina SeString value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static MutableSeString FromLumina(Lumina.Text.ReadOnly.ReadOnlySeString value) => FromLumina(value.AsSpan());

    /// <summary>Creates a new instance of <see cref="MutableSeString"/> from
    /// <see cref="Lumina.Text.ReadOnly.ReadOnlySeStringSpan"/>.</summary>
    /// <param name="value">The Lumina SeString value.</param>
    /// <returns>A new instance of <see cref="MutableSeString"/> initialized with the given string.</returns>
    public static MutableSeString FromLumina(Lumina.Text.ReadOnly.ReadOnlySeStringSpan value)
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
}
