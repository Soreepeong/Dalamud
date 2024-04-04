using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Dalamud.Interface.Internal;
using Dalamud.Interface.Spannables.Internal;
using Dalamud.Interface.Spannables.Styles;
using Dalamud.Interface.Spannables.Text.Internal;

namespace Dalamud.Interface.Spannables.Text;

/// <summary>A UTF-8 character sequence with embedded styling information.</summary>
public sealed partial class StyledText : AbstractStyledText, ISpanParsable<StyledText>, IEquatable<StyledText>
{
    private static readonly (MethodInfo Info, SpannedParseInstructionAttribute Attr)[] SsbMethods =
        typeof(IStyledTextBuilder)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
            .Select(
                x => (
                         Info: typeof(StyledTextBuilder).GetMethod(
                             x.Name,
                             BindingFlags.Instance | BindingFlags.Public,
                             x.GetParameters().Select(y => y.ParameterType).ToArray()),
                         Attr: x.GetCustomAttribute<SpannedParseInstructionAttribute>()))
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Info.Name)
            .ThenByDescending(x => x.Info.GetParameters().Length)
            .ToArray();

    private readonly byte[] textStream;
    private readonly byte[] dataStream;
    private readonly SpannedRecord[] records;
    private readonly FontHandleVariantSet[] fontSets;
    private readonly IDalamudTextureWrap?[] textures;
    private readonly ISpannableTemplate?[] spannables;
    private readonly Lazy<int> hashCode;

    /// <summary>Initializes a new instance of the <see cref="StyledText"/> class.</summary>
    /// <param name="data">The plain text.</param>
    public StyledText(ReadOnlySpan<char> data)
        : this(
            Array.Empty<byte>(),
            Array.Empty<byte>(),
            Array.Empty<SpannedRecord>(),
            Array.Empty<FontHandleVariantSet>(),
            Array.Empty<IDalamudTextureWrap>(),
            Array.Empty<ISpannableTemplate>())
    {
        this.textStream = new byte[Encoding.UTF8.GetByteCount(data)];
        Encoding.UTF8.GetBytes(data, this.textStream);
    }

    /// <summary>Initializes a new instance of the <see cref="StyledText"/> class.</summary>
    /// <param name="data">The plain UTF-8 text.</param>
    public StyledText(ReadOnlySpan<byte> data)
        : this(
            data.ToArray(),
            Array.Empty<byte>(),
            Array.Empty<SpannedRecord>(),
            Array.Empty<FontHandleVariantSet>(),
            Array.Empty<IDalamudTextureWrap>(),
            Array.Empty<ISpannableTemplate>())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StyledText"/> class.</summary>
    /// <param name="data">The plain UTF-8 text.</param>
    public StyledText(byte[] data)
        : this(
            data,
            Array.Empty<byte>(),
            Array.Empty<SpannedRecord>(),
            Array.Empty<FontHandleVariantSet>(),
            Array.Empty<IDalamudTextureWrap>(),
            Array.Empty<ISpannableTemplate>())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StyledText"/> class.</summary>
    /// <param name="textStream">The text storage.</param>
    /// <param name="dataStream">The link strorage.</param>
    /// <param name="records">The spans.</param>
    /// <param name="fontSets">The font sets.</param>
    /// <param name="textures">The textures.</param>
    /// <param name="spannables">The callbacks.</param>
    internal StyledText(
        byte[] textStream,
        byte[] dataStream,
        SpannedRecord[] records,
        FontHandleVariantSet[] fontSets,
        IDalamudTextureWrap?[] textures,
        ISpannableTemplate?[] spannables)
    {
        this.textStream = textStream;
        this.dataStream = dataStream;
        this.records = records;
        this.fontSets = fontSets;
        this.textures = textures;
        this.spannables = spannables;

        this.hashCode = new(
            () =>
            {
                var hc = default(HashCode);
                hc.AddBytes(this.textStream);
                hc.AddBytes(this.dataStream);
                hc.AddBytes(MemoryMarshal.Cast<SpannedRecord, byte>(this.records));
                foreach (var x in this.fontSets)
                    hc.Add(x);
                foreach (var x in this.textures)
                    hc.Add(x);
                foreach (var x in this.spannables)
                    hc.Add(x);
                return hc.ToHashCode();
            });
    }

    /// <summary>Gets an empty instance of <see cref="StyledText"/>.</summary>
    public static StyledText Empty { get; } = new(Array.Empty<byte>());

    /// <summary>Gets the font handle sets.</summary>
    public IList<FontHandleVariantSet> FontHandleSets
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.fontSets;
    }

    /// <summary>Gets the textures.</summary>
    public IList<IDalamudTextureWrap?> Textures
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.textures;
    }

    /// <summary>Gets the callbacks.</summary>
    public IList<ISpannableTemplate?> Spannables
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.spannables;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(StyledText? left, StyledText? right) => Equals(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(StyledText? left, StyledText? right) => !Equals(left, right);

    /// <inheritdoc/>
    public override IReadOnlyList<ISpannableTemplate?> GetChildrenTemplates() => this.spannables;

    /// <inheritdoc/>
    public bool Equals(StyledText? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        if (this.hashCode.Value != other.hashCode.Value)
            return false;
        return this.textStream.AsSpan().SequenceEqual(other.textStream.AsSpan())
               && this.dataStream.AsSpan().SequenceEqual(other.dataStream.AsSpan())
               && this.records.AsSpan().SequenceEqual(other.records.AsSpan())
               && this.fontSets.AsSpan().SequenceEqual(other.fontSets.AsSpan())
               && this.textures.AsSpan().SequenceEqual(other.textures.AsSpan())
               && this.spannables.AsSpan().SequenceEqual(other.spannables.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || (obj is StyledText other && this.Equals(other));

    /// <inheritdoc/>
    public override string ToString() => this.ToString(null);

    /// <inheritdoc/>
    public override int GetHashCode() => this.hashCode.Value;

    /// <inheritdoc/>
    private protected override DataMemory AsMemory() =>
        new(this.textStream, this.dataStream, this.records, this.fontSets, this.textures, this.spannables);
}
