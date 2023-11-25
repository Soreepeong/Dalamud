using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.ImGuiScene.Helpers;
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
internal unsafe partial class Dx11Renderer
{
    private readonly struct ViewportHandler : IDisposable
    {
        private readonly Dx11Renderer renderer;

        [SuppressMessage("ReSharper", "CollectionNeverQueried.Local", Justification = "Keeping references alive")]
        private readonly List<object> delegateReferences = new();

        public ViewportHandler(Dx11Renderer renderer)
        {
            this.renderer = renderer;

            var pio = ImGui.GetPlatformIO();
            pio.Renderer_CreateWindow =
                this.RegisterFunctionPointer<ImGuiViewportHelpers.CreateWindowDelegate>(this.OnCreateWindow);
            pio.Renderer_DestroyWindow =
                this.RegisterFunctionPointer<ImGuiViewportHelpers.DestroyWindowDelegate>(this.OnDestroyWindow);
            pio.Renderer_SetWindowSize =
                this.RegisterFunctionPointer<ImGuiViewportHelpers.SetWindowSizeDelegate>(this.OnSetWindowSize);
            pio.Renderer_RenderWindow =
                this.RegisterFunctionPointer<ImGuiViewportHelpers.RenderWindowDelegate>(this.OnRenderWindow);
            pio.Renderer_SwapBuffers =
                this.RegisterFunctionPointer<ImGuiViewportHelpers.SwapBuffersDelegate>(this.OnSwapBuffers);
        }

        public void Dispose()
        {
            var pio = ImGui.GetPlatformIO();
            pio.Renderer_CreateWindow = nint.Zero;
            pio.Renderer_DestroyWindow = nint.Zero;
            pio.Renderer_SetWindowSize = nint.Zero;
            pio.Renderer_RenderWindow = nint.Zero;
            pio.Renderer_SwapBuffers = nint.Zero;
        }

        private nint RegisterFunctionPointer<T>(T obj)
        {
            this.delegateReferences.Add(obj);
            return Marshal.GetFunctionPointerForDelegate(obj);
        }

        private void OnCreateWindow(ImGuiViewportPtr viewport)
        {
            // PlatformHandleRaw should always be a HWND, whereas PlatformHandle might be a higher-level handle (e.g. GLFWWindow*, SDL_Window*).
            // Some backend will leave PlatformHandleRaw NULL, in which case we assume PlatformHandle will contain the HWND.
            var hWnd = viewport.PlatformHandleRaw;
            if (hWnd == 0)
                hWnd = viewport.PlatformHandle;
            viewport.RendererUserData = (nint)InternalData.Create(this.renderer.device, (HWND)hWnd);
        }

        private void OnDestroyWindow(ImGuiViewportPtr viewport)
        {
            // This is also called on the main viewport for some reason, and we never set that viewport's RendererUserData
            if (viewport.RendererUserData == 0) return;

            InternalData.Free((InternalData*)viewport.RendererUserData);
            viewport.RendererUserData = IntPtr.Zero;
        }

        private void OnSetWindowSize(ImGuiViewportPtr viewport, Vector2 size)
        {
            var data = (InternalData*)viewport.RendererUserData;
            data->View.Reset();
            data->SwapChain.Get()->ResizeBuffers(1, (uint)size.X, (uint)size.Y, DXGI_FORMAT.DXGI_FORMAT_UNKNOWN, 0)
                .ThrowHr();
            data->CreateView(this.renderer.device);
        }

        private void OnRenderWindow(ImGuiViewportPtr viewport, IntPtr v)
        {
            var data = (InternalData*)viewport.RendererUserData;
            this.renderer.context.Get()->OMSetRenderTargets(1, data->View.GetAddressOf(), null);
            if ((viewport.Flags & ImGuiViewportFlags.NoRendererClear) != ImGuiViewportFlags.NoRendererClear)
            {
                var color = default(Vector4);
                this.renderer.context.Get()->ClearRenderTargetView(data->View, (float*)&color);
            }

            this.renderer.RenderDrawData(viewport.DrawData);
        }

        private void OnSwapBuffers(ImGuiViewportPtr viewport, IntPtr v)
        {
            var data = (InternalData*)viewport.RendererUserData;
            data->SwapChain.Get()->Present(0, 0).ThrowHr();
        }

        private struct InternalData : IDisposable
        {
            public ComPtr<IDXGISwapChain> SwapChain;
            public ComPtr<ID3D11RenderTargetView> View;

            public static InternalData* Create(ComPtr<ID3D11Device> device, HWND hWnd)
            {
                using var dxgiDevice = default(ComPtr<IDXGIDevice>);
                using var dxgiAdapter = default(ComPtr<IDXGIAdapter>);
                using var dxgiFactory = default(ComPtr<IDXGIFactory>);
                device.CopyTo(&dxgiDevice).ThrowHr();
                dxgiDevice.Get()->GetAdapter(dxgiAdapter.GetAddressOf()).ThrowHr();
                fixed (Guid* piid = &IID.IID_IDXGIFactory)
                    dxgiAdapter.Get()->GetParent(piid, (void**)dxgiFactory.GetAddressOf()).ThrowHr();

                var data = (InternalData*)Marshal.AllocHGlobal(Marshal.SizeOf<InternalData>());
                *data = default;
                try
                {
                    // Create swapchain
                    var desc = new DXGI_SWAP_CHAIN_DESC
                    {
                        BufferDesc =
                        {
                            Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
                        },
                        SampleDesc = new(1, 0),
                        BufferUsage = DXGI.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                        BufferCount = 1,
                        OutputWindow = hWnd,
                        Windowed = true,
                        SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_DISCARD,
                    };
                    dxgiFactory.Get()->CreateSwapChain(
                            (IUnknown*)dxgiDevice.Get(),
                            &desc,
                            data->SwapChain.GetAddressOf())
                        .ThrowHr();

                    // Create the render target view
                    data->CreateView(device);
                    return data;
                }
                catch
                {
                    data->Dispose();
                    Marshal.FreeHGlobal((nint)data);
                    throw;
                }
            }

            public static void Free(InternalData* instance)
            {
                instance->Dispose();
                Marshal.FreeHGlobal((nint)instance);
            }

            public void CreateView(ComPtr<ID3D11Device> device)
            {
                using var buffer = default(ComPtr<ID3D11Texture2D>);
                var psw = this.SwapChain;
                fixed (Guid* piid = &IID.IID_ID3D11Texture2D)
                    psw.Get()->GetBuffer(0, piid, (void**)buffer.GetAddressOf()).ThrowHr();
                device.Get()->CreateRenderTargetView(
                    (ID3D11Resource*)buffer.Get(),
                    null,
                    this.View.GetAddressOf()).ThrowHr();
            }

            public void Dispose()
            {
                this.SwapChain.Reset();
                this.View.Reset();
            }
        }
    }
}
