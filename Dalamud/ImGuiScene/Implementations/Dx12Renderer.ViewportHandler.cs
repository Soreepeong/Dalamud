using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Dalamud.ImGuiScene.Helpers;
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
[SuppressMessage(
    "StyleCop.CSharp.LayoutRules",
    "SA1519:Braces should not be omitted from multi-line child statement",
    Justification = "Multiple fixed/using scopes")]
internal unsafe partial class Dx12Renderer
{
    private static readonly Vector4 ClearColor = default;

    /// <summary>
    /// MULTI-VIEWPORT / PLATFORM INTERFACE SUPPORT
    /// This is an _advanced_ and _optional_ feature, allowing the backend to create and handle multiple viewports simultaneously.
    /// If you are new to dear imgui or creating a new binding for dear imgui, it is recommended that you completely ignore this section first..
    /// </summary>
    private readonly struct ViewportHandler : IDisposable
    {
        private readonly Dx12Renderer renderer;

        [SuppressMessage("ReSharper", "CollectionNeverQueried.Local", Justification = "Keeping references alive")]
        private readonly List<object> delegateReferences = new();

        public ViewportHandler(Dx12Renderer renderer)
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
            ObjectDisposedException.ThrowIf(this.renderer.mainViewportData is null, this.renderer);

            // PlatformHandleRaw should always be a HWND, whereas PlatformHandle might be a higher-level handle (e.g. GLFWWindow*, SDL_Window*).
            // Some backend will leave PlatformHandleRaw NULL, in which case we assume PlatformHandle will contain the HWND.
            var hWnd = viewport.PlatformHandleRaw;
            if (hWnd == 0)
                hWnd = viewport.PlatformHandle;
            viewport.RendererUserData = (nint)ViewportData.Create(
                this.renderer.mainViewportData->Device,
                (HWND)hWnd,
                this.renderer.mainViewportData->RtvFormat,
                this.renderer.mainViewportData->NumBackBuffers);
        }

        private void OnDestroyWindow(ImGuiViewportPtr viewport)
        {
            // This is also called on the main viewport for some reason, and we never set that viewport's RendererUserData
            if (viewport.RendererUserData == 0) return;

            ViewportData.Free((ViewportData*)viewport.RendererUserData);
            viewport.RendererUserData = IntPtr.Zero;
        }

        private void OnSetWindowSize(ImGuiViewportPtr viewport, Vector2 size)
        {
            var data = (ViewportData*)viewport.RendererUserData;
            data->ResetBuffers();
            data->ResizeBuffers((int)size.X, (int)size.Y, true);
        }

        private void OnRenderWindow(ImGuiViewportPtr viewport, IntPtr unused)
        {
            if (this.renderer.textureManager is null)
                throw new InvalidOperationException();

            var vd = (ViewportData*)viewport.RendererUserData;
            vd->Draw(this.renderer, viewport.DrawData, (viewport.Flags & ImGuiViewportFlags.NoRendererClear) == 0);
        }

