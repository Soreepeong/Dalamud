using System.Runtime.InteropServices;

namespace Dalamud.Interface.Utility.Internal;

/// <summary>Miscellaneous CImGui imports that ImGui.NET did not create bindings for.</summary>
internal static unsafe partial class CImGuiImports
{
    /// <summary>Draws a wrapped text.</summary>
    /// <param name="fmt">Format string.</param>
    /// <param name="valist">Variadic list.</param>
    [LibraryImport("cimgui", EntryPoint = "igTextWrappedV")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    public static partial void TextWrappedV(byte* fmt, void* valist);
}
