using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Dalamud.Game.Text.SeStringHandling.SeStringSpan;

namespace Dalamud.Data.SeStringEvaluation.SeStringContext.Internal;

/// <summary>An unmanaged wrapper for a managed context.</summary>
internal struct ManagedContextWrapper : ISeStringContext, IDisposable
{
    private GCHandle handle;

    /// <summary>Initializes a new instance of the <see cref="ManagedContextWrapper"/> struct.</summary>
    /// <param name="context">The context being wrapped.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ManagedContextWrapper(ISeStringContext? context) =>
        this.handle = context is null ? default : GCHandle.Alloc(context);

    /// <summary>Gets the referenced value.</summary>
    public ISeStringContext? Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.handle.IsAllocated ? this.handle.Target as ISeStringContext : null;
    }

    /// <inheritdoc/>
    public ClientLanguage SheetLanguage
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Value?.SheetLanguage ?? ClientLanguage.English;
    }

    /// <inheritdoc/>
    public bool PreferProduceInChar
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.Value?.PreferProduceInChar ?? true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (this.handle.IsAllocated)
            this.handle.Free();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPlaceholderNum(byte exprType, out uint value)
    {
        if (this.Value?.TryGetPlaceholderNum(exprType, out value) is true)
            return true;
        value = 0;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryProducePlaceholder<TContext1>(byte exprType, ref TContext1 targetContext)
        where TContext1 : ISeStringContext => this.Value?.TryProducePlaceholder(exprType, ref targetContext) ?? false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdatePlaceholder(byte exprType, uint value) =>
        this.Value?.UpdatePlaceholder(exprType, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetLNum(uint parameterIndex, out uint value)
    {
        if (this.Value?.TryGetLNum(parameterIndex, out value) is true)
            return true;
        value = 0;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryProduceLStr<TContext>(uint parameterIndex, ref TContext targetContext)
        where TContext : ISeStringContext => this.Value?.TryProduceLStr(parameterIndex, ref targetContext) ?? false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetGNum(uint parameterIndex, out uint value)
    {
        if (this.Value?.TryGetGNum(parameterIndex, out value) is true)
            return true;
        value = 0;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryProduceGStr<TContext>(uint parameterIndex, ref TContext targetContext)
        where TContext : ISeStringContext => this.Value?.TryProduceGStr(parameterIndex, ref targetContext) ?? false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceString(ReadOnlySpan<byte> value) => this.Value?.ProduceString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceString(ReadOnlySpan<char> value) => this.Value?.ProduceString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceSeString(SeStringReadOnlySpan value) => this.Value?.ProduceSeString(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceNewLine() => this.Value?.ProduceNewLine();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceError(string msg) => this.Value?.ProduceError(msg);

    /// <inheritdoc/>
    public bool HandlePayload<TContext>(SePayloadReadOnlySpan payload, ref TContext context)
        where TContext : ISeStringContext => this.Value?.HandlePayload(payload, ref context) ?? false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushForeColor(uint colorBgra) => this.Value?.PushForeColor(colorBgra);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopForeColor() => this.Value?.PopForeColor();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PushBorderColor(uint colorBgra) => this.Value?.PushBorderColor(colorBgra);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopBorderColor() => this.Value?.PopBorderColor();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetItalic(bool useItalic) => this.Value?.SetItalic(useItalic);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBold(bool useBold) => this.Value?.SetBold(useBold);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawIcon(uint iconId) => this.Value?.DrawIcon(iconId);
}
