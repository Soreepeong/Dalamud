using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;

namespace Dalamud.Data.SeStringEvaluation.Internal;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

/// <summary>Evaluator for SeString components.</summary>
internal unsafe partial class SeStringEvaluator
{
    private delegate void SeStringEvaluatedDelegate<TContext, in TArg>(
        SeStringEvaluator evaluator,
        ref TContext context,
        ReadOnlySpan<byte> evaluatedString,
        TArg arg)
        where TContext : ISeStringContext;

    private struct BinaryExpressionEvaluationHelper<TContext> : ISeStringContext
        where TContext : ISeStringContext
    {
        public bool OperandResultEquals;

        private readonly SeStringEvaluator evaluator;
        private readonly TContext* wrappedContextPtr;
        private readonly byte* operand2Ptr;
        private readonly int operand2Size;

        private int operand1ResultSize;
        private byte* operand1ResultPtr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BinaryExpressionEvaluationHelper(
            SeStringEvaluator evaluator,
            TContext* context,
            byte* operand2Ptr,
            int operand2Size)
        {
            this.evaluator = evaluator;
            this.wrappedContextPtr = context;
            this.operand2Ptr = operand2Ptr;
            this.operand2Size = operand2Size;
        }

        public readonly bool PreferProduceInChar
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->PreferProduceInChar;
        }

