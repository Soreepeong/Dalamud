using System.Runtime.CompilerServices;

using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Component.Text;

using Lumina.Text.Expressions;

namespace Dalamud.Data.SeStringEvaluation.Internal;

/// <summary>Evaluator for SeString components.</summary>
internal unsafe partial class SeStringEvaluator
{
    /// <inheritdoc cref="ISeStringEvaluator.TryResolveUInt{TContext}"/>
    public bool TryResolveUInt<TContext>(ref TContext context, SeExpressionReadOnlySpan expression, out uint value)
        where TContext : ISeStringContext
    {
        if (expression.TryGetUInt(out value))
            return true;

        if (expression.TryGetPlaceholderExpression(out var exprType))
        {
            if (context.TryGetPlaceholderNum(exprType, out value))
                return true;

            switch ((ExpressionType)exprType)
            {
                case ExpressionType.Millisecond:
                    value = (uint)DateTime.Now.Millisecond;
                    return true;
                case ExpressionType.Second:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_sec;
                    return true;
                case ExpressionType.Minute:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_min;
                    return true;
                case ExpressionType.Hour:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_hour;
                    return true;
                case ExpressionType.Day:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_mday;
                    return true;
                case ExpressionType.Weekday:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_wday;
                    return true;
                case ExpressionType.Month:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_mon + 1;
                    return true;
                case ExpressionType.Year:
                    value = (uint)MacroDecoder.GetMacroTime()->tm_year + 1900;
                    return true;
                default:
                    return false;
            }
        }

        if (expression.TryGetParameterExpression(out exprType, out var operand1))
        {
            if (!this.TryResolveUInt(ref context, operand1, out var paramIndex))
                return false;
            if (paramIndex == 0)
                return false;
            paramIndex--;
            switch ((ExpressionType)exprType)
            {
                case ExpressionType.IntegerParameter: // lnum
                    return context.TryGetLNum(paramIndex, out value);

                case ExpressionType.PlayerParameter: // gnum
                    return context.TryGetGNum(paramIndex, out value)
                           || this.TryGetGNumDefault(ref context, paramIndex, out value);

                case ExpressionType.ObjectParameter: // gstr
                case ExpressionType.StringParameter: // lstr
                default:
                    return false;
            }
        }

        if (expression.TryGetBinaryExpression(out exprType, out operand1, out var operand2))
        {
            switch ((ExpressionType)exprType)
            {
                case ExpressionType.GreaterThanOrEqualTo:
                case ExpressionType.GreaterThan:
                case ExpressionType.LessThanOrEqualTo:
                case ExpressionType.LessThan:
                    if (!this.TryResolveInt(ref context, operand1, out var value1)
                        || !this.TryResolveInt(ref context, operand2, out var value2))
                        return false;
                    value = (ExpressionType)exprType switch
                    {
                        ExpressionType.GreaterThanOrEqualTo => value1 >= value2 ? 1u : 0u,
                        ExpressionType.GreaterThan => value1 > value2 ? 1u : 0u,
                        ExpressionType.LessThanOrEqualTo => value1 <= value2 ? 1u : 0u,
                        ExpressionType.LessThan => value1 < value2 ? 1u : 0u,
                        _ => 0u,
                    };
                    return true;

                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                    fixed (byte* operand2Ptr = operand2.Body)
                    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                        fixed (TContext* pContext = &context)
                        {
                            var tmp = new BinaryExpressionEvaluationHelper<TContext>(
                                this,
                                pContext,
                                operand2Ptr,
                                operand2.Body.Length);
                            tmp.Begin(operand1);

                            if ((ExpressionType)exprType == ExpressionType.Equal)
                                value = tmp.OperandResultEquals ? 1u : 0u;
                            else
                                value = tmp.OperandResultEquals ? 0u : 1u;
                        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

                        return true;
                    }

                default:
                    return false;
            }
        }

        return false;
    }

    /// <inheritdoc cref="ISeStringEvaluator.TryResolveInt{TContext}"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolveInt<TContext>(ref TContext context, SeExpressionReadOnlySpan expression, out int value)
        where TContext : ISeStringContext
    {
        if (this.TryResolveUInt(ref context, expression, out var u32))
        {
            value = (int)u32;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc cref="ISeStringEvaluator.TryResolveBool{TContext}"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolveBool<TContext>(ref TContext context, SeExpressionReadOnlySpan expression, out bool value)
        where TContext : ISeStringContext
    {
        if (this.TryResolveUInt(ref context, expression, out var u32))
        {
            value = u32 != 0;
            return true;
        }

        value = false;
        return false;
    }

    /// <inheritdoc cref="ISeStringEvaluator.ResolveStringExpression{TContext}"/>
    public bool ResolveStringExpression<TContext>(ref TContext context, SeExpressionReadOnlySpan expression)
        where TContext : ISeStringContext
    {
        uint u32;

        if (expression.TryGetString(out var innerString))
            return this.ResolveString(ref context, innerString);

        if (expression.TryGetPlaceholderExpression(out var exprType))
        {
            if (context.TryProducePlaceholder(exprType, ref context))
                return true;
        }

        if (expression.TryGetParameterExpression(out exprType, out var operand1))
        {
            if (!this.TryResolveUInt(ref context, operand1, out var paramIndex))
                return false;
            if (paramIndex == 0)
                return false;
            paramIndex--;
            switch ((ExpressionType)exprType)
            {
                case ExpressionType.IntegerParameter: // lnum
                    return context.TryGetLNum(paramIndex, out u32)
                           && this.Produce(ref context, "{0}", unchecked((int)u32));

                case ExpressionType.StringParameter: // lstr
                    return context.TryProduceLStr(paramIndex, ref context);

                case ExpressionType.PlayerParameter: // gnum
                    if (context.TryGetGNum(paramIndex, out u32)
                        || this.TryGetGNumDefault(ref context, paramIndex, out u32))
                        return this.Produce(ref context, "{0}", unchecked((int)u32));
                    return false;

                case ExpressionType.ObjectParameter: // gstr
                    return context.TryProduceGStr(paramIndex, ref context)
                           || this.TryProduceGStrDefault(paramIndex, ref context);

                default:
                    return false;
            }
        }

        // Handles UInt and Binary expressions
        return
            this.TryResolveUInt(ref context, expression, out u32)
            && this.Produce(ref context, "{0}", (int)u32);
    }
}
