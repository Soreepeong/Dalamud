using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Utility;
using Dalamud.Logging.Internal;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;

using ImGuiNET;

namespace Dalamud.Interface.Internal;

/// <summary>
/// Clipboard-related interface.
/// </summary>
public interface IClipboardProvider
{
    /// <summary>
    /// The delegate, called on clipboard change.
    /// </summary>
    /// <param name="source">The source of change.</param>
    public delegate void ClipboardChangeDelegate(ClipboardChangeSource source);
    
    /// <summary>
    /// Invoked on clipboard change.
    /// </summary>
    public event ClipboardChangeDelegate ClipboardChange;
    
    /// <summary>
    /// Source of clipboard change.
    /// </summary>
    public enum ClipboardChangeSource
    {
        /// <summary>
        /// Source is unknown.
        /// </summary>
        Unknown = 0,
        
        /// <summary>
        /// The game has initiated the clipboard change.
        /// </summary>
        Game = 1,
        
        /// <summary>
        /// Some other application else than the game and Dalamud initiated the clipboard change.
        /// </summary>
        External = 2,
        
        /// <summary>
        /// Dalamud or its plugin initiated the clipboard change.
        /// </summary>
        Dalamud = 3,
    }
}

/// <summary>
/// Configures the ImGui clipboard behaviour to work nicely with XIV.
/// </summary>
/// <remarks>
/// <para>
/// XIV uses '\r' for line endings and will truncate all text after a '\n' character.
/// This means that copy/pasting multi-line text from ImGui to XIV will only copy the first line.
/// </para>
/// <para>
/// ImGui uses '\n' for line endings and will ignore '\r' entirely.
/// This means that copy/pasting multi-line text from XIV to ImGui will copy all the text
/// without line breaks.
/// </para>
/// <para>
/// To fix this we normalize all clipboard line endings entering/exiting ImGui to '\r\n' which
/// works for both ImGui and XIV.
/// </para>
/// </remarks>
[ServiceManager.EarlyLoadedService]
internal sealed unsafe class ClipboardProvider : IClipboardProvider, IServiceType, IDisposable
{
    private static readonly ModuleLog Log = new(nameof(ClipboardProvider));
    private readonly nint clipboardUserDataOriginal;
    private readonly nint setTextOriginal;
    private readonly nint getTextOriginal;
    private readonly Hook<ApplyToSystemClipboardDelegate> applyToSystemClipboardHook;

    private GCHandle clipboardUserData;
    private int externalChangeSuppressCounter;

    [ServiceManager.ServiceConstructor]
    private ClipboardProvider(InterfaceManager.InterfaceManagerWithScene imws)
    {
        // Effectively waiting for ImGui to become available.
        _ = imws;
        Debug.Assert(ImGuiHelpers.IsImGuiInitialized, "IMWS initialized but IsImGuiInitialized is false?");

        var io = ImGui.GetIO();
        this.clipboardUserDataOriginal = io.ClipboardUserData;
        this.setTextOriginal = io.SetClipboardTextFn;
        this.getTextOriginal = io.GetClipboardTextFn;
        io.ClipboardUserData = GCHandle.ToIntPtr(this.clipboardUserData = GCHandle.Alloc(this));
        io.SetClipboardTextFn = (nint)(delegate* unmanaged<nint, byte*, void>)&StaticSetClipboardTextImpl;
        io.GetClipboardTextFn = (nint)(delegate* unmanaged<nint, byte*>)&StaticGetClipboardTextImpl;

        this.applyToSystemClipboardHook = Hook<ApplyToSystemClipboardDelegate>.FromFunctionPointerVariable(
            (nint)(&InputManager->ClipboardData.VTable->WriteToSystemClipboard),
            this.ApplyToSystemClipboardDetour);
        this.applyToSystemClipboardHook.Enable();
        return;

        [UnmanagedCallersOnly]
        static void StaticSetClipboardTextImpl(nint userData, byte* text) =>
            ((ClipboardProvider)GCHandle.FromIntPtr(userData).Target)!.SetClipboardString(text);

        [UnmanagedCallersOnly]
        static byte* StaticGetClipboardTextImpl(nint userData) =>
            ((ClipboardProvider)GCHandle.FromIntPtr(userData).Target)!.GetClipboardUtf8String(false).StringPtr;
    }

