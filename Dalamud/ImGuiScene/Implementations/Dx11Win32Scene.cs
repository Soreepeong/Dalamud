using System.Diagnostics.CodeAnalysis;

using Dalamud.ImGuiScene.Helpers;
using Dalamud.Interface.Internal;
using Dalamud.Utility;

using ImGuiNET;

using ImGuizmoNET;

using ImPlotNET;

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Dalamud.ImGuiScene.Implementations;

/// <summary>
/// Backend for ImGui, using <see cref="Dx11Renderer"/> and <see cref="Win32InputHandler"/>.
/// </summary>
[SuppressMessage(
    "StyleCop.CSharp.LayoutRules",
    "SA1519:Braces should not be omitted from multi-line child statement",
    Justification = "Multiple fixed/using scopes")]
internal sealed unsafe class Dx11Win32Scene : IWin32Scene
{
    private readonly Dx11Renderer imguiRenderer;
    private readonly Win32InputHandler imguiInput;
    private readonly WicEasy wicEasy;

    private ComPtr<IDXGISwapChain> swapChain;
    private ComPtr<ID3D11Device> device;
    private ComPtr<ID3D11DeviceContext> deviceContext;
    private ComPtr<ID3D11RenderTargetView> rtv;

    private int targetWidth;
    private int targetHeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dx11Win32Scene"/> class.
    /// </summary>
    /// <param name="swapChain">The pointer to an instance of <see cref="IDXGISwapChain"/>. The reference is copied.</param>
    public Dx11Win32Scene(IDXGISwapChain* swapChain)
    {
        this.wicEasy = new();
        try
        {
            swapChain->AddRef();
            this.swapChain.Attach(swapChain);

            fixed (Guid* guid = &IID.IID_ID3D11Device)
            fixed (ID3D11Device** pp = &this.device.GetPinnableReference())
                this.swapChain.Get()->GetDevice(guid, (void**)pp).ThrowHr();

            fixed (ID3D11DeviceContext** pp = &this.deviceContext.GetPinnableReference())
                this.device.Get()->GetImmediateContext(pp);

            using var buffer = default(ComPtr<ID3D11Resource>);
            fixed (Guid* guid = &IID.IID_ID3D11Resource)
                this.swapChain.Get()->GetBuffer(0, guid, (void**)buffer.GetAddressOf()).ThrowHr();

            fixed (ID3D11RenderTargetView** pp = &this.rtv.GetPinnableReference())
                this.device.Get()->CreateRenderTargetView(buffer.Get(), null, pp).ThrowHr();

            var desc = default(DXGI_SWAP_CHAIN_DESC);
            this.swapChain.Get()->GetDesc(&desc).ThrowHr();
            this.targetWidth = (int)desc.BufferDesc.Width;
            this.targetHeight = (int)desc.BufferDesc.Height;
            this.WindowHandle = desc.OutputWindow;

            var ctx = ImGui.CreateContext();
            ImGuizmo.SetImGuiContext(ctx);
            ImPlot.SetImGuiContext(ctx);
            ImPlot.CreateContext();

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.ViewportsEnable;

            this.imguiRenderer = new(this.Device, this.DeviceContext);
            this.imguiInput = new(this.WindowHandle);
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Dx11Win32Scene"/> class.
    /// </summary>
    ~Dx11Win32Scene() => this.ReleaseUnmanagedResources();

    /// <inheritdoc/>
    public event IImGuiScene.BuildUiDelegate? BuildUi;

    /// <inheritdoc/>
    public event IImGuiScene.NewInputFrameDelegate? NewInputFrame;

    /// <inheritdoc/>
    public event IImGuiScene.NewRenderFrameDelegate? NewRenderFrame;

    /// <inheritdoc/>
    public bool UpdateCursor
    {
        get => this.imguiInput.UpdateCursor;
        set => this.imguiInput.UpdateCursor = value;
    }

    /// <inheritdoc/>
    public string? IniPath
    {
        get => this.imguiInput.IniPath;
        set => this.imguiInput.IniPath = value;
    }

    /// <summary>
    /// Gets the pointer to an instance of <see cref="IDXGISwapChain"/>.
    /// </summary>
    public IDXGISwapChain* SwapChain => this.swapChain;

    /// <summary>
    /// Gets the pointer to an instance of <see cref="ID3D11Device"/>.
    /// </summary>
    public ID3D11Device* Device => this.device;

    /// <summary>
    /// Gets the pointer to an instance of <see cref="ID3D11Device"/>, in <see cref="nint"/>.
    /// </summary>
    public nint DeviceHandle => (nint)this.device.Get();

    /// <summary>
    /// Gets the pointer to an instance of <see cref="ID3D11DeviceContext"/>.
    /// </summary>
    public ID3D11DeviceContext* DeviceContext => this.deviceContext;

    /// <summary>
    /// Gets the window handle.
    /// </summary>
    public HWND WindowHandle { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.ReleaseUnmanagedResources();
        this.wicEasy.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public IntPtr? ProcessWndProcW(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        this.imguiInput.ProcessWndProcW(hWnd, msg, wParam, lParam);

    /// <inheritdoc/>
    public void Render()
    {
        this.imguiRenderer.OnNewFrame();
        this.NewRenderFrame?.Invoke();
        this.imguiInput.NewFrame(this.targetWidth, this.targetHeight);
        this.NewInputFrame?.Invoke();

        ImGui.NewFrame();
        ImGuizmo.BeginFrame();

        this.BuildUi?.Invoke();

        ImGui.Render();

        var prtv = this.rtv.Get();
        this.DeviceContext->OMSetRenderTargets(1, &prtv, null);
        this.imguiRenderer.RenderDrawData(ImGui.GetDrawData());
        this.DeviceContext->OMSetRenderTargets(0, null, null);

        ImGui.UpdatePlatformWindows();
        ImGui.RenderPlatformWindowsDefault();
    }

    /// <inheritdoc/>
    public void OnPreResize()
    {
        this.rtv.Reset();
    }

    /// <inheritdoc/>
    public void OnPostResize(int newWidth, int newHeight)
    {
        using var buffer = default(ComPtr<ID3D11Resource>);
        fixed (Guid* guid = &IID.IID_ID3D11Resource)
            this.SwapChain->GetBuffer(0, guid, (void**)buffer.ReleaseAndGetAddressOf()).ThrowHr();

        using var rtvTemp = default(ComPtr<ID3D11RenderTargetView>);
        this.Device->CreateRenderTargetView(buffer.Get(), null, rtvTemp.GetAddressOf()).ThrowHr();
        this.rtv.Swap(&rtvTemp);

        this.targetWidth = newWidth;
        this.targetHeight = newHeight;
    }

    /// <inheritdoc/>
    public void InvalidateFonts() => this.imguiRenderer.RebuildFontTexture();

    /// <inheritdoc/>
    public bool SupportsTextureFormat(int format) =>
        this.SupportsTextureFormat((DXGI_FORMAT)format);

    /// <inheritdoc/>
    public IDalamudTextureWrap LoadImage(string path)
    {
        using var stream = WicEasyExtensions.CreateStreamFromFile(path);
        return this.LoadImage(stream);
    }

    /// <inheritdoc/>
    public IDalamudTextureWrap LoadImage(ReadOnlySpan<byte> data)
    {
        using var stream = this.wicEasy.CreateStream();
        fixed (byte* pData = data)
        {
            stream.Get()->InitializeFromMemory(pData, (uint)data.Length).ThrowHr();
            return this.LoadImage((IStream*)stream.Get());
        }
    }

    /// <inheritdoc/>
    public IDalamudTextureWrap LoadImageRaw(ReadOnlySpan<byte> data, int width, int height, int numChannels = 4) =>
        this.imguiRenderer.LoadTexture(
            data,
            width * numChannels,
            width,
            height,
            (int)(numChannels == 4 ? DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM : DXGI_FORMAT.DXGI_FORMAT_R8_UNORM));

    /// <inheritdoc/>
    public IDalamudTextureWrap LoadImageFormat(ReadOnlySpan<byte> data, int pitch, int width, int height, int format) =>
        this.imguiRenderer.LoadTexture(data, pitch, width, height, format);

    /// <inheritdoc cref="Dx11Renderer.CreateTexturePipeline"/>
    public nint CreateTexturePipeline(ReadOnlySpan<byte> ps, in D3D11_SAMPLER_DESC samplerDesc) =>
        this.imguiRenderer.CreateTexturePipeline(ps, samplerDesc);

    /// <inheritdoc/>
    public void SetTexturePipeline(IntPtr textureHandle, IntPtr pipelineHandle) =>
        this.imguiRenderer.SetTexturePipeline(textureHandle, pipelineHandle);

    /// <inheritdoc/>
    public IntPtr GetTexturePipeline(IntPtr textureHandle) =>
        this.imguiRenderer.GetTexturePipeline(textureHandle);

    /// <inheritdoc/>
    public void ReleaseTexturePipeline(IntPtr pipelineHandle) =>
        this.imguiRenderer.ReleaseTexturePipeline(pipelineHandle);

    /// <inheritdoc/>
    public bool IsImGuiCursor(nint cursorHandle) => this.imguiInput.IsImGuiCursor(cursorHandle);

    /// <inheritdoc/>
    public bool IsAttachedToPresentationTarget(IntPtr targetHandle) => this.swapChain.Get() == (void*)targetHandle;

    /// <inheritdoc/>
    public bool IsMainViewportFullScreen()
    {
        BOOL fullscreen;
        this.swapChain.Get()->GetFullscreenState(&fullscreen, null);
        return fullscreen;
    }

    /// <summary>
    /// Determines whether the current D3D11 Device supports the given DXGI format.
    /// </summary>
    /// <param name="dxgiFormat">DXGI format to check.</param>
    /// <param name="formatSupport">Formats to test. All formats must be supported, if multiple are specified.</param>
    /// <returns>Whether it is supported.</returns>
    public bool SupportsTextureFormat(
        DXGI_FORMAT dxgiFormat,
        D3D11_FORMAT_SUPPORT formatSupport = D3D11_FORMAT_SUPPORT.D3D11_FORMAT_SUPPORT_TEXTURE2D)
    {
        var flags = 0u;
        if (this.Device->CheckFormatSupport(dxgiFormat, &flags).FAILED)
            return false;
        return (flags & (uint)formatSupport) == (uint)formatSupport;
    }

    /// <summary>
    /// Loads an image from an <see cref="IStream"/>. The ownership is not transferred.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The loaded image.</returns>
    public IDalamudTextureWrap LoadImage(IStream* stream)
    {
        using var source = this.wicEasy.CreateBitmapSource(stream);

        var dxgiFormat = source.Get()->GetPixelFormat().ToDxgiFormat();
        if (dxgiFormat == DXGI_FORMAT.DXGI_FORMAT_UNKNOWN || !this.SupportsTextureFormat(dxgiFormat))
        {
            using var converted = this.wicEasy.ConvertPixelFormat(source, GUID.GUID_WICPixelFormat32bppBGRA);
            converted.Swap(&source);
            dxgiFormat = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
        }

        using var bitmap = default(ComPtr<IWICBitmap>);
        if (source.CopyTo(&bitmap).FAILED)
            this.wicEasy.CreateBitmap(source).Swap(&bitmap);
        
        using var l = bitmap.Get()->LockBits(WICBitmapLockFlags.WICBitmapLockRead, out var pb, out _, out _);
        uint stride, width, height;
        l.Get()->GetStride(&stride).ThrowHr();
        l.Get()->GetSize(&width, &height).ThrowHr();
        return this.LoadImageRaw(pb, (int)stride, (int)width, (int)height, dxgiFormat);
    }

    /// <summary>
    /// Load an image from a span of bytes of specified format.
    /// </summary>
    /// <param name="data">The data to load.</param>
    /// <param name="pitch">The pitch(stride) in bytes.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="dxgiFormat">Format of the texture.</param>
    /// <returns>A texture, ready to use in ImGui.</returns>
    public IDalamudTextureWrap LoadImageRaw(void* data, int pitch, int width, int height, DXGI_FORMAT dxgiFormat) =>
        this.imguiRenderer.LoadTexture(new(data, pitch * height), pitch, width, height, (int)dxgiFormat);

    private void ReleaseUnmanagedResources()
    {
        if (this.device.IsEmpty())
            return;

        this.imguiRenderer.Dispose();
        this.imguiInput.Dispose();

        ImGui.DestroyContext();

        this.rtv.Dispose();
        this.swapChain.Dispose();
        this.deviceContext.Dispose();
        this.device.Dispose();
    }
}
