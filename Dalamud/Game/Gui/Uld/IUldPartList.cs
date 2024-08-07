using System.Collections.Generic;
using System.Numerics;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Data.Files;

namespace Dalamud.Game.Gui.Uld;

/// <summary>Wrapper around <see cref="UldFile.PartList"/> and <see cref="AtkUldPartsList"/>, for working with a set
/// of <a href="https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_images/Implementing_image_sprites_in_CSS">image
/// sprites</a>.</summary>
public interface IUldPartList
{
    /// <summary>Gets the owner ULD manager.</summary>
    IUldManager Owner { get; }

    /// <summary>Gets the address to the native object that can be assigned to game internal fields.</summary>
    nint Address { get; }

    /// <summary>Gets the ID of this part list.</summary>
    uint Id { get; }

    /// <summary>Gets the parts.</summary>
    IReadOnlyDictionary<uint, IUldPart> Parts { get; }

    /// <summary>Adds a part to this part list.</summary>
    /// <param name="asset">Asset to add.</param>
    /// <param name="uv0">Relative UV0 of the sprite in the texture pointed by <paramref name="asset"/>.
    /// Each component should be in the range of 0 to 1.</param>
    /// <param name="uv1">Relative UV1 of the sprite in the texture pointed by <paramref name="asset"/>.
    /// Each component should be in the range of 0 to 1.</param>
    /// <param name="size">Size of the sprite in pixels.</param>
    /// <returns>Newly added part.</returns>
    IUldPart AddPart(IUldAsset asset, Vector2? uv0 = null, Vector2? uv1 = null, Vector2? size = null);

    /// <summary>Adds a part to this part list.</summary>
    /// <param name="part">Newly added or already existing part.</param>
    /// <param name="asset">Asset to add.</param>
    /// <param name="uv0">Relative UV0 of the sprite in the texture pointed by <paramref name="asset"/>.
    /// Each component should be in the range of 0 to 1.</param>
    /// <param name="uv1">Relative UV1 of the sprite in the texture pointed by <paramref name="asset"/>.
    /// Each component should be in the range of 0 to 1.</param>
    /// <param name="size">Size of the sprite in pixels.</param>
    /// <returns>Newly added part.</returns>
    bool TryAddPart(out IUldPart part, IUldAsset asset, Vector2? uv0 = null, Vector2? uv1 = null, Vector2? size = null);
}

internal sealed unsafe class UldPartList : IUldPartList
{
    private readonly Dictionary<uint, AtkUldPart?> customParts = [];
    private readonly AtkUldPartsList* native;

    public UldPartList(UldManager manager, AtkUldPartsList* native)
    {
        this.native = native;
    }

    public IUldManager Owner => throw new NotImplementedException();

    public nint Address { get; set; }

    public uint Id => throw new NotImplementedException();

    public IReadOnlyDictionary<uint, IUldPart> Parts => throw new NotImplementedException();

    public IUldPart AddPart(IUldAsset asset, Vector2? uv0 = null, Vector2? uv1 = null, Vector2? size = null)
    {
        throw new NotImplementedException();
    }

    public bool TryAddPart(out IUldPart part, IUldAsset asset, Vector2? uv0 = null, Vector2? uv1 = null, Vector2? size = null)
    {
        throw new NotImplementedException();
    }
}
