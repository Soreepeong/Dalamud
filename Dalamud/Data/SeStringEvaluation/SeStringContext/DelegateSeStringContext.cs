using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Plugin.Services;

namespace Dalamud.Data.SeStringEvaluation.SeStringContext;

/// <summary>Utility context for providing parameters via delegates.</summary>
public struct DelegateSeStringContext : ISeStringContext
{
    private readonly StringBuilder? resultAccumulator;
    private readonly TryGetUIntParameterDelegate? uintDelegate;
    private readonly TryProduceStringParameterDelegate? stringDelegate;
    private readonly HandlePayloadDelegate? handlePayloadDelegate;

    private unsafe fixed uint placeholderSlots[16];

    /// <summary>Initializes a new instance of the <see cref="DelegateSeStringContext"/> struct.</summary>
    /// <param name="sheetLanguage">The sheet language.</param>
    /// <param name="createString">Whether to accumulate strings from <see cref="ProduceString(ReadOnlySpan{byte})"/>
    /// and <see cref="ProduceString(ReadOnlySpan{char})"/> calls.</param>
    /// <param name="uintDelegate">Handler for query for <c>uint</c> parameters.</param>
    /// <param name="stringDelegate">Handler for query for <see cref="SeStringReadOnlySpan"/> parameters.</param>
    /// <param name="handlePayloadDelegate">The payload handler.</param>
    /// <param name="recursiveEvaluator">The recursive evaluator.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DelegateSeStringContext(
        ClientLanguage sheetLanguage,
        bool createString = false,
        TryGetUIntParameterDelegate? uintDelegate = null,
        TryProduceStringParameterDelegate? stringDelegate = null,
        HandlePayloadDelegate? handlePayloadDelegate = null,
        ISeStringEvaluator? recursiveEvaluator = null)
    {
        this.SheetLanguage = sheetLanguage;
        this.resultAccumulator = createString ? new() : null;
        this.uintDelegate = uintDelegate;
        this.stringDelegate = stringDelegate;
        this.handlePayloadDelegate = handlePayloadDelegate;
        this.RecursiveEvaluator = recursiveEvaluator;
    }

    /// <summary>Gets the uint parameter at the given index.</summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns><c>null</c> if the parameter is not available.</returns>
    public delegate uint? TryGetUIntParameterDelegate(uint parameterIndex);

    /// <summary>Produces the string at the given index.</summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <param name="args">The arguments.</param>
    /// <returns><c>true</c> if the parameter is available.</returns>
    public delegate bool TryProduceStringParameterDelegate(uint parameterIndex, DelegateSeStringCallbackArg args);

    /// <summary>Handles a payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="args">The arguments.</param>
    /// <returns><c>true</c> to suppress further handling of the payload.</returns>
    public delegate bool HandlePayloadDelegate(SePayloadReadOnlySpan payload, DelegateSeStringCallbackArg args);

    /// <summary>Forwards string produce calls.</summary>
    /// <typeparam name="T">The type of span elements.</typeparam>
    /// <param name="span">The string to produce.</param>
    /// <param name="arg">An opaque value that you must forward from <see cref="TryProduceStringParameterDelegate"/>.
    /// </param>
    internal delegate void ProduceStringDelegate<T>(ReadOnlySpan<T> span, nint arg);

    /// <summary>Forwards string produce calls.</summary>
    /// <param name="span">The string to produce.</param>
    /// <param name="arg">An opaque value that you must forward from <see cref="TryProduceStringParameterDelegate"/>.
    /// </param>
    internal delegate void ProduceSeStringDelegate(SeStringReadOnlySpan span, nint arg);

    /// <inheritdoc/>
    public ClientLanguage SheetLanguage { get; }

    /// <summary>Gets the evaluator in use, if specified via constructor.</summary>
    public ISeStringEvaluator? RecursiveEvaluator { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool TryGetPlaceholderNum(byte exprType, out uint value)
    {
        if (exprType is < 0xD0 or > 0xDF)
        {
            value = 0;
            return false;
        }

        value = this.placeholderSlots[exprType - 0xD0];
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void UpdatePlaceholder(byte exprType, uint value)
    {
        if (exprType is >= 0xD0 and <= 0xDF)
            this.placeholderSlots[exprType - 0xD0] = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetLNum(uint parameterIndex, out uint value)
    {
        if (this.uintDelegate?.Invoke(parameterIndex) is { } v)
        {
            value = v;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public unsafe bool TryProduceLStr<TContext>(uint parameterIndex, ref TContext targetContext)
        where TContext : ISeStringContext
    {
        if (this.stringDelegate is not { } del)
            return false;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (TContext* p = &targetContext)
        {
            return del.Invoke(
                parameterIndex,
                new(
                    ref this,
                    static (str, pctx) => ((TContext*)pctx)->ProduceString(str),
                    static (str, pctx) => ((TContext*)pctx)->ProduceString(str),
                    static (str, pctx) => ((TContext*)pctx)->ProduceSeString(str),
                    (nint)p));
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceString(ReadOnlySpan<byte> value)
    {
        if (this.resultAccumulator is not { } acc)
            return;

        Span<char> buf = stackalloc char[Encoding.UTF8.GetCharCount(value)];
        Encoding.UTF8.GetChars(value, buf);
        acc.Append(buf);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceString(ReadOnlySpan<char> value)
    {
        if (this.resultAccumulator is { } acc)
            acc.Append(value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProduceSeString(SeStringReadOnlySpan value) => this.RecursiveEvaluator?.ResolveString(ref this, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool HandlePayload<TContext>(SePayloadReadOnlySpan payload, ref TContext targetContext)
        where TContext : ISeStringContext
    {
        if (this.handlePayloadDelegate is not { } del)
            return false;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (TContext* p = &targetContext)
        {
            return del.Invoke(
                payload,
                new(
                    ref this,
                    static (str, pctx) => ((TContext*)pctx)->ProduceString(str),
                    static (str, pctx) => ((TContext*)pctx)->ProduceString(str),
                    static (str, pctx) => ((TContext*)pctx)->ProduceSeString(str),
                    (nint)p));
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    /// <inheritdoc/>
    public override string ToString() =>
        this.resultAccumulator?.ToString() ?? base.ToString() ?? nameof(DelegateSeStringContext);

    /// <summary>Parameters for <see cref="TryProduceStringParameterDelegate"/>.</summary>
    public readonly ref struct DelegateSeStringCallbackArg
    {
        /// <summary>The current context.</summary>
        public readonly ref DelegateSeStringContext Context;

        private readonly ProduceStringDelegate<byte> byteProducer;
        private readonly ProduceStringDelegate<char> charProducer;
        private readonly ProduceSeStringDelegate seStringProducer;
        private readonly nint arg;

        /// <summary>Initializes a new instance of the <see cref="DelegateSeStringCallbackArg"/> struct.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="byteProducer">The byte span producer.</param>
        /// <param name="charProducer">The char span producer.</param>
        /// <param name="seStringProducer">The SeString producer.</param>
        /// <param name="arg">The opaque argument.</param>
        internal DelegateSeStringCallbackArg(
            ref DelegateSeStringContext context,
            ProduceStringDelegate<byte> byteProducer,
            ProduceStringDelegate<char> charProducer,
            ProduceSeStringDelegate seStringProducer,
            nint arg)
        {
            this.Context = ref context;
            this.byteProducer = byteProducer;
            this.charProducer = charProducer;
            this.seStringProducer = seStringProducer;
            this.arg = arg;
        }

        /// <summary>Produces a UTF-8 string.</summary>
        /// <param name="value">The produced value.</param>
        /// <remarks>This should not be a SeString.</remarks>
        public void ProduceString(ReadOnlySpan<byte> value) => this.byteProducer.Invoke(value, this.arg);

        /// <summary>Produces a UTF-16 string.</summary>
        /// <param name="value">The produced value.</param>
        /// <remarks>This should not be a SeString.</remarks>
        public void ProduceString(ReadOnlySpan<char> value) => this.charProducer.Invoke(value, this.arg);

        /// <summary>Produces a SeString.</summary>
        /// <param name="value">The produced value.</param>
        public void ProduceSeString(SeStringReadOnlySpan value) => this.seStringProducer.Invoke(value, this.arg);
    }
}
