using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Dalamud.Interface.Internal;
using Dalamud.Utility;

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
    [Guid("a29f9ceb-f89f-4f35-bc1c-a5f7dc8af0ff")]
    [StructLayout(LayoutKind.Sequential)]
    private struct TexturePipeline : IUnknown.Interface
    {
        public static readonly Guid MyGuid =
            new(0xa29f9ceb, 0xf89f, 0x4f35, 0xbc, 0x1c, 0xa5, 0xf7, 0xdc, 0x8a, 0xf0, 0xff);

        private static readonly nint[] Vtbl;

        private void* vtbl;
        private uint refCount;
        private ComPtr<ID3D12RootSignature> rootSignature;
        private ComPtr<ID3D12PipelineState> pipelineState;

        static TexturePipeline()
        {
            Vtbl = GC.AllocateArray<nint>(3, true);
            Vtbl[0] = (nint)(delegate*<TexturePipeline*, Guid*, void**, HRESULT>)&StaticQueryInterface;
            Vtbl[1] = (nint)(delegate*<TexturePipeline*, uint>)&StaticAddRef;
            Vtbl[2] = (nint)(delegate*<TexturePipeline*, uint>)&StaticRelease;
        }

        public static Guid* NativeGuid => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in MyGuid));

        public static TexturePipeline* CreateNew(
            ID3D12Device* device,
            DXGI_FORMAT rtvFormat,
            ReadOnlySpan<byte> vs,
            ReadOnlySpan<byte> ps) => CreateNew(
            device,
            rtvFormat,
            vs,
            ps,
            new()
            {
                Filter = D3D12_FILTER.D3D12_FILTER_MIN_MAG_MIP_LINEAR,
                AddressU = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_WRAP,
                AddressV = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_WRAP,
                AddressW = D3D12_TEXTURE_ADDRESS_MODE.D3D12_TEXTURE_ADDRESS_MODE_WRAP,
                MipLODBias = 0,
                MaxAnisotropy = 0,
                ComparisonFunc = D3D12_COMPARISON_FUNC.D3D12_COMPARISON_FUNC_ALWAYS,
                BorderColor = D3D12_STATIC_BORDER_COLOR.D3D12_STATIC_BORDER_COLOR_TRANSPARENT_BLACK,
                MinLOD = 0,
                MaxLOD = 0,
                ShaderRegister = 0,
                RegisterSpace = 0,
                ShaderVisibility = D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_PIXEL,
            });

        public static TexturePipeline* CreateNew(
            ID3D12Device* device,
            DXGI_FORMAT rtvFormat,
            ReadOnlySpan<byte> vs,
            ReadOnlySpan<byte> ps,
            in D3D12_STATIC_SAMPLER_DESC samplerDesc)
        {
            var descRange = new D3D12_DESCRIPTOR_RANGE
            {
                RangeType = D3D12_DESCRIPTOR_RANGE_TYPE.D3D12_DESCRIPTOR_RANGE_TYPE_SRV,
                NumDescriptors = 1,
                BaseShaderRegister = 0,
                RegisterSpace = 0,
                OffsetInDescriptorsFromTableStart = 0,
            };

            var rootParams = stackalloc D3D12_ROOT_PARAMETER[]
            {
                new()
                {
                    ParameterType = D3D12_ROOT_PARAMETER_TYPE.D3D12_ROOT_PARAMETER_TYPE_32BIT_CONSTANTS,
                    Constants = new() { ShaderRegister = 0, RegisterSpace = 0, Num32BitValues = 16 },
                    ShaderVisibility = D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_VERTEX,
                },
                new()
                {
                    ParameterType = D3D12_ROOT_PARAMETER_TYPE.D3D12_ROOT_PARAMETER_TYPE_DESCRIPTOR_TABLE,
                    DescriptorTable = new() { NumDescriptorRanges = 1, pDescriptorRanges = &descRange },
                    ShaderVisibility = D3D12_SHADER_VISIBILITY.D3D12_SHADER_VISIBILITY_PIXEL,
                },
            };

            using var rootSignatureTemp = default(ComPtr<ID3D12RootSignature>);
            using (var successBlob = default(ComPtr<ID3DBlob>))
            using (var errorBlob = default(ComPtr<ID3DBlob>))
            {
                fixed (D3D12_STATIC_SAMPLER_DESC* pSamplerDesc = &samplerDesc)
                {
                    var signatureDesc = new D3D12_ROOT_SIGNATURE_DESC
                    {
                        NumParameters = 2,
                        pParameters = rootParams,
                        NumStaticSamplers = 1,
                        pStaticSamplers = pSamplerDesc,
                        Flags =
                            D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT |
                            D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_HULL_SHADER_ROOT_ACCESS |
                            D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_DOMAIN_SHADER_ROOT_ACCESS |
                            D3D12_ROOT_SIGNATURE_FLAGS.D3D12_ROOT_SIGNATURE_FLAG_DENY_GEOMETRY_SHADER_ROOT_ACCESS,
                    };

                    var hr = DirectX.D3D12SerializeRootSignature(
                        &signatureDesc,
                        D3D_ROOT_SIGNATURE_VERSION.D3D_ROOT_SIGNATURE_VERSION_1,
                        successBlob.GetAddressOf(),
                        errorBlob.GetAddressOf());
                    if (hr.FAILED)
                    {
                        var err = new Span<byte>(
                            errorBlob.Get()->GetBufferPointer(),
                            (int)errorBlob.Get()->GetBufferSize());
                        throw new AggregateException(Encoding.UTF8.GetString(err), Marshal.GetExceptionForHR(hr)!);
                    }
                }

                fixed (Guid* piid = &IID.IID_ID3D12RootSignature)
                {
                    device->CreateRootSignature(
                        0,
                        successBlob.Get()->GetBufferPointer(),
                        successBlob.Get()->GetBufferSize(),
                        piid,
                        (void**)rootSignatureTemp.GetAddressOf()).ThrowHr();
                }
            }

            var pipelineStateTemp = default(ComPtr<ID3D12PipelineState>);
            fixed (void* pvs = vs)
            fixed (void* pps = ps)
            fixed (Guid* piidPipelineState = &IID.IID_ID3D12PipelineState)
            fixed (void* pszPosition = "POSITION"u8)
            fixed (void* pszTexCoord = "TEXCOORD"u8)
            fixed (void* pszColor = "COLOR"u8)
            {
                var layout = stackalloc D3D12_INPUT_ELEMENT_DESC[]
                {
                    new()
                    {
                        SemanticName = (sbyte*)pszPosition,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        AlignedByteOffset = uint.MaxValue,
                        InputSlotClass = D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA,
                    },
                    new()
                    {
                        SemanticName = (sbyte*)pszTexCoord,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        AlignedByteOffset = uint.MaxValue,
                        InputSlotClass = D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA,
                    },
                    new()
                    {
                        SemanticName = (sbyte*)pszColor,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM,
                        AlignedByteOffset = uint.MaxValue,
                        InputSlotClass = D3D12_INPUT_CLASSIFICATION.D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA,
                    },
                };
                var pipelineDesc = new D3D12_GRAPHICS_PIPELINE_STATE_DESC
                {
                    NodeMask = 1,
                    PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE.D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE,
                    pRootSignature = rootSignatureTemp,
                    SampleMask = uint.MaxValue,
                    NumRenderTargets = 1,
                    RTVFormats = new() { e0 = rtvFormat },
                    SampleDesc = new(1, 0),
                    Flags = D3D12_PIPELINE_STATE_FLAGS.D3D12_PIPELINE_STATE_FLAG_NONE,
                    VS = new(pvs, (nuint)vs.Length),
                    PS = new(pps, (nuint)ps.Length),
                    InputLayout = new() { pInputElementDescs = layout, NumElements = 3 },
                    BlendState = new()
                    {
                        AlphaToCoverageEnable = false,
                        RenderTarget = new()
                        {
                            e0 = new()
                            {
                                BlendEnable = true,
                                SrcBlend = D3D12_BLEND.D3D12_BLEND_SRC_ALPHA,
                                DestBlend = D3D12_BLEND.D3D12_BLEND_INV_SRC_ALPHA,
                                BlendOp = D3D12_BLEND_OP.D3D12_BLEND_OP_ADD,
                                SrcBlendAlpha = D3D12_BLEND.D3D12_BLEND_ONE,
                                DestBlendAlpha = D3D12_BLEND.D3D12_BLEND_INV_SRC_ALPHA,
                                BlendOpAlpha = D3D12_BLEND_OP.D3D12_BLEND_OP_ADD,
                                RenderTargetWriteMask = (byte)D3D12_COLOR_WRITE_ENABLE.D3D12_COLOR_WRITE_ENABLE_ALL,
                            },
                        },
                    },
                    RasterizerState = new()
                    {
                        FillMode = D3D12_FILL_MODE.D3D12_FILL_MODE_SOLID,
                        CullMode = D3D12_CULL_MODE.D3D12_CULL_MODE_NONE,
                        FrontCounterClockwise = false,
                        DepthBias = D3D12.D3D12_DEFAULT_DEPTH_BIAS,
                        DepthBiasClamp = D3D12.D3D12_DEFAULT_DEPTH_BIAS_CLAMP,
                        SlopeScaledDepthBias = D3D12.D3D12_DEFAULT_SLOPE_SCALED_DEPTH_BIAS,
                        DepthClipEnable = true,
                        MultisampleEnable = false,
                        AntialiasedLineEnable = false,
                        ForcedSampleCount = 0,
                        ConservativeRaster =
                            D3D12_CONSERVATIVE_RASTERIZATION_MODE.D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF,
                    },
                    DepthStencilState = new()
                    {
                        DepthEnable = false,
                        DepthWriteMask = D3D12_DEPTH_WRITE_MASK.D3D12_DEPTH_WRITE_MASK_ALL,
                        DepthFunc = D3D12_COMPARISON_FUNC.D3D12_COMPARISON_FUNC_ALWAYS,
                        StencilEnable = false,
                        FrontFace = new()
                        {
                            StencilFailOp = D3D12_STENCIL_OP.D3D12_STENCIL_OP_KEEP,
                            StencilDepthFailOp = D3D12_STENCIL_OP.D3D12_STENCIL_OP_KEEP,
                            StencilPassOp = D3D12_STENCIL_OP.D3D12_STENCIL_OP_KEEP,
                            StencilFunc = D3D12_COMPARISON_FUNC.D3D12_COMPARISON_FUNC_ALWAYS,
                        },
                        BackFace = new()
                        {
                            StencilFailOp = D3D12_STENCIL_OP.D3D12_STENCIL_OP_KEEP,
                            StencilDepthFailOp = D3D12_STENCIL_OP.D3D12_STENCIL_OP_KEEP,
                            StencilPassOp = D3D12_STENCIL_OP.D3D12_STENCIL_OP_KEEP,
                            StencilFunc = D3D12_COMPARISON_FUNC.D3D12_COMPARISON_FUNC_ALWAYS,
                        },
                    },
                };
                device->CreateGraphicsPipelineState(
                    &pipelineDesc,
                    piidPipelineState,
                    (void**)pipelineStateTemp.GetAddressOf()).ThrowHr();
            }

            var mem = (TexturePipeline*)Marshal.AllocHGlobal(sizeof(TexturePipeline));
            if (mem is null)
                throw new OutOfMemoryException();
            *mem = new() { vtbl = Unsafe.AsPointer(ref Vtbl[0]), refCount = 1 };
            rootSignatureTemp.Swap(ref mem->rootSignature);
            pipelineStateTemp.Swap(ref mem->pipelineState);
            return mem;
        }

        public void BindTo(ID3D12GraphicsCommandList* ctx)
        {
            ctx->SetPipelineState(this.pipelineState);
            ctx->SetGraphicsRootSignature(this.rootSignature);
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

            this.rootSignature.Reset();
            this.pipelineState.Reset();
            Marshal.FreeHGlobal((nint)Unsafe.AsPointer(ref this));
            return 0;
        }

        private static HRESULT StaticQueryInterface(TexturePipeline* self, Guid* riid, void** ppvObject) =>
            self->QueryInterface(riid, ppvObject);

        private static uint StaticAddRef(TexturePipeline* self) => self->AddRef();

        private static uint StaticRelease(TexturePipeline* self) => self->Release();
    }

    [Guid("f58175a6-37da-4daa-82fd-5993f6847643")]
    [StructLayout(LayoutKind.Sequential)]
    private struct TextureData : IUnknown.Interface
    {
        public static readonly Guid MyGuid =
            new(0xf58175a6, 0x37da, 0x4daa, 0x82, 0xfd, 0x59, 0x93, 0xf6, 0x84, 0x76, 0x43);

        private static readonly nint[] Vtbl;

        private void* vtbl;
        private uint refCount;
        private ComPtr<ID3D12Resource> texture;
        private ComPtr<ID3D12Resource> uploadBuffer;
        private ComPtr<TexturePipeline> customPipeline;

        static TextureData()
        {
            Vtbl = GC.AllocateArray<nint>(3, true);
            Vtbl[0] = (nint)(delegate*<TextureData*, Guid*, void**, HRESULT>)&StaticQueryInterface;
            Vtbl[1] = (nint)(delegate*<TextureData*, uint>)&StaticAddRef;
            Vtbl[2] = (nint)(delegate*<TextureData*, uint>)&StaticRelease;
        }

        public static Guid* NativeGuid => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in MyGuid));

        public DXGI_FORMAT Format { get; private init; }

        public int Width { get; private init; }

        public int Height { get; private init; }

        public int UploadPitch { get; private init; }

        public D3D12_RESOURCE_BARRIER Barrier => new()
        {
            Type = D3D12_RESOURCE_BARRIER_TYPE.D3D12_RESOURCE_BARRIER_TYPE_TRANSITION,
            Flags = D3D12_RESOURCE_BARRIER_FLAGS.D3D12_RESOURCE_BARRIER_FLAG_NONE,
            Transition = new()
            {
                pResource = this.texture,
                Subresource = D3D12.D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES,
                StateBefore = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
                StateAfter = D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COMMON,
            },
        };

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

        public ID3D12Resource* Texture => this.texture;

        public static TextureData* CreateNew(
            DXGI_FORMAT format,
            int width,
            int height,
            int uploadPitch,
            ID3D12Resource* texture,
            ID3D12Resource* uploadBuffer)
        {
            var mem = (TextureData*)Marshal.AllocHGlobal(sizeof(TextureData));
            if (mem is null)
                throw new OutOfMemoryException();
            *mem = new()
            {
                vtbl = Unsafe.AsPointer(ref Vtbl[0]),
                refCount = 1,
                texture = new(texture),
                uploadBuffer = new(uploadBuffer),
                Format = format,
                Width = width,
                Height = height,
                UploadPitch = uploadPitch,
            };
            return mem;
        }

        public void ClearUploadBuffer() => this.uploadBuffer.Reset();

        public void WriteCopyCommand(ID3D12GraphicsCommandList* cmdList)
        {
            var srcLocation = new D3D12_TEXTURE_COPY_LOCATION
            {
                pResource = this.uploadBuffer,
                Type = D3D12_TEXTURE_COPY_TYPE.D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT,
                PlacedFootprint = new()
                {
                    Footprint = new()
                    {
                        Format = this.Format,
                        Width = (uint)this.Width,
                        Height = (uint)this.Height,
                        Depth = 1,
                        RowPitch = (uint)this.UploadPitch,
                    },
                },
            };

            var dstLocation = new D3D12_TEXTURE_COPY_LOCATION
            {
                pResource = this.texture,
                Type = D3D12_TEXTURE_COPY_TYPE.D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
                SubresourceIndex = 0,
            };

            cmdList->CopyTextureRegion(&dstLocation, 0, 0, 0, &srcLocation, null);
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

            this.texture.Reset();
            this.uploadBuffer.Reset();
            this.customPipeline.Reset();
            Marshal.FreeHGlobal((nint)Unsafe.AsPointer(ref this));
            return 0;
        }

        private static HRESULT StaticQueryInterface(TextureData* self, Guid* riid, void** ppvObject) =>
            self->QueryInterface(riid, ppvObject);

        private static uint StaticAddRef(TextureData* self) => self->AddRef();

        private static uint StaticRelease(TextureData* self) => self->Release();
    }

    private class TextureManager : IDisposable
    {
        private const int UploadBatchSize = 256;

        private readonly object uploadListLock = new();
        private List<ComPtr<TextureData>> pendingUploads = new(UploadBatchSize);
        private List<ComPtr<TextureData>> recycler = new(UploadBatchSize);

        private ComPtr<ID3D12Device> device;
        private ComPtr<ID3D12CommandQueue> commandQueue;
        private ComPtr<ID3D12CommandAllocator> commandAllocator;
        private ComPtr<ID3D12GraphicsCommandList> commandList;
        private ComPtr<ID3D12Fence> fence;
        private HANDLE fenceEvent;
        private ulong fenceValue;

        public TextureManager(ID3D12Device* device)
        {
            try
            {
                device->AddRef();
                this.device.Attach(device);

                fixed (Guid* piid = &IID.IID_ID3D12CommandQueue)
                fixed (ID3D12CommandQueue** ppQueue = &this.commandQueue.GetPinnableReference())
                {
                    var queueDesc = new D3D12_COMMAND_QUEUE_DESC
                    {
                        Type = D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_COPY,
                        Flags = D3D12_COMMAND_QUEUE_FLAGS.D3D12_COMMAND_QUEUE_FLAG_NONE,
                        NodeMask = 1,
                    };
                    device->CreateCommandQueue(&queueDesc, piid, (void**)ppQueue).ThrowHr();
                }

                fixed (Guid* piid = &IID.IID_ID3D12CommandAllocator)
                fixed (ID3D12CommandAllocator** ppAllocator = &this.commandAllocator.GetPinnableReference())
                {
                    device->CreateCommandAllocator(
                        D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_COPY,
                        piid,
                        (void**)ppAllocator).ThrowHr();
                }

                fixed (Guid* piid = &IID.IID_ID3D12GraphicsCommandList)
                fixed (ID3D12GraphicsCommandList** ppList = &this.commandList.GetPinnableReference())
                {
                    device->CreateCommandList(
                        0,
                        D3D12_COMMAND_LIST_TYPE.D3D12_COMMAND_LIST_TYPE_COPY,
                        this.commandAllocator,
                        null,
                        piid,
                        (void**)ppList).ThrowHr();
                    this.commandList.Get()->Close().ThrowHr();
                }

                fixed (Guid* piid = &IID.IID_ID3D12Fence)
                fixed (ID3D12Fence** ppFence = &this.fence.GetPinnableReference())
                {
                    device->CreateFence(
                        0,
                        D3D12_FENCE_FLAGS.D3D12_FENCE_FLAG_NONE,
                        piid,
                        (void**)ppFence).ThrowHr();
                }
            }
            catch
            {
                this.ReleaseUnmanagedResources();
                throw;
            }
        }

        ~TextureManager() => this.ReleaseUnmanagedResources();

        public void Dispose()
        {
            this.ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        public TextureWrap CreateTexture(
            ReadOnlySpan<byte> data,
            int pitch,
            int width,
            int height,
            DXGI_FORMAT format)
        {
            uint numRows;
            ulong cbRow;

            // Create an empty texture of same specifications with the request
            using var texture = default(ComPtr<ID3D12Resource>);
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
                    Width = (ulong)width,
                    Height = (uint)height,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = format,
                    SampleDesc = { Count = 1, Quality = 0 },
                    Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_UNKNOWN,
                    Flags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE,
                };
                this.device.Get()->GetCopyableFootprints(&desc, 0, 1, 0, null, &numRows, &cbRow, null);
                if (pitch != (int)cbRow)
                {
                    throw new ArgumentException(
                        $"The provided pitch {pitch} does not match the calculated pitch of {cbRow}.",
                        nameof(pitch));
                }

                this.device.Get()->CreateCommittedResource(
                    &props,
                    D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
                    &desc,
                    D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_COPY_DEST,
                    null,
                    piid,
                    (void**)texture.GetAddressOf()).ThrowHr();
            }

            var uploadPitch = ((checked((int)cbRow) + D3D12.D3D12_TEXTURE_DATA_PITCH_ALIGNMENT) - 1) &
                              ~(D3D12.D3D12_TEXTURE_DATA_PITCH_ALIGNMENT - 1);
            var uploadSize = checked((int)(numRows * uploadPitch));

            // Upload texture to graphics system
            using var uploadBuffer = default(ComPtr<ID3D12Resource>);
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
                    Alignment = 0,
                    Width = (ulong)uploadSize,
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = DXGI_FORMAT.DXGI_FORMAT_UNKNOWN,
                    SampleDesc = { Count = 1, Quality = 0 },
                    Layout = D3D12_TEXTURE_LAYOUT.D3D12_TEXTURE_LAYOUT_ROW_MAJOR,
                    Flags = D3D12_RESOURCE_FLAGS.D3D12_RESOURCE_FLAG_NONE,
                };
                this.device.Get()->CreateCommittedResource(
                    &props,
                    D3D12_HEAP_FLAGS.D3D12_HEAP_FLAG_NONE,
                    &desc,
                    D3D12_RESOURCE_STATES.D3D12_RESOURCE_STATE_GENERIC_READ,
                    null,
                    piid,
                    (void**)uploadBuffer.GetAddressOf()).ThrowHr();
            }

            try
            {
                void* mapped;
                var range = new D3D12_RANGE(0, (nuint)uploadSize);
                uploadBuffer.Get()->Map(0, &range, &mapped).ThrowHr();
                var source = data;
                var target = new Span<byte>(mapped, uploadSize);
                for (var y = 0; y < numRows; y++)
                {
                    source[..pitch].CopyTo(target);
                    source = source[pitch..];
                    target = target[uploadPitch..];
                }
            }
            finally
            {
                uploadBuffer.Get()->Unmap(0, null);
            }

            using var texData = default(ComPtr<TextureData>);
            texData.Attach(TextureData.CreateNew(format, width, height, uploadPitch, texture, uploadBuffer));
            texData.Get()->AddRef();
            lock (this.uploadListLock)
                this.pendingUploads.Add(texData);

            return new(texData);
        }

        public void FlushPendingTextureUploads()
        {
            lock (this.uploadListLock)
            {
                (this.recycler, this.pendingUploads) = (this.pendingUploads, this.recycler);
                this.pendingUploads.Clear();
            }

            var cmdAlloc = this.commandAllocator.Get();
            var cmdList = this.commandList.Get();
            var cmdQueue = this.commandQueue.Get();

            cmdAlloc->Reset().ThrowHr();
            cmdList->Reset(cmdAlloc, null).ThrowHr();

            var barriers = stackalloc D3D12_RESOURCE_BARRIER[UploadBatchSize];
            for (var i = 0; i < this.recycler.Count; i += UploadBatchSize)
            {
                var count = Math.Min(UploadBatchSize, this.recycler.Count - i);
                var dataSpan = CollectionsMarshal.AsSpan(this.recycler).Slice(i, count);
                for (var j = 0; j < count; j++)
                {
                    dataSpan[j].Get()->WriteCopyCommand(cmdList);
                    barriers[j] = dataSpan[j].Get()->Barrier;
                }

                cmdList->ResourceBarrier((uint)count, barriers);
            }

            cmdList->Close().ThrowHr();

            this.fenceValue = Interlocked.Increment(ref this.fenceValue);
            cmdQueue->ExecuteCommandLists(1, (ID3D12CommandList**)&cmdList);
            cmdQueue->Signal(this.fence, this.fenceValue).ThrowHr();

            this.WaitUploadInternal();
            foreach (ref var v in CollectionsMarshal.AsSpan(this.recycler))
            {
                v.Get()->ClearUploadBuffer();
                v.Reset();
            }

            this.recycler.Clear();
            this.WaitUploadInternal();
        }

        private void WaitUploadInternal()
        {
            if (this.fence.IsEmpty())
                return;

            if (this.fence.Get()->GetCompletedValue() != this.fenceValue)
            {
                this.fenceEvent = Win32.CreateEventW(null, true, false, null);
                if (this.fenceEvent == default)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new();

                if (!Win32.ResetEvent(this.fenceEvent))
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new();
                this.fence.Get()->SetEventOnCompletion(this.fenceValue, this.fenceEvent).ThrowHr();
                if (Win32.WaitForSingleObject(this.fenceEvent, Win32.INFINITE) == WAIT.WAIT_FAILED)
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()) ?? new();
            }
        }

        private void ReleaseUnmanagedResources()
        {
            this.WaitUploadInternal();

            lock (this.uploadListLock)
            {
                foreach (ref var v in CollectionsMarshal.AsSpan(this.pendingUploads))
                    v.Reset();
                this.pendingUploads.Clear();

                foreach (ref var v in CollectionsMarshal.AsSpan(this.recycler))
                    v.Reset();
                this.recycler.Clear();
            }

            this.fence.Reset();
            this.commandAllocator.Reset();
            this.commandQueue.Reset();
            this.commandList.Reset();
            this.device.Reset();
            if (this.fenceEvent != default)
            {
                Win32.CloseHandle(this.fenceEvent);
                this.fenceEvent = default;
            }
        }
    }

    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "Internal")]
    private class TextureWrap : IDalamudTextureWrap
    {
        private ComPtr<TextureData> textureData;

        public TextureWrap(TextureData* coreData)
        {
            coreData->AddRef();
            this.textureData.Attach(coreData);
        }

        ~TextureWrap() => this.ReleaseUnmanagedResources();

        public nint ImGuiHandle
        {
            get
            {
                ObjectDisposedException.ThrowIf(this.textureData.IsEmpty(), this);
                return (nint)this.textureData.Get();
            }
        }

        public int Width
        {
            get
            {
                ObjectDisposedException.ThrowIf(this.textureData.IsEmpty(), this);
                return this.textureData.Get()->Width;
            }
        }

        public int Height
        {
            get
            {
                ObjectDisposedException.ThrowIf(this.textureData.IsEmpty(), this);
                return this.textureData.Get()->Height;
            }
        }

        public void Dispose()
        {
            this.ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources() => this.textureData.Reset();
    }
}
