using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dalamud.Interface.Internal;
using Dalamud.Utility;

using ImGuiNET;

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

using Win32 = TerraFX.Interop.Windows.Windows;

namespace Dalamud.ImGuiScene.Implementations;

/// <summary>
/// Deals with rendering ImGui using DirectX 12.
/// See https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx12.cpp for the original implementation.
/// </summary>
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "DX12")]
[SuppressMessage(
    "StyleCop.CSharp.LayoutRules",
    "SA1519:Braces should not be omitted from multi-line child statement",
    Justification = "Multiple fixed/using scopes")]
internal unsafe partial class Dx12Renderer : IImGuiRenderer
{
    private readonly Dictionary<nint, IImGuiRenderer.DrawCmdUserCallbackDelegate> userCallbacks = new();
    private readonly List<IDalamudTextureWrap> fontTextures = new();

    private readonly ViewportHandler viewportHandler;
    private readonly nint renderNamePtr;

    private ComPtr<TexturePipeline> defaultPipeline;
    private TextureManager? textureManager;
    private ViewportData* mainViewportData;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dx12Renderer"/> class,
    /// from existing swap chain, device, and command queue.
    /// </summary>
    /// <param name="swapChain">The swap chain.</param>
    /// <param name="device">The device.</param>
    /// <param name="commandQueue">The command queue.</param>
    public Dx12Renderer(IDXGISwapChain3* swapChain, ID3D12Device* device, ID3D12CommandQueue* commandQueue)
    {
        if (swapChain is null)
            throw new NullReferenceException($"{nameof(swapChain)} cannot be null.");
        if (device is null)
            throw new NullReferenceException($"{nameof(device)} cannot be null.");
        if (commandQueue is null)
            throw new NullReferenceException($"{nameof(commandQueue)} cannot be null.");

        var io = ImGui.GetIO();
        if (ImGui.GetIO().NativePtr->BackendRendererName is not null)
            throw new InvalidOperationException("ImGui backend renderer seems to be have been already attached.");

        try
        {
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasViewports;

            this.renderNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_dx12_c#");
            io.NativePtr->BackendRendererName = (byte*)this.renderNamePtr;

            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                this.viewportHandler = new(this);

            this.mainViewportData = ViewportData.Create(swapChain, device, commandQueue);
            ImGui.GetPlatformIO().Viewports[0].RendererUserData = (nint)this.mainViewportData;
        }
        catch
        {
            this.ReleaseUnmanagedResources();
            throw;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Dx12Renderer"/> class,
    /// without using any swap buffer, for offscreen rendering.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <param name="rtvFormat">The format of render target.</param>
    /// <param name="numBackBuffers">Number of back buffers.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height of render target.</param>
    public Dx12Renderer(ID3D12Device* device, DXGI_FORMAT rtvFormat, int numBackBuffers, int width, int height)
    {
        if (device is null)
            throw new NullReferenceException($"{nameof(device)} cannot be null.");
        if (rtvFormat == DXGI_FORMAT.DXGI_FORMAT_UNKNOWN)
            throw new ArgumentOutOfRangeException(nameof(rtvFormat), rtvFormat, "Cannot be unknown.");
        if (numBackBuffers < 2)
            throw new ArgumentOutOfRangeException(nameof(numBackBuffers), numBackBuffers, "Must be at least 2.");
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Must be a positive number.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Must be a positive number.");

        var io = ImGui.GetIO();
        if (ImGui.GetIO().NativePtr->BackendRendererName is not null)
            throw new InvalidOperationException("ImGui backend renderer seems to be have been already attached.");

        try
        {
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasViewports;

            this.renderNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_dx12_c#");
            io.NativePtr->BackendRendererName = (byte*)this.renderNamePtr;

            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                this.viewportHandler = new(this);

            this.mainViewportData = ViewportData.Create(device, width, height, rtvFormat, numBackBuffers);
            ImGui.GetPlatformIO().Viewports[0].RendererUserData = (nint)this.mainViewportData;
        }
        catch
        {
            this.ReleaseUnmanagedResources();
            throw;
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Dx12Renderer"/> class.
    /// </summary>
    ~Dx12Renderer() => this.ReleaseUnmanagedResources();

    /// <summary>
    /// Gets the current main render target.
    /// </summary>
    public ID3D12Resource* MainRenderTarget
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);
            return this.mainViewportData->Frames[this.mainViewportData->FrameIndex].RenderTarget;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.ReleaseUnmanagedResources();
        this.textureManager?.Dispose();
        this.textureManager = null;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public void OnNewFrame()
    {
        this.EnsureDeviceObjects();
    }

    /// <inheritdoc/>
    public void OnPreResize()
    {
        ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);
        this.mainViewportData->ResetBuffers();
    }

    /// <inheritdoc/>
    public void OnPostResize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);
        this.mainViewportData->ResizeBuffers(width, height, false);
    }

