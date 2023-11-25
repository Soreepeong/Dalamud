using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Dalamud.Interface.Internal;
using Dalamud.Utility;

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
    [Guid("d3a0fe60-060a-49d6-8f6d-68e2ec5905c5")]
    [StructLayout(LayoutKind.Sequential)]
    private struct TexturePipeline : IUnknown.Interface
    {
        private static readonly Guid MyGuid =
            new(0xd3a0fe60, 0x060a, 0x49d6, 0x8f, 0x6d, 0x68, 0xe2, 0xec, 0x59, 0x05, 0xc5);

        private static readonly nint[] Vtbl;

        private void* vtbl;
        private uint refCount;
        private ComPtr<ID3D11PixelShader> shader;
        private ComPtr<ID3D11SamplerState> sampler;

        static TexturePipeline()
        {
            Vtbl = GC.AllocateArray<nint>(3, true);
            Vtbl[0] = (nint)(delegate*<TexturePipeline*, Guid*, void**, HRESULT>)&StaticQueryInterface;
            Vtbl[1] = (nint)(delegate*<TexturePipeline*, uint>)&StaticAddRef;
            Vtbl[2] = (nint)(delegate*<TexturePipeline*, uint>)&StaticRelease;
        }

        public static Guid* NativeGuid => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in MyGuid));

        public static TexturePipeline* CreateNew(
            ID3D11Device* device,
            ReadOnlySpan<byte> ps) => CreateNew(
            device,
            ps,
            new()
            {
                Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
                AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_WRAP,
                AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_WRAP,
                AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_WRAP,
                MipLODBias = 0,
                MaxAnisotropy = 0,
                ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_ALWAYS,
                MinLOD = 0,
                MaxLOD = 0,
            });

        public static TexturePipeline* CreateNew(
            ID3D11Device* device,
            ReadOnlySpan<byte> ps,
            in D3D11_SAMPLER_DESC samplerDesc)
        {
            using var shader = default(ComPtr<ID3D11PixelShader>);
            fixed (byte* pArray = ps)
                device->CreatePixelShader(pArray, (nuint)ps.Length, null, shader.GetAddressOf()).ThrowHr();

            using var sampler = default(ComPtr<ID3D11SamplerState>);
            fixed (D3D11_SAMPLER_DESC* pSamplerDesc = &samplerDesc)
                device->CreateSamplerState(pSamplerDesc, sampler.GetAddressOf()).ThrowHr();

            var mem = (TexturePipeline*)Marshal.AllocHGlobal(sizeof(TexturePipeline));
            if (mem is null)
                throw new OutOfMemoryException();
            *mem = new() { vtbl = Unsafe.AsPointer(ref Vtbl[0]), refCount = 1 };
            shader.Swap(ref mem->shader);
            sampler.Swap(ref mem->sampler);
            return mem;
        }

        public void BindTo(ID3D11DeviceContext* ctx)
        {
            ctx->PSSetShader(this.shader, null, 0);
            ctx->PSSetSamplers(0, 1, this.sampler.GetAddressOf());
        }

        public HRESULT QueryInterface(Guid* riid, void** ppvObject)
        {
            if (riid == null)
                return E.E_INVALIDARG;
            if (ppvObject == null)
                return E.E_POINTER;
            if (*riid == IID.IID_IUnknown || *riid == MyGuid)
                ppvObject[0] = Unsafe.AsPointer(ref this);
            else
                return E.E_NOINTERFACE;
            return S.S_OK;
        }

        public uint AddRef() => Interlocked.Increment(ref this.refCount);

        public uint Release()
        {
            var r = Interlocked.Decrement(ref this.refCount);
            if (r != 0)
                return r;

            this.shader.Reset();
            this.sampler.Reset();
            Marshal.FreeHGlobal((nint)Unsafe.AsPointer(ref this));
            return 0;
        }

        private static HRESULT StaticQueryInterface(TexturePipeline* self, Guid* riid, void** ppvObject) =>
            self->QueryInterface(riid, ppvObject);

        private static uint StaticAddRef(TexturePipeline* self) => self->AddRef();

        private static uint StaticRelease(TexturePipeline* self) => self->Release();
    }

    [Guid("72fe3f82-3ffc-4be9-b008-4aef7a942f55")]
    [StructLayout(LayoutKind.Sequential)]
    private struct TextureData : IUnknown.Interface
    {
        public static readonly Guid MyGuid =
            new(0x72fe3f82, 0x3ffc, 0x4be9, 0xb0, 0x08, 0x4a, 0xef, 0x7a, 0x94, 0x2f, 0x55);

        private static readonly nint[] Vtbl;

        private void* vtbl;
        private uint refCount;
        private ComPtr<ID3D11ShaderResourceView> srv;
        private ComPtr<TexturePipeline> customPipeline;

        static TextureData()
        {
            Vtbl = GC.AllocateArray<nint>(3, true);
            Vtbl[0] = (nint)(delegate*<TextureData*, Guid*, void**, HRESULT>)&StaticQueryInterface;
            Vtbl[1] = (nint)(delegate*<TextureData*, uint>)&StaticAddRef;
            Vtbl[2] = (nint)(delegate*<TextureData*, uint>)&StaticRelease;
        }

        public static Guid* NativeGuid => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in MyGuid));

        public int Width { get; private init; }

        public int Height { get; private init; }

        public ComPtr<TexturePipeline> CustomPipeline
        {
            get => this.customPipeline.IsEmpty() ? default : new(this.customPipeline);
            set
            {
                if (this.customPipeline.Get() == value)
                    return;
                this.customPipeline.Reset();
                if (!value.IsEmpty())
                    value.CopyTo(ref this.customPipeline);
            }
        }

        public ID3D11ShaderResourceView* ShaderResourceView => this.srv;

        public static TextureData* CreateNew(ID3D11ShaderResourceView* srv, int width, int height)
        {
            var mem = (TextureData*)Marshal.AllocHGlobal(sizeof(TextureData));
            if (mem is null)
                throw new OutOfMemoryException();
            *mem = new()
            {
                vtbl = Unsafe.AsPointer(ref Vtbl[0]),
                refCount = 1,
                srv = new(srv),
                Width = width,
                Height = height,
            };
            return mem;
        }

        public HRESULT QueryInterface(Guid* riid, void** ppvObject)
        {
            if (riid == null)
                return E.E_INVALIDARG;
            if (ppvObject == null)
                return E.E_POINTER;
            if (*riid == IID.IID_IUnknown || *riid == MyGuid)
                ppvObject[0] = Unsafe.AsPointer(ref this);
            else
                return E.E_NOINTERFACE;
            return S.S_OK;
        }

        public uint AddRef() => Interlocked.Increment(ref this.refCount);

        public uint Release()
        {
            var r = Interlocked.Decrement(ref this.refCount);
            if (r != 0)
                return r;

            this.srv.Reset();
            this.customPipeline.Reset();
            Marshal.FreeHGlobal((nint)Unsafe.AsPointer(ref this));
            return 0;
        }

        private static HRESULT StaticQueryInterface(TextureData* self, Guid* riid, void** ppvObject) =>
            self->QueryInterface(riid, ppvObject);

        private static uint StaticAddRef(TextureData* self) => self->AddRef();

        private static uint StaticRelease(TextureData* self) => self->Release();
    }

    private class TextureWrap : IDalamudTextureWrap, ICloneable
    {
        private ComPtr<TextureData> data;

        internal TextureWrap(TextureData* data) => this.data = new(data);

        ~TextureWrap() => this.ReleaseUnmanagedResources();

        public nint ImGuiHandle => (nint)this.data.Get();

        public int Width => this.data.Get()->Width;

        public int Height => this.data.Get()->Height;

        public void Dispose()
        {
            this.ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public IDalamudTextureWrap Clone()
        {
            var data2 = this.data;
            ObjectDisposedException.ThrowIf(data2.IsEmpty(), this);
            data2.Get()->AddRef();
            return new TextureWrap(data2);
        }

        object ICloneable.Clone() => this.Clone();

        private void ReleaseUnmanagedResources() => this.data.Dispose();
    }
}
