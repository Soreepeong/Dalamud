using System.IO;

using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;

namespace Dalamud.Plugin.Services;

/// <summary>Evaluator for SeString components.</summary>
/// <remarks>Use the function variants with <c>TContext</c> type parameters to pass value-type implementations of
/// <see cref="ISeStringContext"/>. Otherwise, use the function variants that accept
/// <see cref="ISeStringContext"/>.</remarks>
public interface ISeStringEvaluator
{
    /// <summary>Default implementation for <see cref="ISeStringContext.TryGetGNum"/>.</summary>
    /// <param name="context">The context.</param>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <param name="value">The value.</param>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <returns><c>true</c> if the parameter is produced.</returns>
    bool TryGetGNumDefault<TContext>(ref TContext context, uint parameterIndex, out uint value)
        where TContext : ISeStringContext;

    /// <summary>Default implementation for <see cref="ISeStringContext.TryProduceGStr{TContext}"/>.</summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <param name="context">The context.</param>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <returns><c>true</c> if the parameter is produced.</returns>
    bool TryProduceGStrDefault<TContext>(uint parameterIndex, ref TContext context)
        where TContext : ISeStringContext;

    /// <summary>Attempts to evaluate the given expression as a <c>uint</c>.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The evaluated value.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool TryResolveUInt<TContext>(ref TContext context, SeExpressionReadOnlySpan expression, out uint value)
        where TContext : struct, ISeStringContext;

    /// <summary>Attempts to evaluate the given expression as an <c>int</c>.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The evaluated value.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool TryResolveInt<TContext>(ref TContext context, SeExpressionReadOnlySpan expression, out int value)
        where TContext : struct, ISeStringContext;

    /// <summary>Attempts to evaluate the given expression as a <c>bool</c>.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The evaluated value.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool TryResolveBool<TContext>(ref TContext context, SeExpressionReadOnlySpan expression, out bool value)
        where TContext : struct, ISeStringContext;

    /// <summary>Attempts to resolve the given expression as <see cref="ReadOnlySpan{T}"/>s of <c>byte</c> or
    /// <c>char</c>.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool ResolveStringExpression<TContext>(ref TContext context, SeExpressionReadOnlySpan expression)
        where TContext : struct, ISeStringContext;

    /// <summary>Attempts to resolve the given payload as <see cref="ReadOnlySpan{T}"/>s of <c>byte</c> or
    /// <c>char</c>.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="payload">The payload to evaluate.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool ResolveStringPayload<TContext>(ref TContext context, SePayloadReadOnlySpan payload)
        where TContext : struct, ISeStringContext;

    /// <summary>Attempts to resolve the given string as <see cref="ReadOnlySpan{T}"/>s of <c>byte</c> or
    /// <c>char</c>.</summary>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="value">The string to evaluate.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool ResolveString<TContext>(ref TContext context, SeStringReadOnlySpan value)
        where TContext : struct, ISeStringContext;

    /// <summary>Captures the current parameters and embed the values into a new SeString.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="value">The string to evaluate.</param>
    /// <param name="result">The memory stream to write the result to.</param>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    void CaptureParametersInto<TContext>(ref TContext context, SeStringReadOnlySpan value, MemoryStream result)
        where TContext : struct, ISeStringContext;

    /// <summary>Captures the current parameters and embed the values into a new SeString.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="value">The string to evaluate.</param>
    /// <typeparam name="TContext">The preferrably concrete type of the context.</typeparam>
    /// <returns>The captured SeString.</returns>
    byte[] CaptureParameters<TContext>(ref TContext context, SeStringReadOnlySpan value)
        where TContext : struct, ISeStringContext;

    /// <summary>Attempts to evaluate the given expression as a <c>uint</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The evaluated value.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool TryResolveUInt(ISeStringContext context, SeExpressionReadOnlySpan expression, out uint value);

    /// <summary>Attempts to evaluate the given expression as an <c>int</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The evaluated value.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool TryResolveInt(ISeStringContext context, SeExpressionReadOnlySpan expression, out int value);

    /// <summary>Attempts to evaluate the given expression as a <c>bool</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="value">The evaluated value.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool TryResolveBool(ISeStringContext context, SeExpressionReadOnlySpan expression, out bool value);

    /// <summary>Attempts to resolve the given expression as <see cref="ReadOnlySpan{T}"/>s of <c>byte</c> or
    /// <c>char</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool ResolveStringExpression(ISeStringContext context, SeExpressionReadOnlySpan expression);

    /// <summary>Attempts to resolve the given payload as <see cref="ReadOnlySpan{T}"/>s of <c>byte</c> or
    /// <c>char</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="payload">The payload to evaluate.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool ResolveStringPayload(ISeStringContext context, SePayloadReadOnlySpan payload);

    /// <summary>Attempts to resolve the given string as <see cref="ReadOnlySpan{T}"/>s of <c>byte</c> or
    /// <c>char</c>.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="value">The string to evaluate.</param>
    /// <returns><c>true</c> if evaluated.</returns>
    bool ResolveString(ISeStringContext context, SeStringReadOnlySpan value);

    /// <summary>Captures the current parameters and embed the values into a new SeString.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="value">The string to evaluate.</param>
    /// <param name="result">The memory stream to write the result to.</param>
    void CaptureParametersInto(ISeStringContext context, SeStringReadOnlySpan value, MemoryStream result);

    /// <summary>Captures the current parameters and embed the values into a new SeString.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="value">The string to evaluate.</param>
    /// <returns>The captured data.</returns>
    byte[] CaptureParameters(ISeStringContext context, SeStringReadOnlySpan value);
}
