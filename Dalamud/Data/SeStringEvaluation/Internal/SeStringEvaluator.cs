using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Dalamud.Data.SeStringEvaluation.SeStringContext;
using Dalamud.Data.SeStringEvaluation.SeStringContext.Internal;
using Dalamud.Game.Text.SeStringHandling.SeStringSpan;
using Dalamud.IoC;
using Dalamud.IoC.Internal;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;

using Microsoft.Extensions.ObjectPool;

using LSeString = Lumina.Text.SeString;

namespace Dalamud.Data.SeStringEvaluation.Internal;

/// <summary>Evaluator for SeString components.</summary>
[PluginInterface]
[InterfaceVersion("1.0")]
[ServiceManager.EarlyLoadedService]
#pragma warning disable SA1015
[ResolveVia<ISeStringEvaluator>]
#pragma warning restore SA1015
internal partial class SeStringEvaluator : IServiceType, ISeStringEvaluator
{
    [ServiceManager.ServiceDependency]
    private readonly DataManager dataManager = Service<DataManager>.Get();

    private readonly LanguageSheet[] sheets;

    [ServiceManager.ServiceConstructor]
    private SeStringEvaluator()
    {
        this.sheets = new LanguageSheet[Enum.GetValues<ClientLanguage>().Max(x => (int)x) + 1];
        foreach (var language in Enum.GetValues<ClientLanguage>())
            this.sheets[(int)language] = new(this, language);
    }

