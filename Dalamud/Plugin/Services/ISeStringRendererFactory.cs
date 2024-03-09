using Dalamud.Interface.SeStringRenderer;
using Dalamud.Interface.SeStringRenderer.Internal;

using ImGuiNET;

namespace Dalamud.Plugin.Services;

/// <summary>Factory for custom text rendering.</summary>
public interface ISeStringRendererFactory
{
    /// <summary>Rents an instance of the <see cref="SeStringRenderer"/> class, for measuring.</summary>
    /// <returns>The rented renderer.</returns>
    ISeStringRenderer RentForMeasuring();

    /// <summary>Rents an instance of the <see cref="SeStringRenderer"/> class, for drawing to a specified instance
    /// of <see cref="ImDrawListPtr"/>.</summary>
    /// <param name="drawListPtr">The draw list to render to.</param>
    /// <returns>The rented renderer.</returns>
    ISeStringRenderer RentForDrawing(ImDrawListPtr drawListPtr);

    /// <summary>Rents an instance of the <see cref="SeStringRenderer"/> class, for drawing an interactable object
    /// to the current window.</summary>
    /// <param name="label">The ImGui ID to track states.</param>
    /// <returns>The rented renderer.</returns>
    ISeStringRenderer RentAsItem(ReadOnlySpan<byte> label);

    /// <summary>Rents an instance of the <see cref="SeStringRenderer"/> class, for drawing an interactable object
    /// to the current window.</summary>
    /// <param name="label">The ImGui ID to track states.</param>
    /// <returns>The rented renderer.</returns>
    ISeStringRenderer RentAsItem(ReadOnlySpan<char> label);

    /// <summary>Rents an instance of the <see cref="SeStringRenderer"/> class, for drawing an interactable object
    /// to the current window.</summary>
    /// <param name="id">The ImGui ID to track states.</param>
    /// <returns>The rented renderer.</returns>
    ISeStringRenderer RentAsItem(nint id);

    /// <summary>Rents an instance of the <see cref="SeStringRenderer"/> class, for drawing an space-filler object
    /// to the current window.</summary>
    /// <returns>The rented renderer.</returns>
    /// <remarks>To use interactions, use the other variants with labels or IDs.</remarks>
    ISeStringRenderer RentAsDummy();
}