        private void OnSwapBuffers(ImGuiViewportPtr viewport, IntPtr v)
        {
            var data = (ViewportData*)viewport.RendererUserData;
            if (!data->SwapChain.IsEmpty())
                data->SwapChain.Get()->Present(0, 0).ThrowHr();

            data->WaitForPendingOperations();
        }
    }

    /// <summary>
    /// Helper structure we store in the void* RendererUserData field of each ImGuiViewport to easily retrieve our backend data.
    /// Main viewport created by application will only use the Resources field.
    /// Secondary viewports created by this backend will use all the fields (including Window fields.)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ViewportData : IDisposable
    {
        public ComPtr<ID3D12Device> Device;
        public ComPtr<ID3D12CommandQueue> CommandQueue;
        public ComPtr<ID3D12GraphicsCommandList> CommandList;
        public ComPtr<ID3D12DescriptorHeap> RtvDescHeap;
        public ComPtr<IDXGISwapChain3> SwapChain;
        public ComPtr<ID3D12Fence> Fence;
        public ulong FenceSignaledValue;
        public HANDLE FenceEvent;
        public DXGI_FORMAT RtvFormat;
        public int NumBackBuffers;
        public int FrameIndex;

        private int width;
        private int height;

        // we're special
        private ViewportFrame frame0;
        private ViewportFrame frame1;

        public ref ViewportFrame CurrentViewportFrame => ref this.Frames[this.FrameIndex];

        public Span<ViewportFrame> Frames => new(
            Unsafe.AsPointer(ref this.frame0),
            this.NumBackBuffers);

        public static ViewportData* Create(
            IDXGISwapChain3* swapChain3,
            ID3D12Device* device,
            ID3D12CommandQueue* commandQueue)
        {
            DXGI_SWAP_CHAIN_DESC desc;
            swapChain3->GetDesc(&desc).ThrowHr();
            return CreateInternal(
                swapChain3,
                device,
                commandQueue,
                desc.BufferDesc.Format,
                (int)desc.BufferDesc.Width,
                (int)desc.BufferDesc.Height,
                (int)desc.BufferCount);
        }

        public static ViewportData* Create(
            ID3D12Device* device,
            HWND hWnd,
            DXGI_FORMAT rtvFormat,
            int numBackBuffers)
        {
            // Create command queue.
            using var commandQueue = default(ComPtr<ID3D12CommandQueue>);
            fixed (Guid* piid = &IID.IID_ID3D12CommandQueue)
            {
                var queueDesc = new D3D12_COMMAND_QUEUE_DESC
                {
                    Flags = D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE,
                    Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
                };

                device->CreateCommandQueue(
                    &queueDesc,
                    piid,
                    (void**)commandQueue.GetAddressOf()).ThrowHr();
            }

            // Create swap chain.
            // FIXME-VIEWPORT: May want to copy/inherit swap chain settings from the user/application.
            using var swapChain3 = default(ComPtr<IDXGISwapChain3>);
            fixed (Guid* piidFactory2 = &IID.IID_IDXGIFactory2)
            fixed (Guid* piidSwapChain3 = &IID.IID_IDXGISwapChain3)
            {
                var sd1 = new DXGI_SWAP_CHAIN_DESC1
                {
                    Width = 0,
                    Height = 0,
                    Format = rtvFormat,
                    Stereo = false,
                    SampleDesc = new(1, 0),
                    BufferUsage = DXGI.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                    BufferCount = (uint)numBackBuffers,
                    Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH,
                    SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD,
                    AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_UNSPECIFIED,
                    Flags = 0,
                };

                using var dxgiFactory2 = default(ComPtr<IDXGIFactory2>);
#if DEBUG
                DirectX.CreateDXGIFactory2(
                    DXGI.DXGI_CREATE_FACTORY_DEBUG,
                    piidFactory2,
                    (void**)dxgiFactory2.GetAddressOf()).ThrowHr();
#else
                DirectX.CreateDXGIFactory1(piidFactory2, (void**)dxgiFactory2.GetAddressOf()).ThrowHr();
#endif

                using var swapChainTmp = default(ComPtr<IDXGISwapChain1>);
                dxgiFactory2.Get()->CreateSwapChainForHwnd(
                    (IUnknown*)commandQueue.Get(),
                    hWnd,
                    &sd1,
                    null,
                    null,
                    swapChainTmp.GetAddressOf()).ThrowHr();
                swapChainTmp.Get()->QueryInterface(piidSwapChain3, (void**)swapChain3.GetAddressOf()).ThrowHr();
            }

            return Create(swapChain3, device, commandQueue);
        }

        public static ViewportData* Create(
            ID3D12Device* device,
            int width,
            int height,
            DXGI_FORMAT rtvFormat,
            int numBackBuffers)
        {
            // Create command queue.
            using var commandQueue = default(ComPtr<ID3D12CommandQueue>);
            fixed (Guid* piid = &IID.IID_ID3D12CommandQueue)
            {
                var queueDesc = new D3D12_COMMAND_QUEUE_DESC
                {
                    Flags = D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE,
                    Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
                };

                device->CreateCommandQueue(
                    &queueDesc,
                    piid,
                    (void**)commandQueue.GetAddressOf()).ThrowHr();
            }

            return CreateInternal(null, device, commandQueue, rtvFormat, width, height, numBackBuffers);
        }

        public static void Free(ViewportData* instance)
        {
            instance->Dispose();
            Marshal.FreeHGlobal((nint)instance);
        }

        public void Dispose()
        {
            this.WaitForPendingOperations();
            this.Device.Reset();
            this.CommandQueue.Reset();
            this.CommandList.Reset();
            this.RtvDescHeap.Reset();
            this.SwapChain.Reset();
            this.Fence.Reset();
            if (this.FenceEvent != default)
                Win32.CloseHandle(this.FenceEvent);
            this.FenceEvent = default;
            foreach (ref var x in this.Frames)
                x.Dispose();
        }

        public void Draw(Dx12Renderer renderer, ImDrawDataPtr drawData, bool clearRenderTarget)
        {
            if (renderer.textureManager is null)
                throw new InvalidOperationException();

            ref var currentFrame = ref this.CurrentViewportFrame;
            var backBufferIndex =
                this.SwapChain.IsEmpty()
                    ? this.FrameIndex
                    : (int)this.SwapChain.Get()->GetCurrentBackBufferIndex();
            ref var backBufferFrame = ref this.Frames[backBufferIndex];

            var barrier = new D3D12_RESOURCE_BARRIER
            {
                Type = D3D12_RESOURCE_BARRIER_TYPE.D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
                Flags = D3D12_RESOURCE_BARRIER_FLAGS.D3D12_RESOURCE_BARRIER_FLAG_NONE,
                Transition = new()
                {
                    pResource = backBufferFrame.RenderTarget,
                    Subresource = D3D12.D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES,
                    StateBefore = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT,
                    StateAfter = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET,
                },
            };

            // Draw
            var cmdList = this.CommandList.Get();
            var cmdQueue = this.CommandQueue.Get();

            currentFrame.CommandAllocator.Get()->Reset().ThrowHr();
            cmdList->Reset(currentFrame.CommandAllocator.Get(), null).ThrowHr();
            cmdList->ResourceBarrier(1, &barrier);
            cmdList->OMSetRenderTargets(
                1,
                (D3D12_CPU_DESCRIPTOR_HANDLE*)Unsafe.AsPointer(ref backBufferFrame.RenderTargetCpuDescriptor),
                false,
                null);
            if (clearRenderTarget)
            {
                var clearColor = ClearColor;
                cmdList->ClearRenderTargetView(
                    backBufferFrame.RenderTargetCpuDescriptor,
                    (float*)&clearColor,
                    0,
                    null);
            }

            renderer.RenderDrawDataInternal(drawData, cmdList);

            barrier.Transition.StateBefore = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_RENDER_TARGET;
            barrier.Transition.StateAfter = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_PRESENT;
            cmdList->ResourceBarrier(1, &barrier);
            cmdList->Close();

            cmdQueue->Wait(this.Fence, this.FenceSignaledValue);
            cmdQueue->ExecuteCommandLists(1, (ID3D12CommandList**)&cmdList);
            cmdQueue->Signal(this.Fence, Interlocked.Increment(ref this.FenceSignaledValue));
        }

        public void ResetBuffers()
        {
            this.WaitForPendingOperations();
            for (var i = 0; i < this.NumBackBuffers; i++)
                this.Frames[i].RenderTarget.Reset();
        }

        public void ResizeBuffers(int newWidth, int newHeight, bool resizeSwapChain)
        {
            this.width = newWidth;
            this.height = newHeight;
            if (!this.SwapChain.IsEmpty() && resizeSwapChain)
            {
                DXGI_SWAP_CHAIN_DESC1 desc;
                this.SwapChain.Get()->GetDesc1(&desc).ThrowHr();
                this.SwapChain.Get()->ResizeBuffers(
                    desc.BufferCount,
                    (uint)newWidth,
                    (uint)newHeight,
                    DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
                    desc.Flags).ThrowHr();
            }

            this.CreateRenderTargets();
        }

        public void WaitForPendingOperations()
        {
            if (this.CommandQueue.IsEmpty() || this.Fence.IsEmpty())
                return;

            var value = Interlocked.Increment(ref this.FenceSignaledValue);
            this.CommandQueue.Get()->Signal(this.Fence, value).ThrowHr();

            if (this.FenceEvent == default)
            {
                this.FenceEvent = Win32.CreateEventW(null, false, false, null);
                if (this.FenceEvent == default)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new();
            }
            else
            {
                if (!Win32.ResetEvent(this.FenceEvent)) // reset any forgotten waits
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new();
            }

            this.Fence.Get()->SetEventOnCompletion(value, this.FenceEvent).ThrowHr();
            if (Win32.WaitForSingleObject(this.FenceEvent, Win32.INFINITE) != WAIT.WAIT_OBJECT_0)
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new();
        }

        private static ViewportData* CreateInternal(
            IDXGISwapChain3* swapChain3,
            ID3D12Device* device,
            ID3D12CommandQueue* commandQueue,
            DXGI_FORMAT rtvFormat,
            int width,
            int height,
            int numBackBuffers)
        {
            if (numBackBuffers < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numBackBuffers),
                    numBackBuffers,
                    "Must be at least 2.");
            }

            var dataSizeTest = default(ViewportData);
            var baseDataSize = (nint)(&dataSizeTest.frame0) - (nint)(&dataSizeTest);
            var paddedSize = (nint)(&dataSizeTest.frame1) - (nint)(&dataSizeTest.frame0);
            var dataSize = baseDataSize + (numBackBuffers * paddedSize);
            var data = (ViewportData*)Marshal.AllocHGlobal(dataSize);
            new Span<byte>(data, (int)dataSize).Clear();
            try
            {
                // Set up basic information.
                data->width = width;
                data->height = height;
                data->RtvFormat = rtvFormat;
                data->NumBackBuffers = numBackBuffers;
                data->FrameIndex = data->NumBackBuffers - 1;

                // Attach device.
                device->AddRef();
                data->Device.Attach(device);

                // Attach swap chain, if provided.
                if (swapChain3 is not null)
                {
                    swapChain3->AddRef();
                    data->SwapChain.Attach(swapChain3);
                }

                // Attach command queue. 
                commandQueue->AddRef();
                data->CommandQueue.Attach(commandQueue);

                // Create command allocator.
                fixed (Guid* piid = &IID.IID_ID3D12CommandAllocator)
                {
                    for (var i = 0; i < data->NumBackBuffers; i++)
                    {
                        device->CreateCommandAllocator(
                            D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
                            piid,
                            (void**)data->Frames[i].CommandAllocator.GetAddressOf()).ThrowHr();
                    }
                }

                // Create command list.
                fixed (Guid* piid = &IID.IID_ID3D12CommandList)
                {
                    device->CreateCommandList(
                        0,
                        D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_DIRECT,
                        data->Frames[0].CommandAllocator,
                        null,
                        piid,
                        (void**)data->CommandList.GetAddressOf()).ThrowHr();
                    data->CommandList.Get()->Close();
                }

                // Create fence.
                fixed (Guid* piid = &IID.IID_ID3D12Fence)
                {
                    device->CreateFence(
                        0,
                        D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE,
                        piid,
                        (void**)data->Fence.GetAddressOf()).ThrowHr();
                }

                // Create the render targets.
                fixed (Guid* piidDescHeap = &IID.IID_ID3D12DescriptorHeap)
                {
                    var desc = new D3D12_DESCRIPTOR_HEAP_DESC
                    {
                        Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV,
                        NumDescriptors = (uint)numBackBuffers,
                        Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_NONE,
                        NodeMask = 1,
                    };

                    device->CreateDescriptorHeap(
                        &desc,
                        piidDescHeap,
                        (void**)data->RtvDescHeap.GetAddressOf()).ThrowHr();

                    var rtvDescriptorSize = device->GetDescriptorHandleIncrementSize(
                        D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
                    var rtvHandle = data->RtvDescHeap.Get()->GetCPUDescriptorHandleForHeapStart();
                    for (var i = 0; i < numBackBuffers; i++)
                    {
                        data->Frames[i].RenderTargetCpuDescriptor = rtvHandle;
                        rtvHandle.ptr += rtvDescriptorSize;
                    }

                    data->CreateRenderTargets();
                }

                return data;
            }
            catch
            {
                Free(data);
                throw;
            }
        }

        private void CreateRenderTargets()
        {
            // Create the render targets
            fixed (Guid* piidResource = &IID.IID_ID3D12Resource)
            {
                for (var i = 0; i < this.NumBackBuffers; i++)
                {
                    ref var frameData = ref this.Frames[i];
                    using var backBuffer = default(ComPtr<ID3D12Resource>);
                    if (this.SwapChain.IsEmpty())
                    {
                        fixed (Guid* piid = &IID.IID_ID3D12Resource)
                        {
                            var props = new D3D12_HEAP_PROPERTIES
                            {
                                Type = D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_DEFAULT,
                                CPUPageProperty = D3D12_CPU_PAGE_PROPERTY.D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
                                MemoryPoolPreference = D3D12_MEMORY_POOL.D3D12_MEMORY_POOL_UNKNOWN,
                            };
                            var desc = new D3D12_RESOURCE_DESC
                            {
                                Dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_TEXTURE2D,
                                Alignment = 0,
                                Width = (ulong)this.width,
                                Height = (uint)this.height,
                                DepthOrArraySize = 1,
                                MipLevels = 1,
                                Format = this.RtvFormat,
                                SampleDesc = { Count = 1, Quality = 0 },
                                Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_UNKNOWN,
                                Flags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET |
                                        D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS,
                            };
                            var clearColor = ClearColor;
                            var clearValue = new D3D12_CLEAR_VALUE(this.RtvFormat, (float*)&clearColor);
                            this.Device.Get()->CreateCommittedResource(
                                &props,
                                D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_SHARED,
                                &desc,
                                D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
                                &clearValue,
                                piid,
                                (void**)backBuffer.GetAddressOf()).ThrowHr();
                        }
                    }
                    else
                    {
                        this.SwapChain.Get()->GetBuffer(
                            (uint)i,
                            piidResource,
                            (void**)backBuffer.GetAddressOf()).ThrowHr();
                    }

                    this.Device.Get()->CreateRenderTargetView(
                        backBuffer,
                        null,
                        frameData.RenderTargetCpuDescriptor);
                    frameData.RenderTarget.Swap(&backBuffer);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewportFrame : IDisposable
    {
        // Buffers used during the rendering of a frame.
        public ComPtr<ID3D12Resource> IndexBuffer;
        public ComPtr<ID3D12Resource> VertexBuffer;
        public uint IndexBufferSize;
        public uint VertexBufferSize;

        // Buffers used for secondary viewports created by the multi-viewports systems.
        public ComPtr<ID3D12CommandAllocator> CommandAllocator;
        public ComPtr<ID3D12Resource> RenderTarget;
        public D3D12_CPU_DESCRIPTOR_HANDLE RenderTargetCpuDescriptor;

        // Even if we start at 1 item, no way this gets filled up, unless we somehow made 2B textures.
        private const int HeapsPendingForDeletion = 32;
        private const int HeapDefaultCapacity = 1024;

        private ComPtr<ID3D12DescriptorHeap> heap;
        private int heapLength;
        private int heapCapacity;

        private fixed ulong pastHeapIntPtrs[HeapsPendingForDeletion];

        public void Dispose()
        {
            this.ResetHeap();
            this.heap.Dispose();
            this.IndexBuffer.Reset();
            this.VertexBuffer.Reset();
            this.IndexBufferSize = this.VertexBufferSize = 0u;
            this.CommandAllocator.Reset();
            this.RenderTarget.Reset();
            this.RenderTargetCpuDescriptor = default;
        }

        public void EnsureVertexBufferCapacity(ID3D12Device* device, int capacity)
        {
            if (this.VertexBufferSize >= capacity)
                return;

            this.VertexBuffer.Reset();
            this.VertexBufferSize = (uint)(capacity + 5000);
            fixed (Guid* piid = &IID.IID_ID3D12Resource)
            {
                var props = new D3D12_HEAP_PROPERTIES
                {
                    Type = D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD,
                    CPUPageProperty = D3D12_CPU_PAGE_PROPERTY.D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
                    MemoryPoolPreference = D3D12_MEMORY_POOL.D3D12_MEMORY_POOL_UNKNOWN,
                };
                var desc = new D3D12_RESOURCE_DESC
                {
                    Dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_BUFFER,
                    Width = (ulong)(this.VertexBufferSize * sizeof(ImDrawVert)),
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
                    SampleDesc = new(1, 0),
                    Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_ROW_MAJOR,
                    Flags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE,
                };
                device->CreateCommittedResource(
                    &props,
                    D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
                    &desc,
                    D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
                    null,
                    piid,
                    (void**)this.VertexBuffer.GetAddressOf()).ThrowHr();
            }
        }

        public void EnsureIndexBufferCapacity(ID3D12Device* device, int capacity)
        {
            if (this.IndexBufferSize >= capacity)
                return;

            this.IndexBuffer.Reset();
            this.IndexBufferSize = (uint)(capacity + 10000);
            fixed (Guid* piid = &IID.IID_ID3D12Resource)
            {
                var props = new D3D12_HEAP_PROPERTIES
                {
                    Type = D3D12_HEAP_TYPE.D3D12_HEAP_TYPE_UPLOAD,
                    CPUPageProperty = D3D12_CPU_PAGE_PROPERTY.D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
                    MemoryPoolPreference = D3D12_MEMORY_POOL.D3D12_MEMORY_POOL_UNKNOWN,
                };
                var desc = new D3D12_RESOURCE_DESC
                {
                    Dimension = D3D12_RESOURCE_DIMENSION.D3D12_RESOURCE_DIMENSION_BUFFER,
                    Width = this.IndexBufferSize * sizeof(ushort),
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
                    SampleDesc = new(1, 0),
                    Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_ROW_MAJOR,
                    Flags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE,
                };
                device->CreateCommittedResource(
                    &props,
                    D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
                    &desc,
                    D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
                    null,
                    piid,
                    (void**)this.IndexBuffer.GetAddressOf()).ThrowHr();
            }
        }

        public void ResetHeap()
        {
            this.heapLength = 0;
            for (var i = 0; i < HeapsPendingForDeletion && this.pastHeapIntPtrs[i] != 0; i++)
            {
                ((IUnknown*)this.pastHeapIntPtrs[i])->Release();
                this.pastHeapIntPtrs[i] = 0;
            }
        }

        public void EnsureHeapCapacity(ID3D12Device* device, int capacity)
        {
            if (this.heapCapacity >= capacity)
                return;

            if (!this.heap.IsEmpty())
            {
                for (var i = 0; i < HeapsPendingForDeletion; i++)
                {
                    if (this.pastHeapIntPtrs[i] != 0)
                        continue;

                    this.pastHeapIntPtrs[i] = (ulong)(nint)this.heap.Get();
                    this.heap.Detach();
                    this.heapCapacity = 0;
                }

                if (!this.heap.IsEmpty())
                    throw new OutOfMemoryException();
            }

            var newCapacity = this.heapLength == 0 ? HeapDefaultCapacity : this.heapLength * 2;
            var desc = new D3D12_DESCRIPTOR_HEAP_DESC
            {
                Type = D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV,
                NumDescriptors = (uint)newCapacity,
                Flags = D3D12_DESCRIPTOR_HEAP_FLAGS.D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE,
            };
            fixed (Guid* guid = &IID.IID_ID3D12DescriptorHeap)
            fixed (ID3D12DescriptorHeap** ppHeap = &this.heap.GetPinnableReference())
                device->CreateDescriptorHeap(&desc, guid, (void**)ppHeap).ThrowHr();
            this.heapCapacity = newCapacity;
        }

        public void BindResourceUsingHeap(
            ID3D12Device* device,
            ID3D12GraphicsCommandList* cmdList,
            ID3D12Resource* resource)
        {
            var entrySize = device->GetDescriptorHandleIncrementSize(
                D3D12_DESCRIPTOR_HEAP_TYPE.D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
            this.EnsureHeapCapacity(device, this.heapLength + 1);
            
            var h = this.heap.Get();
            var cpuh = h->GetCPUDescriptorHandleForHeapStart();
            var gpuh = h->GetGPUDescriptorHandleForHeapStart();
            cpuh.ptr += (nuint)(entrySize * this.heapLength);
            gpuh.ptr += (nuint)(entrySize * this.heapLength);
            device->CreateShaderResourceView(resource, null, cpuh);
            this.heapLength++;

            cmdList->SetDescriptorHeaps(1, &h);
            cmdList->SetGraphicsRootDescriptorTable(1, gpuh);
        }
    }
}
