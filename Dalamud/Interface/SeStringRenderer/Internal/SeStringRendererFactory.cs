using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Data;
using Dalamud.Data.SeStringEvaluation.Internal;
using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Interface.Internal;
using Dalamud.IoC;
using Dalamud.IoC.Internal;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

using ImGuiNET;

using Lumina.Data.Files;

namespace Dalamud.Interface.SeStringRenderer.Internal;

/// <summary>Custom text renderer factory.</summary>
[ServiceManager.EarlyLoadedService]
[PluginInterface]
[InterfaceVersion("1.0")]
#pragma warning disable SA1015
[ResolveVia<ISeStringRendererFactory>]
#pragma warning restore SA1015
internal class SeStringRendererFactory : IServiceType, IDisposable, ISeStringRendererFactory
{
    [ServiceManager.ServiceDependency]
    private readonly SeStringEvaluator evaluator = Service<SeStringEvaluator>.Get();

    [ServiceManager.ServiceDependency]
    private readonly DataManager dataManager = Service<DataManager>.Get();

    private readonly SeStringRenderer?[] rendererPool;
    private readonly MemoryStream?[] memoryStreamPool;
    private readonly byte[] gfdFile;
    private readonly IDalamudTextureWrap[] gfdTextures;

    private bool disposing;

    [ServiceManager.ServiceConstructor]
    private SeStringRendererFactory(InterfaceManager.InterfaceManagerWithScene imws, TextureManager textureManager)
    {
        this.rendererPool = new SeStringRenderer?[64];
        this.memoryStreamPool = new MemoryStream?[64];

        var t = this.dataManager.GetFile("common/font/gfdata.gfd")!.Data;
        t.CopyTo((this.gfdFile = GC.AllocateUninitializedArray<byte>(t.Length, true)).AsSpan());
        this.gfdTextures =
            new[]
                {
                    "common/font/fonticon_xinput.tex",
                    "common/font/fonticon_ps3.tex",
                    "common/font/fonticon_ps4.tex",
                    "common/font/fonticon_ps5.tex",
                    "common/font/fonticon_lys.tex",
                }
                .Select(x => textureManager.GetTexture(this.dataManager.GetFile<TexFile>(x)!))
                .ToArray();
    }

    /// <summary>Gets the textures for graphic font icons.</summary>
    internal ReadOnlySpan<IDalamudTextureWrap> GfdTextures => this.gfdTextures;

