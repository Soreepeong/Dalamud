using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dalamud.Interface.Textures.TextureWraps;

using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.STD;

using Lumina.Data.Files;

namespace Dalamud.Game.Gui.Uld;

/// <summary>Wrapper around <see cref="UldFile"/> and <see cref="AtkUldManager"/> for working with predefined UI
/// elements' layout definitions and assets.</summary>
/// <remarks>Calling <see cref="IDisposable.Dispose"/> will remove the custom elements added.</remarks>
public interface IUldManager : IDisposable
{
    /// <summary>Type definition for <see cref="IUldManager.PartLists"/>.</summary>
    public interface IPartListDictionary :
        IDictionary<uint, IUldPartList>, IReadOnlyDictionary<uint, IUldPartList>, ICollection
    {
        /// <inheritdoc cref="ICollection{T}.Count"/>
        new int Count { get; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.Keys"/>
        new ICollection<uint> Keys { get; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.Values"/>
        new ICollection<IUldPartList> Values { get; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.this"/>
        new IUldPartList this[uint key] { get; }
        
        /// <inheritdoc cref="IDictionary{TKey,TValue}.ContainsKey"/>
        new bool ContainsKey(uint key);

        /// <inheritdoc cref="IDictionary{TKey,TValue}.TryGetValue"/>
        new bool TryGetValue(uint key, [MaybeNullWhen(false)] out IUldPartList value);
    }

    /// <summary>Type definition for <see cref="IUldManager.Assets"/>.</summary>
    public interface IAssetDictionary :
        IDictionary<uint, IUldAsset>, IReadOnlyDictionary<uint, IUldAsset>, ICollection
    {
        /// <inheritdoc cref="ICollection{T}.Count"/>
        new int Count { get; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.Keys"/>
        new ICollection<uint> Keys { get; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.Values"/>
        new ICollection<IUldAsset> Values { get; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.this"/>
        new IUldAsset this[uint key] { get; set; }

        /// <inheritdoc cref="IDictionary{TKey,TValue}.ContainsKey"/>
        new bool ContainsKey(uint key);

        /// <inheritdoc cref="IDictionary{TKey,TValue}.TryGetValue"/>
        new bool TryGetValue(uint key, [MaybeNullWhen(false)] out IUldAsset value);
    }

    /// <summary>Gets the address to the native object that can be assigned to game internal fields.</summary>
    nint Address { get; }

    /// <summary>Gets all the defined part lists.</summary>
    IPartListDictionary PartLists { get; }

    /// <summary>Gets all the defined assets.</summary>
    IAssetDictionary Assets { get; }

    /// <summary>Adds an asset from a texture wrap.</summary>
    /// <param name="textureWrap">Texture wrap to add as an asset.</param>
    /// <returns>Newly added asset.</returns>
    /// <remarks>Life of the underlying texture will be extended until the returned asset is removed or this manager
    /// is cleaned up.</remarks>
    IUldAsset AddAsset(IDalamudTextureWrap textureWrap);

    /// <summary>Attempts to add an asset from a texture wrap.</summary>
    /// <param name="asset">Newly added or already existing asset.</param>
    /// <param name="id">ID of the asset to add.</param>
    /// <param name="textureWrap">Texture wrap to add as an asset.</param>
    /// <returns><c>true</c> if a new asset has been added; <c>false</c> if an asset with the given ID already exists.
    /// </returns>
    bool TryAddAsset(out IUldAsset asset, uint id, IDalamudTextureWrap textureWrap);

    /// <summary>Adds an empty part list.</summary>
    /// <returns>Newly added part list.</returns>
    IUldPartList AddPartList();

    /// <summary>Attempts to add an empty part list.</summary>
    /// <param name="partsList">Newly added or already existing part list.</param>
    /// <param name="id">ID of the part list to add.</param>
    /// <returns><c>true</c> if a new part list has been added; <c>false</c> if a part list with the given ID already
    /// exists.</returns>
    bool TryAddPartList(out IUldPartList partsList, uint id);
}

/// <summary>Wraps <see cref="AtkUldManager"/>.</summary>
internal sealed class UldManager : IUldManager
{
    private readonly Dictionary<uint, AtkUldPartsList?> customPartListIds = [];
    private readonly Dictionary<uint, AtkUldAsset?> customAssetIds = [];
    private readonly Dictionary<uint, UldPartList> wrappedPartLists = [];
    private readonly unsafe AtkUldManager* native;

    /// <summary>Initializes a new instance of the <see cref="UldManager"/> class.</summary>
    /// <param name="componentBase">An instance of <see cref="AtkComponentBase"/> to initialize from.</param>
    public unsafe UldManager(AtkComponentBase* componentBase)
        : this(&componentBase->UldManager)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UldManager"/> class.</summary>
    /// <param name="unitBase">An instance of <see cref="AtkUnitBase"/> to initialize from.</param>
    public unsafe UldManager(AtkUnitBase* unitBase)
        : this(&unitBase->UldManager)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UldManager"/> class.</summary>
    /// <param name="native">An instance of <see cref="AtkUldManager"/> to initialize from.</param>
    public unsafe UldManager(AtkUldManager* native)
    {
        this.native = native;
        this.PartLists = new PartListsDictionary(this);
    }

    /// <inheritdoc/>
    public unsafe nint Address => (nint)this.native;

    /// <inheritdoc/>
    public IUldManager.IPartListDictionary PartLists { get; }

    /// <inheritdoc/>
    public IUldManager.IAssetDictionary Assets { get; }

    private unsafe Span<AtkUldPartsList> PartListSpan => new(this.native->PartsList, this.native->PartsListCount);

    /// <inheritdoc/>
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IUldAsset AddAsset(IDalamudTextureWrap textureWrap)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public bool TryAddAsset(out IUldAsset asset, uint id, IDalamudTextureWrap textureWrap)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public IUldPartList AddPartList()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public unsafe bool TryAddPartList(out IUldPartList partsList, uint id)
    {
        var buf = IMemorySpace.GetDefaultSpace()->Malloc(
            (ulong)((this.native->PartsListCount + 1) * sizeof(AtkUldPartsList)),
            (ulong)sizeof(nint));
        if (buf is null)
            throw new OutOfMemoryException();

        this.PartListSpan.CopyTo(new(buf, MemoryMarshal.Cast<AtkUldPartsList, byte>(this.PartListSpan).Length));
        if (this.native->PartsList is not null)
            IMemorySpace.Free(this.native->PartsList);
        this.native->PartsList = (AtkUldPartsList*)buf;
        ref var last = ref this.native->PartsList[this.native->PartsListCount];
        last = new()
        {
            Id = id,
            PartCount = 0,
            Parts = null,
        };
        this.native->PartsListCount++;
        partsList = this.WrapPartList(ref last);
        this.ReassignWrappedPartListAddresses();
        return true;
    }

    private unsafe IUldPartList WrapPartList(ref AtkUldPartsList native2)
    {
        if (this.wrappedPartLists.TryGetValue(native2.Id, out var value))
            return value;
        return this.wrappedPartLists[native2.Id] = new(this, (AtkUldPartsList*)Unsafe.AsPointer(ref native2));
    }

    private unsafe void CleanupPartList(IUldPartList w)
    {
        throw new NotImplementedException("destruct");
    }

    private unsafe void ReassignWrappedPartListAddresses()
    {
        foreach (var w in this.wrappedPartLists.Values)
        {
            foreach (ref var p in this.PartListSpan)
            {
                if (p.Id == w.Id)
                {
                    w.Address = (nint)Unsafe.AsPointer(ref p);
                    break;
                }
            }
        }
    }

    private sealed class PartListsDictionary(UldManager uldm) : IUldManager.IPartListDictionary
    {
        public unsafe int Count => uldm.native->PartsListCount;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public bool IsReadOnly => false;

        public ICollection<uint> Keys { get; } = new KeyCollection(uldm);

        public ICollection<IUldPartList> Values { get; } = new ValueCollection(uldm);

        IEnumerable<uint> IReadOnlyDictionary<uint, IUldPartList>.Keys => this.Keys;

        IEnumerable<IUldPartList> IReadOnlyDictionary<uint, IUldPartList>.Values => this.Values;

        public unsafe IUldPartList this[uint key]
        {
            get
            {
                var p = uldm.native->PartsList;
                for (var i = 0; i < uldm.native->PartsListCount; i++, p++)
                {
                    if (p->Id == key)
                        return uldm.WrapPartList(ref *p);
                }

                if (uldm.wrappedPartLists.Remove(key, out var value))
                    uldm.CleanupPartList(value);
                throw new KeyNotFoundException();
            }

            set
            {
                if (!uldm.wrappedPartLists.TryGetValue(key, out var value2) || !ReferenceEquals(value, value2))
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        public IEnumerator<KeyValuePair<uint, IUldPartList>> GetEnumerator()
        {
            for (var i = 0; i < uldm.PartListSpan.Length; i++)
                yield return new(uldm.PartListSpan[i].Id, uldm.WrapPartList(ref uldm.PartListSpan[i]));
        }

        public void Add(KeyValuePair<uint, IUldPartList> item) => this.Add(item.Key, item.Value);

        public unsafe void Clear()
        {
            foreach (var p in uldm.PartListSpan)
            {
                if (uldm.wrappedPartLists.TryGetValue(p.Id, out var w))
                    uldm.CleanupPartList(w);
            }

            uldm.native->PartsListCount = 0;
        }

        public bool Contains(KeyValuePair<uint, IUldPartList> item) =>
            this.TryGetValue(item.Key, out var v) && v == item.Value;

        public void CopyTo(KeyValuePair<uint, IUldPartList>[] array, int arrayIndex)
        {
            var source = uldm.PartListSpan;
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if (array.Length < arrayIndex + source.Length)
                throw new ArgumentException(null, nameof(arrayIndex));

            var target = array.AsSpan(arrayIndex);
            for (var i = 0; i < source.Length; i++)
                target[i] = new(source[i].Id, uldm.WrapPartList(ref source[i]));
        }

        public bool Remove(KeyValuePair<uint, IUldPartList> item) =>
            this.TryGetValue(item.Key, out var v) && v == item.Value && this.Remove(item.Key);

        public void CopyTo(Array array, int arrayIndex)
        {
            var source = uldm.PartListSpan;
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if (array.Length < arrayIndex + source.Length)
                throw new ArgumentException(null, nameof(arrayIndex));

            for (var i = 0; i < source.Length; i++)
            {
                array.SetValue(
                    new KeyValuePair<uint, IUldPartList>(source[i].Id, uldm.WrapPartList(ref source[i])),
                    arrayIndex + i);
            }
        }

        public void Add(uint key, IUldPartList value) => throw new NotSupportedException();

        public bool ContainsKey(uint key)
        {
            foreach (ref readonly var p in uldm.PartListSpan)
            {
                if (p.Id == key)
                    return true;
            }

            return false;
        }

        public bool TryGetValue(uint key, out IUldPartList value)
        {
            foreach (ref var p in uldm.PartListSpan)
            {
                if (p.Id != key)
                    continue;

                value = uldm.WrapPartList(ref p);
                return true;
            }

            value = null!;
            return false;
        }

        public unsafe bool Remove(uint key)
        {
            var span = uldm.PartListSpan;
            for (var i = 0; i < span.Length; i++)
            {
                var p = span[i];
                if (p.Id != key)
                    continue;

                if (uldm.wrappedPartLists.TryGetValue(p.Id, out var w))
                    uldm.CleanupPartList(w);
                span[(i + 1)..].CopyTo(span[i..]);
                uldm.native->PartsListCount--;
                uldm.ReassignWrappedPartListAddresses();
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private sealed class KeyCollection(UldManager uldm) : ICollection<uint>
        {
            public unsafe int Count => uldm.native->PartsListCount;

            public bool IsReadOnly => true;

            public IEnumerator<uint> GetEnumerator()
            {
                for (var i = 0; i < uldm.PartListSpan.Length; i++)
                    yield return uldm.PartListSpan[i].Id;
            }

            public bool Contains(uint item)
            {
                foreach (ref readonly var p in uldm.PartListSpan)
                {
                    if (p.Id == item)
                        return true;
                }

                return false;
            }

            public void CopyTo(uint[] array, int arrayIndex)
            {
                var source = uldm.PartListSpan;
                ArgumentNullException.ThrowIfNull(array);
                ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
                if (array.Length < arrayIndex + source.Length)
                    throw new ArgumentException(null, nameof(arrayIndex));

                var target = array.AsSpan(arrayIndex);
                for (var i = 0; i < source.Length; i++)
                    target[i] = source[i].Id;
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

            public void Add(uint item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Remove(uint item) => throw new NotSupportedException();
        }

        private sealed class ValueCollection(UldManager uldm) : ICollection<IUldPartList>
        {
            public unsafe int Count => uldm.native->PartsListCount;

            public bool IsReadOnly => true;

            public IEnumerator<IUldPartList> GetEnumerator()
            {
                for (var i = 0; i < uldm.PartListSpan.Length; i++)
                    yield return uldm.WrapPartList(ref uldm.PartListSpan[i]);
            }

            public bool Contains(IUldPartList item)
            {
                if (item.Owner != uldm)
                    return false;

                var id = item.Id;
                foreach (ref readonly var p in uldm.PartListSpan)
                {
                    if (p.Id == id)
                        return true;
                }

                return false;
            }

            public void CopyTo(IUldPartList[] array, int arrayIndex)
            {
                var source = uldm.PartListSpan;
                ArgumentNullException.ThrowIfNull(array);
                ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
                if (array.Length < arrayIndex + source.Length)
                    throw new ArgumentException(null, nameof(arrayIndex));

                var target = array.AsSpan(arrayIndex);
                for (var i = 0; i < source.Length; i++)
                    target[i] = uldm.WrapPartList(ref source[i]);
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

            public void Add(IUldPartList item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Remove(IUldPartList item) => throw new NotSupportedException();
        }
    }
}