    /// <summary>Gets the string builder pool.</summary>
    public ObjectPool<StringBuilder> StringBuilderPool { get; } =
        new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy());

    /// <summary>Gets the memory stream pool.</summary>
    public ObjectPool<MemoryStream> MemoryStreamPool { get; } =
        new DefaultObjectPool<MemoryStream>(new MemoryStreamObjectPoolPolicy());

    /// <inheritdoc/>
    public unsafe bool TryGetGNumDefault<TContext>(ref TContext context, uint parameterIndex, out uint value)
        where TContext : ISeStringContext
    {
        value = 0u;

        var rtm = RaptureTextModule.Instance();
        if (rtm is null)
            return false;

        if (!ThreadSafety.IsMainThread)
        {
            context.ProduceError("Global parameters may only be used from the main thread.");
            return false;
        }

        ref var gp = ref rtm->TextModule.MacroDecoder.GlobalParameters;
        if (parameterIndex >= gp.MySize)
            return false;

        var p = rtm->TextModule.MacroDecoder.GlobalParameters.Get(parameterIndex);
        switch (p.Type)
        {
            case TextParameterType.Integer:
                value = (uint)p.IntValue;
                return true;

            case TextParameterType.Utf8String:
                context.ProduceError($"Requested a number; Utf8String global parameter at {parameterIndex}.");
                return false;

            case TextParameterType.String:
                context.ProduceError($"Requested a number; string global parameter at {parameterIndex}.");
                return false;

            case TextParameterType.Uninitialized:
                context.ProduceError($"Requested a number; uninitialized global parameter at {parameterIndex}.");
                return false;
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public unsafe bool TryProduceGStrDefault<TContext>(uint parameterIndex, ref TContext context)
        where TContext : ISeStringContext
    {
        var rtm = RaptureTextModule.Instance();
        if (rtm is null)
            return false;

        ref var gp = ref rtm->TextModule.MacroDecoder.GlobalParameters;
        if (parameterIndex >= gp.MySize)
            return false;

        if (!ThreadSafety.IsMainThread)
        {
            context.ProduceError("Global parameters may only be used from the main thread.");
            return false;
        }

        var p = rtm->TextModule.MacroDecoder.GlobalParameters.Get(parameterIndex);
        switch (p.Type)
        {
            case TextParameterType.Integer:
                return this.Produce(ref context, "{0}", p.IntValue);
            case TextParameterType.Utf8String:
                return this.ResolveStringPayload(ref context, p.Utf8StringValue->AsSpan());
            case TextParameterType.String:
                return this.ResolveStringPayload(
                    ref context,
                    MemoryHelper.CastNullTerminated<byte>((nint)p.StringValue));
            case TextParameterType.Uninitialized:
            default:
                return false;
        }
    }

    /// <inheritdoc cref="ISeStringEvaluator.ResolveString{TContext}"/>
    public bool ResolveString<TContext>(ref TContext context, SeStringReadOnlySpan value)
        where TContext : ISeStringContext
    {
        var all = true;
        foreach (var payload in value)
            all &= this.ResolveStringPayload(ref context, payload);
        return all;
    }

    /// <inheritdoc cref="ISeStringEvaluator.CaptureParametersInto{TContext}"/>
    public void CaptureParametersInto<TContext>(ref TContext context, SeStringReadOnlySpan value, MemoryStream result)
        where TContext : ISeStringContext
    {
        var c = new ParameterCapturingContext<TContext>(this, this.MemoryStreamPool, result, ref context);
        c.ProduceSeString(value);
    }

    /// <inheritdoc cref="ISeStringEvaluator.CaptureParameters{TContext}"/>
    public byte[] CaptureParameters<TContext>(ref TContext context, SeStringReadOnlySpan value)
        where TContext : ISeStringContext
    {
        var ms = this.MemoryStreamPool.Get();
        var c = new ParameterCapturingContext<TContext>(this, this.MemoryStreamPool, ms, ref context);
        c.ProduceSeString(value);
        var res = ms.ToArray();
        this.MemoryStreamPool.Return(ms);
        return res;
    }

    /// <inheritdoc/>
    bool ISeStringEvaluator.TryResolveUInt<TContext>(
        ref TContext context,
        SeExpressionReadOnlySpan expression,
        out uint value) => this.TryResolveUInt(ref context, expression, out value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.TryResolveInt<TContext>(
        ref TContext context,
        SeExpressionReadOnlySpan expression,
        out int value) => this.TryResolveInt(ref context, expression, out value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.TryResolveBool<TContext>(
        ref TContext context,
        SeExpressionReadOnlySpan expression,
        out bool value) => this.TryResolveBool(ref context, expression, out value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.ResolveStringExpression<TContext>(
        ref TContext context,
        SeExpressionReadOnlySpan expression) => this.ResolveStringExpression(ref context, expression);

    /// <inheritdoc/>
    bool ISeStringEvaluator.ResolveStringPayload<TContext>(
        ref TContext context,
        SePayloadReadOnlySpan payload) => this.ResolveStringPayload(ref context, payload);

    /// <inheritdoc/>
    bool ISeStringEvaluator.ResolveString<TContext>(ref TContext context, SeStringReadOnlySpan value) =>
        this.ResolveString(ref context, value);

    /// <inheritdoc/>
    void ISeStringEvaluator.CaptureParametersInto<TContext>(
        ref TContext context,
        SeStringReadOnlySpan value,
        MemoryStream result) => this.CaptureParametersInto(ref context, value, result);

    /// <inheritdoc/>
    byte[] ISeStringEvaluator.CaptureParameters<TContext>(ref TContext context, SeStringReadOnlySpan value) =>
        this.CaptureParameters(ref context, value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.TryResolveUInt(
        ISeStringContext context,
        SeExpressionReadOnlySpan expression,
        out uint value) => this.TryResolveUInt(ref context, expression, out value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.TryResolveInt(
        ISeStringContext context,
        SeExpressionReadOnlySpan expression,
        out int value) => this.TryResolveInt(ref context, expression, out value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.TryResolveBool(
        ISeStringContext context,
        SeExpressionReadOnlySpan expression,
        out bool value) => this.TryResolveBool(ref context, expression, out value);

    /// <inheritdoc/>
    bool ISeStringEvaluator.ResolveStringExpression(
        ISeStringContext context,
        SeExpressionReadOnlySpan expression) => this.ResolveStringExpression(ref context, expression);

    /// <inheritdoc/>
    bool ISeStringEvaluator.ResolveStringPayload(
        ISeStringContext context,
        SePayloadReadOnlySpan payload) => this.ResolveStringPayload(ref context, payload);

    /// <inheritdoc/>
    bool ISeStringEvaluator.ResolveString(
        ISeStringContext context,
        SeStringReadOnlySpan value) => this.ResolveString(ref context, value);

    /// <inheritdoc/>
    void ISeStringEvaluator.CaptureParametersInto(
        ISeStringContext context,
        SeStringReadOnlySpan value,
        MemoryStream result) => this.CaptureParametersInto(ref context, value, result);

    /// <inheritdoc/>
    byte[] ISeStringEvaluator.CaptureParameters(ISeStringContext context, SeStringReadOnlySpan value) =>
        this.CaptureParameters(ref context, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Produce<TContext>(ref TContext context, ReadOnlySpan<byte> s)
        where TContext : ISeStringContext
    {
        context.ProduceString(s);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Produce<TContext>(ref TContext context, ReadOnlySpan<char> s)
        where TContext : ISeStringContext
    {
        context.ProduceString(s);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Produce<TContext>(ref TContext context, ReadOnlySpan<byte> u8, ReadOnlySpan<char> u16)
        where TContext : ISeStringContext
    {
        if (context.PreferProduceInChar)
            context.ProduceString(u16);
        else
            context.ProduceString(u8);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool Produce<TContext>(ref TContext context, char charVal)
        where TContext : ISeStringContext
    {
        if (context.PreferProduceInChar || charVal >= 128)
            return Produce(ref context, new ReadOnlySpan<char>(&charVal, 1));
        return Produce(ref context, new ReadOnlySpan<byte>(&charVal, 1));
    }

    private bool Produce<TContext>(
        ref TContext context,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)]
        string format,
        object? arg0)
        where TContext : ISeStringContext
    {
        var sb = this.StringBuilderPool.Get();
        sb.AppendFormat(format, arg0);

        Span<char> result = stackalloc char[sb.Length];
        var resultWriter = result;
        foreach (var chunk in sb.GetChunks())
        {
            chunk.Span.CopyTo(resultWriter);
            resultWriter = resultWriter[chunk.Length..];
        }

        context.ProduceString(result);
        this.StringBuilderPool.Return(sb);
        return true;
    }

    private string? ExpressionToString<TContext>(ref TContext context, SeExpressionReadOnlySpan expr)
        where TContext : ISeStringContext
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        string? str = null;
        unsafe
        {
            fixed (TContext* pcontext = &context)
            {
                var helper = new StringExpressionEvaluationHelper<TContext, nint>(
                    this,
                    pcontext,
                    static (
                            SeStringEvaluator _,
                            ref TContext _,
                            ReadOnlySpan<byte> evaluatedString,
                            nint arg) =>
                        *(string*)arg = Encoding.UTF8.GetString(evaluatedString),
                    (nint)(&str));
                if (!this.ResolveStringExpression(ref helper, expr))
                    return null;
            }
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

        return str;
    }

    private readonly struct LanguageSheet
    {
        public readonly ExcelSheet<Addon> Addon;
        public readonly ExcelSheet<Level> Level;

        private readonly SeStringEvaluator evaluator;
        private readonly ClientLanguage language;
        private readonly ConcurrentDictionary<string, RawExcelSheet?> sheets;

        public LanguageSheet(SeStringEvaluator evaluator, ClientLanguage language)
        {
            this.evaluator = evaluator;
            this.language = language;
            this.Addon = this.evaluator.dataManager.GetExcelSheet<Addon>(language) ??
                         throw new InvalidOperationException("Sheet missing");
            this.Level = this.evaluator.dataManager.GetExcelSheet<Level>(language) ??
                              throw new InvalidOperationException("Sheet missing");
            this.sheets = new();
        }

        public readonly RawExcelSheet? this[string name] =>
            this.sheets.GetOrAdd(
                name,
                static (key, arg) => arg.Excel.GetSheetRaw(key, arg.language.ToLumina()),
                (this.evaluator.dataManager.Excel, this.language));

        public bool ProduceFromSheet<TContext>(
            ref TContext context,
            string sheetName,
            uint rowId,
            int colIndex,
            ReadOnlySpan<byte> expressions)
            where TContext : ISeStringContext
        {
            var sheet = this[sheetName];

            if (sheet?.GetRow(rowId) is not { } row)
                return false;
            switch (row.ReadColumnRaw(colIndex))
            {
                case bool val:
                    return Produce(ref context, val ? "true"u8 : "false"u8, val ? "true" : "false");

                case LSeString val:
                {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#pragma warning disable SA1519
                    unsafe
                    {
                        fixed (byte* expr = expressions)
                        fixed (TContext* pcontext = &context)
                        {
                            var helper = new ParameterEvaluationHelper<TContext>(
                                this.evaluator,
                                pcontext,
                                expr,
                                expressions.Length);
                            return this.evaluator.ResolveString(ref helper, val.RawData);
                        }
                    }
#pragma warning restore SA1519
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                }

                case { } val:
                    return this.evaluator.Produce(ref context, "{0}", val);
                default:
                    return false;
            }
        }
    }
}