    /// <inheritdoc/>
    public void RenderDrawData(ImDrawDataPtr drawData)
    {
        ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);
        var noSwapChain = this.mainViewportData->SwapChain.IsEmpty();
        this.textureManager?.FlushPendingTextureUploads();
        this.mainViewportData->Draw(this, drawData, noSwapChain);
        if (noSwapChain)
            this.mainViewportData->WaitForPendingOperations();
    }

    /// <summary>
    /// Creates a new texture pipeline. Dispose using <see cref="ReleaseTexturePipeline"/>.
    /// </summary>
    /// <param name="ps">The pixel shader data.</param>
    /// <param name="samplerDesc">The sampler description.</param>
    /// <returns>The handle to the new texture pipeline.</returns>
    public nint CreateTexturePipeline(ReadOnlySpan<byte> ps, in D3D12_STATIC_SAMPLER_DESC samplerDesc)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var streamVs = assembly.GetManifestResourceStream("imgui-vertex.hlsl.bytes")!;
        var vs = ArrayPool<byte>.Shared.Rent((int)streamVs.Length);
        try
        {
            streamVs.ReadExactly(vs, 0, (int)streamVs.Length);
            return (nint)TexturePipeline.CreateNew(
                this.mainViewportData->Device,
                this.mainViewportData->RtvFormat,
                vs.AsSpan(0, (int)streamVs.Length),
                ps,
                samplerDesc);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(vs);
        }
    }

    /// <inheritdoc/>
    public nint GetTexturePipeline(nint textureHandle)
    {
        if (textureHandle == default)
            throw new NullReferenceException();
        using var ptcd = default(ComPtr<TextureData>);
        ((ComPtr<IUnknown>)(IUnknown*)textureHandle).CopyTo(&ptcd).ThrowHr();
        return (nint)ptcd.Get()->CustomPipeline.Get();
    }

    /// <inheritdoc/>
    public void SetTexturePipeline(nint textureHandle, nint pipelineHandle)
    {
        if (textureHandle == default)
            throw new NullReferenceException();
        using var ptcd = default(ComPtr<TextureData>);
        ((ComPtr<IUnknown>)(IUnknown*)textureHandle).CopyTo(&ptcd).ThrowHr();

        if (pipelineHandle == default)
        {
            ptcd.Get()->CustomPipeline = default;
        }
        else
        {
            using var ppsh = default(ComPtr<TexturePipeline>);
            ((ComPtr<IUnknown>)(IUnknown*)pipelineHandle).CopyTo(&ppsh).ThrowHr();
            ptcd.Get()->CustomPipeline = ppsh;
        }
    }

    /// <inheritdoc/>
    public void ReleaseTexturePipeline(nint pipelineHandle)
    {
        if (pipelineHandle == default)
            throw new NullReferenceException();

        // We call Release twice, since type checking effectively involves a call to AddRef.
        using var ppsh = default(ComPtr<TexturePipeline>);
        ((ComPtr<IUnknown>)(IUnknown*)pipelineHandle).CopyTo(&ppsh).ThrowHr();
        ppsh.Get()->Release();
    }

    /// <inheritdoc/>
    public nint AddDrawCmdUserCallback(IImGuiRenderer.DrawCmdUserCallbackDelegate @delegate)
    {
        if (this.userCallbacks.FirstOrDefault(x => x.Value == @delegate).Key is not 0 and var key)
            return key;

        key = Marshal.GetFunctionPointerForDelegate(@delegate);
        this.userCallbacks.Add(key, @delegate);
        return key;
    }

    /// <inheritdoc/>
    public void RemoveDrawCmdUserCallback(IImGuiRenderer.DrawCmdUserCallbackDelegate @delegate)
    {
        foreach (var key in this.userCallbacks
                                .Where(x => x.Value == @delegate)
                                .Select(x => x.Key)
                                .ToArray())
        {
            this.userCallbacks.Remove(key);
        }
    }

    /// <summary>
    /// Rebuilds font texture.
    /// </summary>
    public void RebuildFontTexture()
    {
        foreach (var fontResourceView in this.fontTextures)
            fontResourceView.Dispose();
        this.fontTextures.Clear();

        this.CreateFontsTexture();
    }

    /// <inheritdoc/>
    public IDalamudTextureWrap LoadTexture(
        ReadOnlySpan<byte> data,
        int pitch,
        int width,
        int height,
        int format)
    {
        ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);
        try
        {
            return (this.textureManager ??= new(this.mainViewportData->Device))
                .CreateTexture(data, pitch, width, height, (DXGI_FORMAT)format);
        }
        catch (COMException e) when (e.HResult == unchecked((int)0x887a0005))
        {
            throw new AggregateException(
                Marshal.GetExceptionForHR(this.mainViewportData->Device.Get()->GetDeviceRemovedReason()) ?? new(),
                e);
        }
    }

    private void RenderDrawDataInternal(ImDrawDataPtr drawData, ID3D12GraphicsCommandList* ctx)
    {
        ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);

        // Avoid rendering when minimized
        if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
            return;

        if (!drawData.Valid || drawData.CmdListsCount == 0)
            return;

        var cmdLists = new Span<ImDrawListPtr>(
            drawData.NativePtr->CmdLists,
            drawData.NativePtr->CmdListsCount);

        ref var vd = ref *(ViewportData*)drawData.OwnerViewport.RendererUserData;
        vd.FrameIndex = (vd.FrameIndex + 1) % vd.NumBackBuffers;
        ref var frameData = ref vd.CurrentViewportFrame;

        // Create and grow vertex/index buffers if needed
        frameData.EnsureVertexBufferCapacity(this.mainViewportData->Device, drawData.TotalVtxCount);
        frameData.EnsureIndexBufferCapacity(this.mainViewportData->Device, drawData.TotalIdxCount);

        // Upload vertex/index data into a single contiguous GPU buffer
        try
        {
            var range = default(D3D12_RANGE); // we don't care about what was in there before
            void* tmp;

            frameData.VertexBuffer.Get()->Map(0, &range, &tmp).ThrowHr();
            var targetVertices = new Span<ImDrawVert>(tmp, (int)frameData.VertexBufferSize);
            
            frameData.IndexBuffer.Get()->Map(0, &range, &tmp).ThrowHr();
            var targetIndices = new Span<ushort>(tmp, (int)frameData.IndexBufferSize);

            foreach (ref var cmdList in new Span<ImDrawListPtr>(
                         drawData.NativePtr->CmdLists,
                         drawData.NativePtr->CmdListsCount))
            {
                var vertices = new Span<ImDrawVert>((void*)cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size);
                var indices = new Span<ushort>((void*)cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size);

                vertices.CopyTo(targetVertices);
                indices.CopyTo(targetIndices);

                targetVertices = targetVertices[vertices.Length..];
                targetIndices = targetIndices[indices.Length..];
            }
        }
        finally
        {
            frameData.VertexBuffer.Get()->Unmap(0, null);
            frameData.IndexBuffer.Get()->Unmap(0, null);
        }

        // Setup desired DX state
        this.SetupRenderState(drawData, ctx, frameData);

        // Setup orthographic projection matrix into our constant buffer.
        // Our visible imgui space lies from DisplayPos (LT) to DisplayPos+DisplaySize (RB).
        // DisplayPos is (0,0) for single viewport apps.
        var projMtx = Matrix4x4.CreateOrthographicOffCenter(
            drawData.DisplayPos.X,
            drawData.DisplayPos.X + drawData.DisplaySize.X,
            drawData.DisplayPos.Y + drawData.DisplaySize.Y,
            drawData.DisplayPos.Y,
            1f,
            0f);

        // Ensure that heap is of sufficient size.
        // We're overshooting it; a texture may be bound to the same heap multiple times.
        frameData.ResetHeap();
        var ensuringHeapSize = 0;
        foreach (ref var cmdList in cmdLists)
            ensuringHeapSize += cmdList.CmdBuffer.Size;
        frameData.EnsureHeapCapacity(this.mainViewportData->Device, ensuringHeapSize);

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        var vertexOffset = 0;
        var indexOffset = 0;
        var clipOff = new Vector4(drawData.DisplayPos, drawData.DisplayPos.X, drawData.DisplayPos.Y);
        foreach (ref var cmdList in cmdLists)
        {
            foreach (ref var pcmd in new Span<ImDrawCmd>((void*)cmdList.CmdBuffer.Data, cmdList.CmdBuffer.Size))
            {
                var clipV4 = pcmd.ClipRect - clipOff;
                var clipRect = new RECT((int)clipV4.X, (int)clipV4.Y, (int)clipV4.Z, (int)clipV4.W);
                ctx->RSSetScissorRects(1, &clipRect);

                if (pcmd.UserCallback == IntPtr.Zero)
                {
                    // Bind texture and draw
                    var ptcd = (TextureData*)pcmd.TextureId;

                    using var pipeline = ptcd->CustomPipeline;
                    if (pipeline.IsEmpty())
                        this.defaultPipeline.CopyTo(&pipeline);
                    pipeline.Get()->BindTo(ctx);

                    ctx->SetGraphicsRoot32BitConstants(0, 16, &projMtx, 0);
                    frameData.BindResourceUsingHeap(this.mainViewportData->Device, ctx, ptcd->Texture);
                    ctx->DrawIndexedInstanced(
                        pcmd.ElemCount,
                        1,
                        (uint)(pcmd.IdxOffset + indexOffset),
                        (int)(pcmd.VtxOffset + vertexOffset),
                        0);
                }
                else if (this.userCallbacks.TryGetValue(pcmd.UserCallback, out var cb))
                {
                    // Use custom callback
                    cb(drawData, (ImDrawCmd*)Unsafe.AsPointer(ref pcmd));
                }
            }

            indexOffset += cmdList.IdxBuffer.Size;
            vertexOffset += cmdList.VtxBuffer.Size;
        }
    }

    /// <summary>
    /// Builds fonts as necessary, and uploads the built data onto the GPU.<br />
    /// No-op if it has already been done.
    /// </summary>
    private void CreateFontsTexture()
    {
        ObjectDisposedException.ThrowIf(this.mainViewportData is null, this);

        if (this.fontTextures.Any())
            return;

        var io = ImGui.GetIO();
        if (io.Fonts.Textures.Size == 0)
            io.Fonts.Build();

        for (int textureIndex = 0, textureCount = io.Fonts.Textures.Size;
             textureIndex < textureCount;
             textureIndex++)
        {
            // Build texture atlas
            io.Fonts.GetTexDataAsRGBA32(
                textureIndex,
                out byte* fontPixels,
                out var width,
                out var height,
                out var bytespp);

            var tex = this.LoadTexture(
                new(fontPixels, width * height * bytespp),
                width * bytespp,
                width,
                height,
                (int)DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM);
            io.Fonts.SetTexID(textureIndex, tex.ImGuiHandle);
            this.fontTextures.Add(tex);
        }

        io.Fonts.ClearTexData();
    }

    /// <summary>
    /// Initializes the device context's render state to what we would use for rendering ImGui by default.
    /// </summary>
    /// <param name="drawData">The relevant ImGui draw data.</param>
    /// <param name="ctx">The command list.</param>
    /// <param name="frameData">The viewport frame data.</param>
    private void SetupRenderState(ImDrawDataPtr drawData, ID3D12GraphicsCommandList* ctx, in ViewportFrame frameData)
    {
        // Setup viewport
        var vp = new D3D12_VIEWPORT
        {
            Width = drawData.DisplaySize.X,
            Height = drawData.DisplaySize.Y,
            MinDepth = 0f,
            MaxDepth = 1f,
            TopLeftX = 0f,
            TopLeftY = 0f,
        };
        ctx->RSSetViewports(1, &vp);

        // Bind shader and vertex buffers
        var vbv = new D3D12_VERTEX_BUFFER_VIEW
        {
            BufferLocation = frameData.VertexBuffer.Get()->GetGPUVirtualAddress(),
            SizeInBytes = frameData.VertexBufferSize * (uint)sizeof(ImDrawVert),
            StrideInBytes = (uint)sizeof(ImDrawVert),
        };
        ctx->IASetVertexBuffers(0, 1, &vbv);

        var ibv = new D3D12_INDEX_BUFFER_VIEW
        {
            BufferLocation = frameData.IndexBuffer.Get()->GetGPUVirtualAddress(),
            SizeInBytes = frameData.IndexBufferSize * sizeof(ushort),
            Format = DXGI_FORMAT.DXGI_FORMAT_R16_UINT,
        };
        ctx->IASetIndexBuffer(&ibv);
        ctx->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

        // Setup blend factor
        var blendFactor = default(Vector4);
        ctx->OMSetBlendFactor((float*)&blendFactor);
    }

    private void EnsureDeviceObjects()
    {
        if (this.defaultPipeline.IsEmpty())
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var streamVs = assembly.GetManifestResourceStream("imgui-vertex.hlsl.bytes")!;
            using var streamPs = assembly.GetManifestResourceStream("imgui-frag.hlsl.bytes")!;
            var vs = ArrayPool<byte>.Shared.Rent((int)streamVs.Length);
            var ps = ArrayPool<byte>.Shared.Rent((int)streamPs.Length);
            streamVs.ReadExactly(vs, 0, (int)streamVs.Length);
            streamPs.ReadExactly(ps, 0, (int)streamPs.Length);
            this.defaultPipeline.Attach(TexturePipeline.CreateNew(
                this.mainViewportData->Device,
                this.mainViewportData->RtvFormat,
                vs.AsSpan(0, (int)streamVs.Length),
                ps.AsSpan(0, (int)streamPs.Length)));
            ArrayPool<byte>.Shared.Return(vs);
            ArrayPool<byte>.Shared.Return(ps);
        }

        this.textureManager ??= new(this.mainViewportData->Device);

        this.CreateFontsTexture();
    }

    private void ReleaseUnmanagedResources()
    {
        if (this.mainViewportData is not null)
        {
            ViewportData.Free(this.mainViewportData);
            this.mainViewportData = null;
        }

        ImGui.GetPlatformIO().Viewports[0].RendererUserData = nint.Zero;
        ImGui.DestroyPlatformWindows();

        this.viewportHandler.Dispose();

        var io = ImGui.GetIO();
        if (io.NativePtr->BackendRendererName == (void*)this.renderNamePtr)
            io.NativePtr->BackendRendererName = null;
        if (this.renderNamePtr != 0)
            Marshal.FreeHGlobal(this.renderNamePtr);

        foreach (var t in this.fontTextures)
            t.Dispose();

        foreach (var i in Enumerable.Range(0, io.Fonts.Textures.Size))
            io.Fonts.SetTexID(i, IntPtr.Zero);
    }
}
