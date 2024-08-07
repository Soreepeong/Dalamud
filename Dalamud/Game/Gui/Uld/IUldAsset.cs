using Dalamud.Interface.Textures.TextureWraps;

using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Data.Files;

namespace Dalamud.Game.Gui.Uld;

/// <summary>Wrapper around <see cref="UldFile.PartList"/> and <see cref="AtkUldPartsList"/>, for working with
/// individual textures.</summary>
public interface IUldAsset
{
    /// <summary>Gets the owner ULD manager.</summary>
    IUldManager Owner { get; }

    /// <summary>Gets the address to the native object that can be assigned to game internal fields.</summary>
    nint Address { get; }

    /// <summary>Gets the ID of this asset.</summary>
    nint Id { get; }

    /// <summary>Gets an immediate texture wrap that is valid only for the current frame.</summary>
    IDalamudTextureWrap ImmediateTextureWrap { get; }
}