    /// <summary>Gets the GFD file view.</summary>
    internal unsafe GfdFileView GfdFileView => new(new(Unsafe.AsPointer(ref this.gfdFile[0]), this.gfdFile.Length));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposing)
            return;

        this.disposing = true;
        foreach (ref var p in this.rendererPool.AsSpan())
        {
            p?.DisposeInternal();
            p = null;
        }

        foreach (var t in this.gfdTextures)
            t.Dispose();
    }

    /// <inheritdoc/>
    public ISeStringRenderer RentForMeasuring()
    {
        var instance = this.RentCore();
        instance.Initialize(default, false, default);
        return instance;
    }

    /// <inheritdoc/>
    public ISeStringRenderer RentForDrawing(ImDrawListPtr drawListPtr)
    {
        var instance = this.RentCore();
        instance.Initialize(drawListPtr, false, default);
        return instance;
    }

    /// <inheritdoc/>
    public unsafe ISeStringRenderer RentAsItem(ReadOnlySpan<byte> label)
    {
        uint globalId;
        fixed (byte* p = label)
            globalId = ImGuiNative.igGetID_StrStr(p, p + label.Length);

        var instance = this.RentCore();
        instance.Initialize(ImGui.GetWindowDrawList(), true, globalId);
        return instance;
    }

    /// <inheritdoc/>
    public ISeStringRenderer RentAsItem(ReadOnlySpan<char> label)
    {
        Span<byte> buf = stackalloc byte[Encoding.UTF8.GetByteCount(label)];
        Encoding.UTF8.GetBytes(label, buf);
        return this.RentAsItem(buf);
    }

    /// <inheritdoc/>
    public unsafe ISeStringRenderer RentAsItem(nint id)
    {
        var globalId = ImGuiNative.igGetID_Ptr((void*)id);
        var instance = this.RentCore();
        instance.Initialize(ImGui.GetWindowDrawList(), true, globalId);
        return instance;
    }

    /// <inheritdoc/>
    public ISeStringRenderer RentAsDummy()
    {
        var instance = this.RentCore();
        instance.Initialize(ImGui.GetWindowDrawList(), true, 0u);
        return instance;
    }

    /// <summary>Rents a memory stream.</summary>
    /// <returns>The rented memory stream.</returns>
    internal MemoryStream RentMemoryStream()
    {
        ThreadSafety.DebugAssertMainThread();

        foreach (ref var x in this.memoryStreamPool.AsSpan())
        {
            if (x is not null)
            {
                var instance = x;
                x = null;
                return instance;
            }
        }

        return new();
    }

    /// <summary>Returns the finished instance of <see cref="SeStringRenderer"/>.</summary>
    /// <param name="renderer">The instance to return.</param>
    /// <remarks>For use with <see cref="SeStringRenderer.Dispose"/>.</remarks>
    internal void Return(SeStringRenderer? renderer)
    {
        if (renderer is null)
            return;
        if (!this.disposing)
        {
            foreach (ref var x in this.rendererPool.AsSpan())
            {
                if (x is null)
                {
                    x = renderer;
                    return;
                }
            }
        }

        renderer.DisposeInternal();
    }

    /// <summary>Returns an instance of <see cref="MemoryStream"/>.</summary>
    /// <param name="memoryStream">The instance to return.</param>
    /// <remarks>For use with <see cref="SeStringRenderer.Dispose"/>.</remarks>
    internal void Return(MemoryStream? memoryStream)
    {
        if (memoryStream is null)
            return;

        if (!this.disposing)
        {
            foreach (ref var x in this.memoryStreamPool.AsSpan())
            {
                if (x is null)
                {
                    memoryStream.Position = 0;
                    memoryStream.SetLength(0);
                    x = memoryStream;
                    return;
                }
            }
        }
    }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

    /// <summary>Default handler for non-text/link payloads.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="renderer">The current renderer.</param>
    /// <param name="payload">The current payload.</param>
    /// <param name="context">The current context.</param>
    internal unsafe void DefaultResolvePayload<TContext>(
        SeStringRenderer renderer,
        SePayloadReadOnlySpan payload,
        ref TContext context)
        where TContext : ISeStringContext
    {
        fixed (TContext* pContext = &context)
        {
            var ctx = new DalamudTextRendererContext<TContext>(renderer, this.evaluator, pContext);
            this.evaluator.ResolveStringPayload(ref ctx, payload);
        }
    }

    private SeStringRenderer RentCore()
    {
        ThreadSafety.DebugAssertMainThread();

        foreach (ref var x in this.rendererPool.AsSpan())
        {
            if (x is not null)
            {
                var instance = x;
                x = null;
                return instance;
            }
        }

        return new(this);
    }

    private readonly unsafe struct DalamudTextRendererContext<TContext> : ISeStringContext
        where TContext : ISeStringContext
    {
        private readonly SeStringRenderer renderer;
        private readonly SeStringEvaluator evaluator;

        private readonly TContext* wrappedContextPtr;

        public DalamudTextRendererContext(
            SeStringRenderer renderer,
            SeStringEvaluator evaluator,
            TContext* wrappedContextPtr)
        {
            this.renderer = renderer;
            this.evaluator = evaluator;
            this.wrappedContextPtr = wrappedContextPtr;
        }

        /// <inheritdoc/>
        public ClientLanguage SheetLanguage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->SheetLanguage;
        }

        /// <inheritdoc/>
        public bool PreferProduceInChar
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->PreferProduceInChar;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlaceholderNum(byte exprType, out uint value) =>
            this.wrappedContextPtr->TryGetPlaceholderNum(exprType, out value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProducePlaceholder<TContext1>(byte exprType, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProducePlaceholder(exprType, ref targetContext);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePlaceholder(byte exprType, uint value) =>
            this.wrappedContextPtr->UpdatePlaceholder(exprType, value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetLNum(parameterIndex, out value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceLStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceLStr(parameterIndex, ref targetContext);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetGNum(parameterIndex, out value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceGStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceGStr(parameterIndex, ref targetContext);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<byte> value)
        {
            this.wrappedContextPtr->ProduceString(value);
            this.renderer.AddText(value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<char> value)
        {
            this.wrappedContextPtr->ProduceString(value);
            this.renderer.AddText(value);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceSeString(SeStringReadOnlySpan value) =>
            this.evaluator.ResolveString(ref Unsafe.AsRef(in this), value);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceError(string msg) => this.wrappedContextPtr->ProduceError(msg);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceNewLine()
        {
            this.wrappedContextPtr->ProduceNewLine();
            this.renderer.AddNewLine(SeStringRendererParams.NewLineType.SePayload);
        }

        /// <inheritdoc/>
        public bool HandlePayload<TContext1>(SePayloadReadOnlySpan payload, ref TContext1 context)
            where TContext1 : ISeStringContext
        {
            if (this.wrappedContextPtr->HandlePayload(payload, ref context))
                return true;
            if (payload.MacroCode == MacroCode.Link)
            {
                this.renderer.SetActiveLinkPayload(payload);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void PushForeColor(uint colorBgra)
        {
            this.wrappedContextPtr->PushForeColor(colorBgra);
            this.renderer.ForeColorStack.Push(this.renderer.DesignParams.ForeColorU32);
            var color = BgraToRgba(colorBgra);
            this.renderer.DesignParams = this.renderer.DesignParams with { ForeColorU32 = color };
        }

        /// <inheritdoc/>
        public void PopForeColor()
        {
            this.wrappedContextPtr->PopForeColor();
            if (this.renderer.ForeColorStack.TryPop(out var color))
                this.renderer.DesignParams = this.renderer.DesignParams with { ForeColorU32 = color };
        }

        /// <inheritdoc/>
        public void PushBorderColor(uint colorBgra)
        {
            this.wrappedContextPtr->PushBorderColor(colorBgra);
            this.renderer.BorderColorStack.Push(this.renderer.DesignParams.BorderColorU32);
            var color = BgraToRgba(colorBgra);
            this.renderer.DesignParams = this.renderer.DesignParams with { BorderColorU32 = color };
        }

        /// <inheritdoc/>
        public void PopBorderColor()
        {
            this.wrappedContextPtr->PopBorderColor();
            if (this.renderer.BorderColorStack.TryPop(out var color))
                this.renderer.DesignParams = this.renderer.DesignParams with { BorderColorU32 = color };
        }

        /// <inheritdoc/>
        public void SetBold(bool useBold)
        {
            this.wrappedContextPtr->SetBold(useBold);
        }

        /// <inheritdoc/>
        public void SetItalic(bool useItalic)
        {
            this.wrappedContextPtr->SetItalic(useItalic);
            this.renderer.DesignParams = this.renderer.DesignParams with { Italic = useItalic };
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawIcon(uint iconId)
        {
            this.wrappedContextPtr->DrawIcon(iconId);
            this.renderer.AddGfdIcon(iconId);
        }

        private static uint BgraToRgba(uint x)
        {
            var buf = (byte*)&x;
            (buf[0], buf[2]) = (buf[2], buf[0]);
            return x;
        }
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}
