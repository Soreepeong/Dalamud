using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Dalamud.Interface.Spannables.Helpers;

/// <summary>Extension methods for everything under <see cref="Spannables"/>.</summary>
public static class SpannableExtensions
{
    /// <summary>Transforms spannable-local coordiantes to screen coordinates.</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="p">The point to transform.</param>
    /// <returns>The transformed coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 PointToScreen(this Spannable measurement, Vector2 p) =>
        Vector2.Transform(p, measurement.FullTransformation);

    /// <summary>Transforms screen coordiantes to spannable-local coordinates.</summary>
    /// <param name="measurement">The spannable measurement.</param>
    /// <param name="p">The point to transform.</param>
    /// <returns>The transformed coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 PointToClient(this Spannable measurement, Vector2 p) =>
        Vector2.Transform(
            p,
            Matrix4x4.Invert(measurement.FullTransformation, out var inverted)
                ? inverted
                : Matrix4x4.Identity);

    /// <summary>Copies the this value to an out parameter, for extracting a temporary value in a inline list or
    /// dictionary definition.</summary>
    /// <param name="value">Source value.</param>
    /// <param name="outValue">Value copy destination.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The same <paramref name="value"/> for method chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetAsOut<T>(this T value, out T outValue)
        where T : Spannable => outValue = value;

    /// <summary>Enumerates all items in the hierarchy under a spannable, including itself.</summary>
    /// <param name="root">The root item to enumerate.</param>
    /// <returns>An enumerable that enumerates through all spannables under the root, including itself.</returns>
    public static IEnumerable<Spannable> EnumerateHierarchy(this Spannable root)
    {
        yield return root;
        foreach (var s in root.EnumerateChildren(true))
        {
            foreach (var child in s.EnumerateHierarchy())
                yield return child;
        }
    }

    /// <summary>Enumerates all items in the hierarchy under a spannable, including itself.</summary>
    /// <param name="root">The root item to enumerate.</param>
    /// <typeparam name="T">The type of spannable interested.</typeparam>
    /// <returns>An enumerable that enumerates through all spannables under the root, including itself.</returns>
    public static IEnumerable<T> EnumerateHierarchy<T>(this Spannable root) where T : Spannable
    {
        if (root is T roott)
            yield return roott;
        foreach (var s in root.EnumerateChildren(true))
        {
            foreach (var child in s.EnumerateHierarchy<T>())
                yield return child;
        }
    }
}