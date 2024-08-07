using System.Numerics;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Data.Parsing.Uld;

namespace Dalamud.Game.Gui.Uld;

/// <summary>Wrapper around <see cref="UldRoot.PartData"/> and <see cref="AtkUldPart"/>, for working with
/// individual textures.</summary>
public interface IUldPart
{
    /// <summary>Gets the owner ULD manager.</summary>
    IUldManager Owner { get; }

    /// <summary>Gets the address to the native object that can be assigned to game internal fields.</summary>
    nint Address { get; }

    /// <summary>Gets the address to the native object that can be assigned to game internal fields.</summary>
    IUldAsset Asset { get; }

    /// <summary>Gets the relative UV0 (top left coordinates) of this part in the texture pointed by
    /// <see cref="Asset"/>.</summary>
    /// <value>Component values should be in the range of 0 to 1.</value>
    Vector2 Uv0 { get; }

    /// <summary>Gets the relative UV0 (bottom right coordinates) of this part in the texture pointed by
    /// <see cref="Asset"/>.</summary>
    /// <value>Component values should be in the range of 0 to 1.</value>
    Vector2 Uv1 { get; }

    /// <summary>Gets the size of this part in pixels.</summary>
    Vector2 Size { get; }
}
