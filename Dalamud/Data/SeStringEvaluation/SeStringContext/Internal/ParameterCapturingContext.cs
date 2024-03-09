using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Interface.SeStringRenderer.Internal;
using Dalamud.Plugin.Services;

using Lumina.Text.Expressions;

using Microsoft.Extensions.ObjectPool;

namespace Dalamud.Data.SeStringEvaluation.SeStringContext.Internal;

/// <summary>A context for embedding the current parameters into a SeString.</summary>
/// <typeparam name="TContext">The context type.</typeparam>
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
internal unsafe struct ParameterCapturingContext<TContext> : ISeStringContext
    where TContext : ISeStringContext
{
    private readonly ISeStringEvaluator evaluator;
    private readonly ObjectPool<MemoryStream> memoryStreamPool;
    private readonly TContext* context;
    private MemoryStream stream;

    /// <summary>Initializes a new instance of the <see cref="ParameterCapturingContext{TContext}"/> struct.</summary>
    /// <param name="evaluator">An instance of <see cref="ISeStringEvaluator"/>.</param>
    /// <param name="memoryStreamPool">A memory stream pool.</param>
    /// <param name="stream">The result stream to write the result to.</param>
    /// <param name="context">The evaluation context.</param>
    /// <remarks><paramref name="context"/> must be <c>fixed</c>.</remarks>
    public ParameterCapturingContext(
        ISeStringEvaluator evaluator,
        ObjectPool<MemoryStream> memoryStreamPool,
        MemoryStream stream,
        ref TContext context)
    {
        this.evaluator = evaluator;
        this.memoryStreamPool = memoryStreamPool;
        this.context = (TContext*)Unsafe.AsPointer(ref context);
        this.stream = stream;
    }

    /// <inheritdoc/>
    public bool PreferProduceInChar => false;

    /// <inheritdoc/>
    public ClientLanguage SheetLanguage => this.context->SheetLanguage;

    /// <inheritdoc/>
    public bool TryGetPlaceholderNum(byte exprType, out uint value) =>
        this.context->TryGetPlaceholderNum(exprType, out value);

    /// <inheritdoc/>
    public bool TryProducePlaceholder<TContext1>(byte exprType, ref TContext1 targetContext)
        where TContext1 : ISeStringContext => this.context->TryProducePlaceholder(exprType, ref targetContext);

    /// <inheritdoc/>
    public void UpdatePlaceholder(byte exprType, uint value) => this.context->UpdatePlaceholder(exprType, value);

    /// <inheritdoc/>
    public bool TryGetLNum(uint parameterIndex, out uint value) =>
        this.context->TryGetLNum(parameterIndex, out value);

    /// <inheritdoc/>
    public bool TryProduceLStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
        where TContext1 : ISeStringContext => this.context->TryProduceLStr(parameterIndex, ref targetContext);

    /// <inheritdoc/>
    public bool TryGetGNum(uint parameterIndex, out uint value) =>
        this.context->TryGetGNum(parameterIndex, out value);

    /// <inheritdoc/>
    public bool TryProduceGStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
        where TContext1 : ISeStringContext => this.context->TryProduceGStr(parameterIndex, ref targetContext);

    /// <inheritdoc/>
    public void ProduceString(ReadOnlySpan<byte> value) => this.stream.Write(value);

    /// <inheritdoc/>
    public void ProduceString(ReadOnlySpan<char> value)
    {
        var off = (int)this.stream.Length;
        var len = Encoding.UTF8.GetByteCount(value);
        this.stream.SetLength(off + len);
        this.stream.Position += Encoding.UTF8.GetBytes(value, this.stream.GetBuffer().AsSpan(off, len));
    }

    /// <inheritdoc/>
    public void ProduceSeString(SeStringReadOnlySpan value) => this.evaluator.ResolveString(ref this, value);

    /// <inheritdoc/>
    public void ProduceError(string msg) => this.context->ProduceError(msg);

    /// <inheritdoc/>
    public bool HandlePayload<TContext1>(SePayloadReadOnlySpan payload, ref TContext1 context1)
        where TContext1 : ISeStringContext
    {
        // Let the time value get captured to nullary expressions.
        if (payload.MacroCode is MacroCode.SetTime or MacroCode.SetResetTime)
            return this.context->HandlePayload(payload, ref context1);

        if (payload.IsText)
        {
            this.ProduceString(payload.Body);
        }
        else if (payload.IsInvalid)
        {
            this.context->ProduceError("Invalid payload detected.");
        }
        else
        {
            var ms2 = this.memoryStreamPool.Get();
            Span<byte> buf = stackalloc byte[5];
            foreach (var expr in payload)
                this.WriteExpression(expr, ms2);

            this.stream.WriteByte(2);
            this.stream.WriteByte((byte)payload.Type);
            this.stream.Write(buf[..^SeStringExpressionUtilities.EncodeUInt(buf, (uint)ms2.Length).Length]);
            ms2.WriteTo(this.stream);
            this.stream.WriteByte(3);
            this.memoryStreamPool.Return(ms2);
        }

        return true;
    }

    private void WriteExpression(SeExpressionReadOnlySpan expr, MemoryStream outStream)
    {
        if (expr.Body[0] == (byte)ExpressionType.StackColor)
        {
            outStream.WriteByte(expr.Body[0]);
            return;
        }

        if (expr.TryGetBinaryExpression(out var exprType, out var operand1, out var operand2))
        {
            outStream.WriteByte(exprType);
            this.WriteExpression(operand1, outStream);
            this.WriteExpression(operand2, outStream);
            return;
        }

        Span<byte> buf = stackalloc byte[5];

        if (this.evaluator.TryResolveUInt(ref this, expr, out var u32))
        {
            outStream.Write(buf[..^SeStringExpressionUtilities.EncodeUInt(buf, u32).Length]);
            return;
        }

        var ms3 = this.memoryStreamPool.Get();
        (this.stream, ms3) = (ms3, this.stream);
        _ = this.evaluator.ResolveStringExpression(ref this, expr);
        (this.stream, ms3) = (ms3, this.stream);
        outStream.WriteByte(0xFF);
        outStream.Write(buf[..^SeStringExpressionUtilities.EncodeUInt(buf, (uint)ms3.Length).Length]);
        ms3.WriteTo(outStream);
        this.memoryStreamPool.Return(ms3);
    }
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
