using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dalamud.ImGuiScene.Helpers;
using Dalamud.Interface.Internal;
using Dalamud.Utility;

using ImGuiNET;

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Dalamud.ImGuiScene.Implementations;

/// <summary>
/// Deals with rendering ImGui using DirectX 11.
/// See https://github.com/ocornut/imgui/blob/master/examples/imgui_impl_dx11.cpp for the original implementation.
/// </summary>
[SuppressMessage(
    "StyleCop.CSharp.LayoutRules",
    "SA1519:Braces should not be omitted from multi-line child statement",
    Justification = "Multiple fixed/using scopes")]
internal unsafe partial class Dx11Renderer : IImGuiRenderer
{
    private readonly Dictionary<nint, IImGuiRenderer.DrawCmdUserCallbackDelegate> userCallbacks = new();
    private readonly List<IDalamudTextureWrap> fontTextures = new();
    private readonly D3D_FEATURE_LEVEL featureLevel;
    private readonly ViewportHandler viewportHandler;
    private readonly nint renderNamePtr;

    private ComPtr<ID3D11Device> device;
    private ComPtr<ID3D11DeviceContext> context;
    private ComPtr<ID3D11VertexShader> vertexShader;
    private ComPtr<TexturePipeline> defaultTexturePipeline;
    private ComPtr<ID3D11InputLayout> inputLayout;
    private ComPtr<ID3D11Buffer> vertexConstantBuffer;
    private ComPtr<ID3D11BlendState> blendState;
    private ComPtr<ID3D11RasterizerState> rasterizerState;
    private ComPtr<ID3D11DepthStencilState> depthStencilState;
    private ComPtr<ID3D11Buffer> vertexBuffer;
    private ComPtr<ID3D11Buffer> indexBuffer;
    private int vertexBufferSize;
    private int indexBufferSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dx11Renderer"/> class.
    /// </summary>
    /// <param name="device">A pointer to an instance of <see cref="ID3D11Device"/>.</param>
    /// <param name="context">A pointer to an instance of <see cref="ID3D11DeviceContext"/>.</param>
    public Dx11Renderer(ID3D11Device* device, ID3D11DeviceContext* context)
    {
        var io = ImGui.GetIO();
        if (ImGui.GetIO().NativePtr->BackendRendererName is not null)
            throw new InvalidOperationException("ImGui backend renderer seems to be have been already attached.");
        try
        {
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasViewports;

            this.renderNamePtr = Marshal.StringToHGlobalAnsi("imgui_impl_dx11_c#");
            io.NativePtr->BackendRendererName = (byte*)this.renderNamePtr;

            device->AddRef();
            this.device.Attach(device);
            context->AddRef();
            this.context.Attach(context);
            this.featureLevel = device->GetFeatureLevel();

            if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                this.viewportHandler = new(this);
        }
        catch
        {
            this.ReleaseUnmanagedResources();
            throw;
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Dx11Renderer"/> class.
    /// </summary>
    ~Dx11Renderer() => this.ReleaseUnmanagedResources();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.ReleaseUnmanagedResources();
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
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void OnPostResize(int width, int height)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void RenderDrawData(ImDrawDataPtr drawData)
    {
        // Avoid rendering when minimized
        if (drawData.DisplaySize.X <= 0 || drawData.DisplaySize.Y <= 0)
            return;

        if (!drawData.Valid || drawData.CmdListsCount == 0)
            return;

        // Create and grow vertex/index buffers if needed
        if (this.vertexBufferSize < drawData.TotalVtxCount)
            this.vertexBuffer.Dispose();
        if (this.vertexBuffer.Get() is null)
        {
            this.vertexBufferSize = drawData.TotalVtxCount + 5000;
            var desc = new D3D11_BUFFER_DESC(
                (uint)(sizeof(ImDrawVert) * this.vertexBufferSize),
                (uint)D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
                D3D11_USAGE.D3D11_USAGE_DYNAMIC,
                (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE);
            var buffer = default(ID3D11Buffer*);
            this.device.Get()->CreateBuffer(&desc, null, &buffer).ThrowHr();
            this.vertexBuffer.Attach(buffer);
        }

        if (this.indexBufferSize < drawData.TotalIdxCount)
            this.indexBuffer.Dispose();
        if (this.indexBuffer.Get() is null)
        {
            this.indexBufferSize = drawData.TotalIdxCount + 5000;
            var desc = new D3D11_BUFFER_DESC(
                (uint)(sizeof(ushort) * this.indexBufferSize),
                (uint)D3D11_BIND_FLAG.D3D11_BIND_INDEX_BUFFER,
                D3D11_USAGE.D3D11_USAGE_DYNAMIC,
                (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE);
            var buffer = default(ID3D11Buffer*);
            this.device.Get()->CreateBuffer(&desc, null, &buffer).ThrowHr();
            this.indexBuffer.Attach(buffer);
        }

        // Upload vertex/index data into a single contiguous GPU buffer
        try
        {
            var vertexData = default(D3D11_MAPPED_SUBRESOURCE);
            var indexData = default(D3D11_MAPPED_SUBRESOURCE);
            this.context.Get()->Map(
                (ID3D11Resource*)this.vertexBuffer.Get(),
                0,
                D3D11_MAP.D3D11_MAP_WRITE_DISCARD,
                0,
                &vertexData).ThrowHr();
            this.context.Get()->Map(
                (ID3D11Resource*)this.indexBuffer.Get(),
                0,
                D3D11_MAP.D3D11_MAP_WRITE_DISCARD,
                0,
                &indexData).ThrowHr();

            var targetVertices = new Span<ImDrawVert>(vertexData.pData, this.vertexBufferSize);
            var targetIndices = new Span<ushort>(indexData.pData, this.indexBufferSize);
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
            this.context.Get()->Unmap((ID3D11Resource*)this.vertexBuffer.Get(), 0);
            this.context.Get()->Unmap((ID3D11Resource*)this.indexBuffer.Get(), 0);
        }

        // Setup orthographic projection matrix into our constant buffer.
        // Our visible imgui space lies from DisplayPos (LT) to DisplayPos+DisplaySize (RB).
        // DisplayPos is (0,0) for single viewport apps.
        try
        {
            var data = default(D3D11_MAPPED_SUBRESOURCE);
            this.context.Get()->Map(
                (ID3D11Resource*)this.vertexConstantBuffer.Get(),
                0,
                D3D11_MAP.D3D11_MAP_WRITE_DISCARD,
                0,
                &data).ThrowHr();
            *(Matrix4x4*)data.pData = Matrix4x4.CreateOrthographicOffCenter(
                drawData.DisplayPos.X,
                drawData.DisplayPos.X + drawData.DisplaySize.X,
                drawData.DisplayPos.Y + drawData.DisplaySize.Y,
                drawData.DisplayPos.Y,
                1f,
                0f);
        }
        finally
        {
            this.context.Get()->Unmap((ID3D11Resource*)this.vertexConstantBuffer.Get(), 0);
        }

        using var oldState = new D3D11DeviceContextStateBackup(this.featureLevel, this.context.Get());

        // Setup desired DX state
        this.SetupRenderState(drawData);

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        var vertexOffset = 0;
        var indexOffset = 0;
        var clipOff = new Vector4(drawData.DisplayPos, drawData.DisplayPos.X, drawData.DisplayPos.Y);
        foreach (ref var cmdList in new Span<ImDrawListPtr>(
                     drawData.NativePtr->CmdLists,
                     drawData.NativePtr->CmdListsCount))
        {
            foreach (ref var pcmd in new Span<ImDrawCmd>((void*)cmdList.CmdBuffer.Data, cmdList.CmdBuffer.Size))
            {
                var clipV4 = pcmd.ClipRect - clipOff;
                var clipRect = new RECT((int)clipV4.X, (int)clipV4.Y, (int)clipV4.Z, (int)clipV4.W);
                this.context.Get()->RSSetScissorRects(1, &clipRect);

                if (pcmd.UserCallback == IntPtr.Zero)
                {
                    // Bind texture and draw
                    var ptcd = (TextureData*)pcmd.TextureId;

                    using var pipeline = ptcd->CustomPipeline;
                    if (pipeline.IsEmpty())
                        this.defaultTexturePipeline.CopyTo(&pipeline);

                    pipeline.Get()->BindTo(this.context);

                    var srv = ptcd->ShaderResourceView;
                    this.context.Get()->PSSetShaderResources(0, 1, &srv);
                    this.context.Get()->DrawIndexed(
                        pcmd.ElemCount,
                        (uint)(pcmd.IdxOffset + indexOffset),
                        (int)(pcmd.VtxOffset + vertexOffset));
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
    /// Creates a new texture pipeline. Dispose using <see cref="ReleaseTexturePipeline"/>.
    /// </summary>
    /// <param name="ps">The pixel shader data.</param>
    /// <param name="samplerDesc">The sampler description.</param>
    /// <returns>The handle to the new texture pipeline.</returns>
    public nint CreateTexturePipeline(ReadOnlySpan<byte> ps, in D3D11_SAMPLER_DESC samplerDesc) =>
        (nint)TexturePipeline.CreateNew(this.device, ps, samplerDesc);

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
    public void ReleaseTexturePipeline(IntPtr pipelineHandle)
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
    public IDalamudTextureWrap LoadTexture(ReadOnlySpan<byte> data, int pitch, int width, int height, int format)
    {
        var texd = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = (DXGI_FORMAT)format,
            SampleDesc = new(1, 0),
            Usage = D3D11_USAGE.D3D11_USAGE_IMMUTABLE,
            BindFlags = (uint)D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
            CPUAccessFlags = 0,
            MiscFlags = 0,
        };
        using var texture = default(ComPtr<ID3D11Texture2D>);
        fixed (void* dataPtr = data)
        {
            var subrdata = new D3D11_SUBRESOURCE_DATA { pSysMem = dataPtr, SysMemPitch = (uint)pitch };
            Marshal.ThrowExceptionForHR(this.device.Get()->CreateTexture2D(&texd, &subrdata, texture.GetAddressOf()));
        }

        var viewd = new D3D11_SHADER_RESOURCE_VIEW_DESC
        {
            Format = texd.Format,
            ViewDimension = D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2D,
            Texture2D = new() { MipLevels = texd.MipLevels },
        };
        using var view = default(ComPtr<ID3D11ShaderResourceView>);
        Marshal.ThrowExceptionForHR(
            this.device.Get()->CreateShaderResourceView((ID3D11Resource*)texture.Get(), &viewd, view.GetAddressOf()));

        using var texData = default(ComPtr<TextureData>);
        texData.Attach(TextureData.CreateNew(view, width, height));
        return new TextureWrap(texData);
    }

    /// <summary>
    /// Builds fonts as necessary, and uploads the built data onto the GPU.<br />
    /// No-op if it has already been done.
    /// </summary>
    private void CreateFontsTexture()
    {
        if (this.device.IsEmpty())
            throw new ObjectDisposedException(nameof(Dx11Renderer));

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
    private void SetupRenderState(ImDrawDataPtr drawData)
    {
        var ctx = this.context.Get();
        ctx->IASetInputLayout(this.inputLayout);
        var buffer = this.vertexBuffer.Get();
        var stride = (uint)sizeof(ImDrawVert);
        var offset = 0u;
        ctx->IASetVertexBuffers(0, 1, &buffer, &stride, &offset);
        ctx->IASetIndexBuffer(this.indexBuffer, DXGI_FORMAT.DXGI_FORMAT_R16_UINT, 0);
        ctx->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);

        var viewport = new D3D11_VIEWPORT(0, 0, drawData.DisplaySize.X, drawData.DisplaySize.Y);
        ctx->RSSetState(this.rasterizerState);
        ctx->RSSetViewports(1, &viewport);

        var blendColor = default(Vector4);
        ctx->OMSetBlendState(this.blendState, (float*)&blendColor, 0xffffffff);
        ctx->OMSetDepthStencilState(this.depthStencilState, 0);

        ctx->VSSetShader(this.vertexShader.Get(), null, 0);
        buffer = this.vertexConstantBuffer.Get();
        ctx->VSSetConstantBuffers(0, 1, &buffer);

        // PS handled later

        ctx->GSSetShader(null, null, 0);
        ctx->HSSetShader(null, null, 0);
        ctx->DSSetShader(null, null, 0);
        ctx->CSSetShader(null, null, 0);
    }

    /// <summary>
    /// Creates objects from the device as necessary.<br />
    /// No-op if objects already are built.
    /// </summary>
    private void EnsureDeviceObjects()
    {
        if (this.device.IsEmpty())
            throw new ObjectDisposedException(nameof(Dx11Renderer));

        var assembly = Assembly.GetExecutingAssembly();

        // Create the vertex shader
        if (this.vertexShader.IsEmpty() || this.inputLayout.IsEmpty())
        {
            using var stream = assembly.GetManifestResourceStream("imgui-vertex.hlsl.bytes")!;
            var array = ArrayPool<byte>.Shared.Rent((int)stream.Length);
            stream.ReadExactly(array, 0, (int)stream.Length);
            fixed (byte* pArray = array)
            fixed (ID3D11VertexShader** ppShader = &this.vertexShader.GetPinnableReference())
            fixed (ID3D11InputLayout** ppInputLayout = &this.inputLayout.GetPinnableReference())
            fixed (void* pszPosition = "POSITION"u8)
            fixed (void* pszTexCoord = "TEXCOORD"u8)
            fixed (void* pszColor = "COLOR"u8)
            {
                this.device.Get()->CreateVertexShader(pArray, (nuint)stream.Length, null, ppShader).ThrowHr();

                var ied = stackalloc D3D11_INPUT_ELEMENT_DESC[]
                {
                    new()
                    {
                        SemanticName = (sbyte*)pszPosition,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        AlignedByteOffset = uint.MaxValue,
                    },
                    new()
                    {
                        SemanticName = (sbyte*)pszTexCoord,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        AlignedByteOffset = uint.MaxValue,
                    },
                    new()
                    {
                        SemanticName = (sbyte*)pszColor,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
                        AlignedByteOffset = uint.MaxValue,
                    },
                };
                this.device.Get()->CreateInputLayout(ied, 3, pArray, (nuint)stream.Length, ppInputLayout).ThrowHr();
            }

            ArrayPool<byte>.Shared.Return(array);
        }

        // Create the constant buffer
        if (this.vertexConstantBuffer.IsEmpty())
        {
            var bufferDesc = new D3D11_BUFFER_DESC(
                (uint)sizeof(Matrix4x4),
                (uint)D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
                D3D11_USAGE.D3D11_USAGE_DYNAMIC,
                (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE);
            fixed (ID3D11Buffer** ppBuffer = &this.vertexConstantBuffer.GetPinnableReference())
                this.device.Get()->CreateBuffer(&bufferDesc, null, ppBuffer).ThrowHr();
        }

        // Create the default texture pipeline
        if (this.defaultTexturePipeline.IsEmpty())
        {
            using var stream = assembly.GetManifestResourceStream("imgui-frag.hlsl.bytes")!;
            var array = ArrayPool<byte>.Shared.Rent((int)stream.Length);
            var psspan = array.AsSpan(0, (int)stream.Length);
            stream.ReadExactly(psspan);
            this.defaultTexturePipeline.Attach(TexturePipeline.CreateNew(this.device, psspan));
            ArrayPool<byte>.Shared.Return(array);
        }

        // Create the blending setup
        if (this.blendState.IsEmpty())
        {
            var blendStateDesc = new D3D11_BLEND_DESC
            {
                RenderTarget =
                {
                    e0 =
                    {
                        BlendEnable = true,
                        SrcBlend = D3D11_BLEND.D3D11_BLEND_SRC_ALPHA,
                        DestBlend = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
                        BlendOp = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
                        SrcBlendAlpha = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA,
                        DestBlendAlpha = D3D11_BLEND.D3D11_BLEND_ZERO,
                        BlendOpAlpha = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD,
                        RenderTargetWriteMask = (byte)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL,
                    },
                },
            };
            fixed (ID3D11BlendState** ppBlendState = &this.blendState.GetPinnableReference())
                this.device.Get()->CreateBlendState(&blendStateDesc, ppBlendState).ThrowHr();
        }

        // Create the rasterizer state
        if (this.rasterizerState.IsEmpty())
        {
            var rasterizerDesc = new D3D11_RASTERIZER_DESC
            {
                FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
                CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
                ScissorEnable = true,
                DepthClipEnable = true,
            };
            fixed (ID3D11RasterizerState** ppRasterizerState = &this.rasterizerState.GetPinnableReference())
                this.device.Get()->CreateRasterizerState(&rasterizerDesc, ppRasterizerState).ThrowHr();
        }

        // Create the depth-stencil State
        if (this.depthStencilState.IsEmpty())
        {
            var dsDesc = new D3D11_DEPTH_STENCIL_DESC
            {
                DepthEnable = false,
                DepthWriteMask = D3D11_DEPTH_WRITE_MASK.D3D11_DEPTH_WRITE_MASK_ALL,
                DepthFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS,
                StencilEnable = false,
                StencilReadMask = byte.MaxValue,
                StencilWriteMask = byte.MaxValue,
                FrontFace =
                {
                    StencilFailOp = D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
                    StencilDepthFailOp = D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
                    StencilPassOp = D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
                    StencilFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS,
                },
                BackFace =
                {
                    StencilFailOp = D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
                    StencilDepthFailOp = D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
                    StencilPassOp = D3D11_STENCIL_OP.D3D11_STENCIL_OP_KEEP,
                    StencilFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS,
                },
            };
            fixed (ID3D11DepthStencilState** ppDepthStencilState = &this.depthStencilState.GetPinnableReference())
                this.device.Get()->CreateDepthStencilState(&dsDesc, ppDepthStencilState).ThrowHr();
        }

        this.CreateFontsTexture();
    }

    private void ReleaseUnmanagedResources()
    {
        if (this.device.IsEmpty())
            return;

        ImGui.DestroyPlatformWindows();

        this.viewportHandler.Dispose();

        var io = ImGui.GetIO();
        if (io.NativePtr->BackendRendererName == (void*)this.renderNamePtr)
            io.NativePtr->BackendRendererName = null;
        if (this.renderNamePtr != 0)
            Marshal.FreeHGlobal(this.renderNamePtr);

        foreach (var fontResourceView in this.fontTextures)
            fontResourceView.Dispose();

        foreach (var i in Enumerable.Range(0, io.Fonts.Textures.Size))
            io.Fonts.SetTexID(i, IntPtr.Zero);

        this.device.Reset();
        this.context.Reset();
        this.defaultTexturePipeline.Reset();
        this.vertexShader.Reset();
        this.inputLayout.Reset();
        this.vertexConstantBuffer.Reset();
        this.blendState.Reset();
        this.rasterizerState.Reset();
        this.depthStencilState.Reset();
        this.vertexBuffer.Reset();
        this.indexBuffer.Reset();
    }
}