    private delegate void ApplyToSystemClipboardDelegate(nint cm, nint u1, nint u2);

    /// <inheritdoc/>
    public event IClipboardProvider.ClipboardChangeDelegate? ClipboardChange;

    private static ref AtkInputManager* InputManager => ref *AtkStage.GetSingleton()->AtkInputManager;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.clipboardUserData.IsAllocated)
            return;

        var io = ImGui.GetIO();
        io.SetClipboardTextFn = this.setTextOriginal;
        io.GetClipboardTextFn = this.getTextOriginal;
        io.ClipboardUserData = this.clipboardUserDataOriginal;

        this.applyToSystemClipboardHook.Dispose();
        this.clipboardUserData.Free();
    }

    /// <summary>
    /// Sets the clipboard text, from a <see cref="SeString"/>, payloads intact.
    /// </summary>
    /// <param name="seString">The string.</param>
    public void SetClipboardSeString(SeString seString)
    {
        using var ms = new MemoryStream();
        ms.Write(seString.Encode());
        ms.WriteByte(0);
        fixed (byte* p = ms.GetBuffer())
            this.SetClipboardString(p);
    }

    /// <summary>
    /// Sets the clipboard text.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <exception cref="ArgumentException">If text includes a null character.</exception>
    public void SetClipboardString(ReadOnlySpan<char> text)
    {
        if (text.IndexOf((char)0) != -1)
            throw new ArgumentException("Cannot contain a null character (\\0).", nameof(text));

        var len = Encoding.UTF8.GetByteCount(text);
        var bytes = len <= 512 ? stackalloc byte[len + 1] : new byte[len + 1];
        Encoding.UTF8.GetBytes(text, bytes);
        bytes[len] = 0;

        fixed (byte* p = bytes)
            this.SetClipboardString(p);
    }

    /// <summary>
    /// Sets the clipboard text. It may include payloads; they must be correctly encoded.
    /// </summary>
    /// <param name="text">The string.</param>
    public void SetClipboardString(byte* text)
    {
        ThreadSafety.AssertMainThread();
        this.applyToSystemClipboardHook.Disable();
        
        var inpman = InputManager;
        inpman->CopyBufferRaw.SetString(text);
        inpman->ClipboardData.WriteToSystemClipboard(&inpman->CopyBufferRaw, &inpman->CopyBufferFiltered);
        
        this.applyToSystemClipboardHook.Enable();
        this.InvokeClipboardChangedCallback(IClipboardProvider.ClipboardChangeSource.Dalamud);
    }

    /// <summary>
    /// Gets the clipboard string.
    /// </summary>
    /// <returns>The string.</returns>
    public string GetClipboardString() => Encoding.UTF8.GetString(this.GetClipboardUtf8String(false).AsSpan());

    /// <summary>
    /// Gets the clipboard SeString.
    /// </summary>
    /// <returns>The SeString.</returns>
    public SeString GetClipboardSeString() => SeString.Parse(this.GetClipboardUtf8String(true).AsSpan());

    /// <summary>
    /// Invokes <see cref="ClipboardChange"/>.
    /// </summary>
    /// <param name="source">The source of clipboard change.</param>
    internal void InvokeClipboardChangedCallback(IClipboardProvider.ClipboardChangeSource source)
    {
        if (source == IClipboardProvider.ClipboardChangeSource.External)
        {
            if (this.externalChangeSuppressCounter > 0)
            {
                this.externalChangeSuppressCounter--;
                return;
            }
        }
        else
        {
            this.externalChangeSuppressCounter++;
        }

        this.ClipboardChange?.Invoke(source);
    }

    private ref Utf8String GetClipboardUtf8String(bool enablePayloads)
    {
        ThreadSafety.AssertMainThread();

        var inpman = InputManager;
        ref var sysClip = ref *inpman->ClipboardData.GetSystemClipboardText();
        if (enablePayloads && inpman->CopyBufferFiltered.AsSpan().SequenceEqual(sysClip.AsSpan()))
            return ref inpman->CopyBufferRaw;

        return ref sysClip;
    }

    private void ApplyToSystemClipboardDetour(nint cm, nint u1, nint u2)
    {
        this.applyToSystemClipboardHook.Original.Invoke(cm, u1, u2);
        this.InvokeClipboardChangedCallback(IClipboardProvider.ClipboardChangeSource.Game);
    }
}