        public ClientLanguage SheetLanguage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->SheetLanguage;
        }

        private readonly SeExpressionReadOnlySpan Operand2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(new ReadOnlySpan<byte>(this.operand2Ptr, this.operand2Size));
        }

        private readonly ReadOnlySpan<byte> Operand1Result
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this.operand1ResultPtr, this.operand1ResultSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlaceholderNum(byte exprType, out uint value) =>
            this.wrappedContextPtr->TryGetPlaceholderNum(exprType, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProducePlaceholder<TContext1>(byte exprType, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProducePlaceholder(exprType, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePlaceholder(byte exprType, uint value) =>
            this.wrappedContextPtr->UpdatePlaceholder(exprType, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HandlePayload<TContext1>(SePayloadReadOnlySpan payload, ref TContext1 targetContext)
            where TContext1 : ISeStringContext => this.wrappedContextPtr->HandlePayload(payload, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetLNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetLNum(parameterIndex, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceLStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceLStr(parameterIndex, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetGNum(parameterIndex, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceGStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceGStr(parameterIndex, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Begin(SeExpressionReadOnlySpan operand1) =>
            this.evaluator.ResolveStringExpression(ref this, operand1);

        public void ProduceString(ReadOnlySpan<byte> value)
        {
            if (this.operand1ResultPtr == null)
            {
                fixed (byte* p = value)
                {
                    this.operand1ResultPtr = p;
                    this.operand1ResultSize = value.Length;
                    this.evaluator.ResolveStringExpression(ref this, this.Operand2);
                }
            }
            else
            {
                this.OperandResultEquals = this.Operand1Result.SequenceEqual(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<char> value)
        {
            Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetByteCount(value)];
            Encoding.UTF8.GetBytes(value, bytes);
            this.ProduceString(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceSeString(SeStringReadOnlySpan value) => this.evaluator.ResolveString(ref this, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceError(string msg) => this.wrappedContextPtr->ProduceError(msg);
    }

    private struct ParameterEvaluationHelper<TContext> : ISeStringContext
        where TContext : ISeStringContext
    {
        private readonly SeStringEvaluator evaluator;
        private readonly TContext* wrappedContextPtr;
        private readonly byte* exprBegin;
        private readonly int exprLen;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ParameterEvaluationHelper(
            SeStringEvaluator evaluator,
            TContext* context,
            byte* exprBegin,
            int exprLen)
        {
            this.evaluator = evaluator;
            this.wrappedContextPtr = context;
            this.exprBegin = exprBegin;
            this.exprLen = exprLen;
        }

        public bool PreferProduceInChar
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->PreferProduceInChar;
        }

        public ClientLanguage SheetLanguage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->SheetLanguage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlaceholderNum(byte exprType, out uint value) =>
            this.wrappedContextPtr->TryGetPlaceholderNum(exprType, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProducePlaceholder<TContext1>(byte exprType, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProducePlaceholder(exprType, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePlaceholder(byte exprType, uint value) =>
            this.wrappedContextPtr->UpdatePlaceholder(exprType, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HandlePayload<TContext1>(SePayloadReadOnlySpan payload, ref TContext1 targetContext)
            where TContext1 : ISeStringContext => this.wrappedContextPtr->HandlePayload(payload, ref targetContext);

        public bool TryGetLNum(uint parameterIndex, out uint value)
        {
            value = 0;
            var span = new ReadOnlySpan<byte>(this.exprBegin, this.exprLen);
            while (!span.IsEmpty)
            {
                if (!SeStringExpressionUtilities.TryDecodeLength(span, out var len))
                    return false;
                if (parameterIndex-- == 0)
                    return this.evaluator.TryResolveUInt(ref this, span[..len], out value);

                span = span[len..];
            }

            return false;
        }

        public bool TryProduceLStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext
        {
            var span = new ReadOnlySpan<byte>(this.exprBegin, this.exprLen);
            while (!span.IsEmpty)
            {
                if (!SeStringExpressionUtilities.TryDecodeLength(span, out var len))
                    return false;
                if (parameterIndex-- == 0)
                    return this.evaluator.ResolveStringExpression(ref this, span[..len]);

                span = span[len..];
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetGNum(parameterIndex, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceGStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceGStr(parameterIndex, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<byte> value) => this.wrappedContextPtr->ProduceString(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<char> value) => this.wrappedContextPtr->ProduceString(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceSeString(SeStringReadOnlySpan value) => this.wrappedContextPtr->ProduceSeString(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceError(string msg) => this.wrappedContextPtr->ProduceError(msg);
    }

    private struct StringExpressionEvaluationHelper<TContext, TArg> : ISeStringContext
        where TContext : ISeStringContext
    {
        private readonly SeStringEvaluator evaluator;
        private readonly TContext* wrappedContextPtr;
        private readonly SeStringEvaluatedDelegate<TContext, TArg> evaluatedDelegate;
        private readonly TArg arg;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringExpressionEvaluationHelper(
            SeStringEvaluator evaluator,
            TContext* context,
            SeStringEvaluatedDelegate<TContext, TArg> evaluatedDelegate,
            TArg arg)
        {
            this.evaluator = evaluator;
            this.wrappedContextPtr = context;
            this.evaluatedDelegate = evaluatedDelegate;
            this.arg = arg;
        }

        public bool PreferProduceInChar
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->PreferProduceInChar;
        }

        public ClientLanguage SheetLanguage
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.wrappedContextPtr->SheetLanguage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPlaceholderNum(byte exprType, out uint value) =>
            this.wrappedContextPtr->TryGetPlaceholderNum(exprType, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProducePlaceholder<TContext1>(byte exprType, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProducePlaceholder(exprType, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdatePlaceholder(byte exprType, uint value) =>
            this.wrappedContextPtr->UpdatePlaceholder(exprType, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HandlePayload<TContext1>(SePayloadReadOnlySpan payload, ref TContext1 targetContext)
            where TContext1 : ISeStringContext => this.wrappedContextPtr->HandlePayload(payload, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetLNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetLNum(parameterIndex, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceLStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceLStr(parameterIndex, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGNum(uint parameterIndex, out uint value) =>
            this.wrappedContextPtr->TryGetGNum(parameterIndex, out value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryProduceGStr<TContext1>(uint parameterIndex, ref TContext1 targetContext)
            where TContext1 : ISeStringContext =>
            this.wrappedContextPtr->TryProduceGStr(parameterIndex, ref targetContext);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<byte> value) =>
            this.evaluatedDelegate.Invoke(this.evaluator, ref *this.wrappedContextPtr, value, this.arg);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceString(ReadOnlySpan<char> value)
        {
            Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetByteCount(value)];
            Encoding.UTF8.GetBytes(value, bytes);
            this.ProduceString(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceSeString(SeStringReadOnlySpan value) => this.evaluator.ResolveString(ref this, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProduceError(string msg) => this.wrappedContextPtr->ProduceError(msg);
    }
}
