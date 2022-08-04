using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Dalamud.Injector;

public class ArgumentParser
{
    private static readonly Regex WhitespaceRegex = new(@"\s+");

    private readonly HashSet<string> namedArgumentPrefixes = new() { "-" };

    private readonly Dictionary<Type, Func<string, object?>> valueParsers = new();

    public void AddValueParser<T>(Func<string, T?> parser)
    {
        this.valueParsers[typeof(T)] = x => parser(x);
    }

    public string GetHelpMessage<T>(string programName, int lineBreakWidth = 80, string newLine = "\n    ")
    {
        var verbs = typeof(T).GetFields()
                             .Select(x => (x, x.GetCustomAttribute<VerbAttribute>()))
                             .Where(x => x.Item2 != null)
                             .Select(x => (field: x.Item1, attr: x.Item2!))
                             .OrderBy(x => x.attr.Names.First())
                             .ToList();
        var namedArguments = typeof(T).GetFields()
                                      .Select(x => (x, x.GetCustomAttribute<NamedArgumentAttribute>()))
                                      .Where(x => x.Item2 != null)
                                      .Select(x => (field: x.Item1, attr: x.Item2!))
                                      .OrderBy(x => x.attr.Names.First())
                                      .ToList();
        var positionalArgument = typeof(T).GetFields()
                                          .Select(x => (x, x.GetCustomAttribute<PositionalArgumentAttribute>()))
                                          .Where(x => x.Item2 != null)
                                          .Select(x => (field: x.Item1, attr: x.Item2!))
                                          .SingleOrDefault();
        var passthroughArgument = typeof(T).GetFields()
                                           .Select(x => (x, x.GetCustomAttribute<PassthroughArgumentAttribute>()))
                                           .Where(x => x.Item2 != null)
                                           .Select(x => (field: x.Item1, attr: x.Item2!))
                                           .SingleOrDefault();

        StringBuilder sb = new();
        StringBuilder argstr = new();
        var linelen = programName.Length;
        sb.Append(programName);

        foreach (var arg in namedArguments)
        {
            argstr.Clear();
            if (!arg.attr.Required)
                argstr.Append('[');
            argstr.Append(arg.attr.Names.First());

            if (arg.field.FieldType != typeof(bool) || arg.attr.ImplicitValue is not true)
            {
                if (arg.attr.ImplicitValue != null)
                    argstr.Append('(');
                argstr.Append('=');
                if (arg.attr.ValuePlaceholder != null)
                    argstr.Append('<').Append(arg.attr.ValuePlaceholder).Append('>');
                else if (arg.attr.ImplicitValue != null)
                    argstr.Append(arg.attr.ImplicitValue);
                else
                    argstr.Append('<').Append(GetFieldItemType(arg.field).Name).Append('>');
                if (arg.attr.ImplicitValue != null)
                    argstr.Append(')');
            }

            if (!arg.attr.Required)
                argstr.Append(']');

            if (linelen + 1 + argstr.Length >= lineBreakWidth)
            {
                sb.Append(newLine).Append(argstr);
                linelen = newLine.Length + argstr.Length;
            }
            else
            {
                sb.Append(' ').Append(argstr);
                linelen += 1 + argstr.Length;
            }
        }

        if (verbs.Any())
        {
            argstr.Clear();
            argstr.Append("<verb>");
            if (linelen + 1 + argstr.Length >= lineBreakWidth)
            {
                sb.Append(newLine).Append(argstr);
                linelen = newLine.Length + argstr.Length;
            }
            else
            {
                sb.Append(' ').Append(argstr);
                linelen += 1 + argstr.Length;
            }
        }

        if (positionalArgument.attr != null)
        {
            for (var i = 1; i <= 3; i++)
            {
                argstr.Clear();
                if (i == 3)
                {
                    argstr.Append("...");
                }
                else
                {
                    argstr.Append(positionalArgument.attr.ValuePlaceholder ??
                                  GetFieldItemType(positionalArgument.field).Name).Append(i);
                }

                if (linelen + 1 + argstr.Length >= lineBreakWidth)
                {
                    sb.Append(newLine).Append(argstr);
                    linelen = newLine.Length + argstr.Length;
                }
                else
                {
                    sb.Append(' ').Append(argstr);
                    linelen += 1 + argstr.Length;
                }
            }
        }

        if (passthroughArgument.attr != null)
        {
            argstr.Clear();
            argstr.Append('[').Append(passthroughArgument.attr.BeginToken).Append(' ');

            for (var i = 1; i <= 3; i++)
            {
                argstr.Clear();
                if (i == 3)
                {
                    argstr.Append("...");
                }
                else
                {
                    argstr.Append(passthroughArgument.attr.ValuePlaceholder ??
                                  GetFieldItemType(passthroughArgument.field).Name).Append(i);
                }

                if (linelen + 1 + argstr.Length >= lineBreakWidth)
                {
                    sb.Append(newLine).Append(argstr);
                    linelen = newLine.Length + argstr.Length;
                }
                else
                {
                    sb.Append(' ').Append(argstr);
                    linelen += 1 + argstr.Length;
                }
            }

            if (linelen + 1 + argstr.Length >= lineBreakWidth)
            {
                sb.Append(newLine).Append(argstr);
                linelen = newLine.Length + argstr.Length;
            }
            else
            {
                sb.Append(' ').Append(argstr);
                linelen += 1 + argstr.Length;
            }
        }

        sb.AppendLine().AppendLine();

        if (namedArguments.Any())
        {
            sb.Append("Named arguments:").AppendLine();

            var maxNameLength = Math.Min(namedArguments.Max(x => x.attr.Names.First().Length) + 2,
                                         lineBreakWidth / 2);
            foreach (var arg in namedArguments)
            {
                sb.Append(arg.attr.Names.First())
                  .Append(' ', Math.Max(2, maxNameLength - arg.attr.Names.First().Length))
                  .Append(arg.attr.Summary)
                  .AppendLine();
            }

            foreach (var arg in namedArguments)
            {
                if (arg.attr.Names.Length < 2 && arg.attr.Help == null)
                    continue;

                sb.AppendLine()
                  .Append(arg.attr.Names.First())
                  .AppendLine();
                if (arg.attr.Names.Length > 1)
                {
                    sb.Append("Aliases: ");
                    for (var i = 1; i < arg.attr.Names.Length; i++)
                    {
                        if (i > 1)
                            sb.Append(", ");
                        sb.Append(arg.attr.Names[i]);
                    }

                    sb.AppendLine();
                }

                if (arg.attr.Help != null)
                {
                    sb.Append(arg.attr.Help)
                      .AppendLine();
                }
            }

            sb.AppendLine();
        }

        if (positionalArgument.attr != null)
        {
            sb.Append("Positional argument: ").Append(positionalArgument.attr.Name).AppendLine()
              .Append(positionalArgument.attr.Summary).AppendLine()
              .AppendLine();
            if (positionalArgument.attr.Help != null)
            {
                sb.Append(positionalArgument.attr.Help).AppendLine()
                  .AppendLine();
            }
        }

        if (passthroughArgument.attr != null)
        {
            sb.Append("Passthrough argument: ").Append(passthroughArgument.attr.Name).AppendLine()
              .Append(passthroughArgument.attr.Summary).AppendLine()
              .AppendLine();
            if (passthroughArgument.attr.Help != null)
            {
                sb.Append(passthroughArgument.attr.Help).AppendLine()
                  .AppendLine();
            }
        }

        if (verbs.Any())
        {
            sb.Append("Verbs:").AppendLine();

            var maxNameLength = Math.Min(verbs.Max(x => x.attr.Names.First().Length) + 2, lineBreakWidth / 2);
            foreach (var verb in verbs)
            {
                sb.Append(verb.attr.Names.First())
                  .Append(' ', Math.Max(2, maxNameLength - verb.attr.Names.First().Length))
                  .Append(verb.attr.Summary)
                  .AppendLine();
            }

            foreach (var verb in verbs)
            {
                if (verb.attr.Names.Length < 2 && verb.attr.Help == null)
                    continue;

                sb.AppendLine()
                  .Append(verb.attr.Names.First())
                  .AppendLine();
                if (verb.attr.Names.Length > 1)
                {
                    sb.Append("Aliases: ");
                    for (var i = 1; i < verb.attr.Names.Length; i++)
                    {
                        if (i > 1)
                            sb.Append(", ");
                        sb.Append(verb.attr.Names[i]);
                    }
                }

                if (verb.attr.Help != null)
                {
                    sb.Append(verb.attr.Help)
                      .AppendLine();
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public T Parse<T>(IList<string> args, int firstArgIndex = 1)
    {
        var res = (T)typeof(T).GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object?>())!;
        var i = firstArgIndex;

        var parsedValues = new Dictionary<(object, FieldInfo), (UntypedList values, Attribute attr)>();
        ParseInto(args, res, ref i,
                  parsedValues,
                  new List<(string name, object obj, FieldInfo field, NamedArgumentAttribute attr)>());

        foreach (var ((parentObject, field), (value, attr)) in parsedValues)
        {
            var attrName = string.Empty;
            int minCount = 0, maxCount = int.MaxValue;
            var required = false;
            if (attr is PositionalArgumentAttribute positionalArgument)
            {
                attrName = positionalArgument.Name;
                minCount = positionalArgument.MinCount;
                maxCount = positionalArgument.MaxCount;
                required = positionalArgument.Required;
            }
            else if (attr is PassthroughArgumentAttribute passthroughArgument)
            {
                attrName = passthroughArgument.Name;
                required = passthroughArgument.Required;
                // pass
            }
            else if (attr is NamedArgumentAttribute namedArgument)
            {
                attrName = namedArgument.Names.First();
                minCount = namedArgument.MinCount;
                maxCount = namedArgument.MaxCount;
                required = namedArgument.Required;
            }
            else if (attr is VerbAttribute verb)
            {
                // pass
            }

            if (value.Count == 0)
            {
                if (required)
                {
                    throw new InvalidArgumentException(
                        $"Argument \"{attrName}\" must be supplied.");
                }

                continue;
            }

            if (value.Count < minCount)
            {
                throw new InvalidArgumentException(
                    $"Need at least {minCount} values for argument \"{attrName}\".");
            }

            if (value.Count > maxCount)
            {
                throw new InvalidArgumentException(
                    $"Up to {maxCount} values are accepted for argument \"{attrName}\".");
            }

            SetValueTo(parentObject, field, value);
        }

        return res;
    }

    private void ParseInto(
        IList<string> args, object obj, ref int i,
        Dictionary<(object, FieldInfo), (UntypedList values, Attribute attr)> parsedValues,
        IEnumerable<(string name, object obj, FieldInfo field, NamedArgumentAttribute attr)>
            inheritedNamedArguments)
    {
        var allFields = obj.GetType().GetFields();
        var namedArguments = allFields
                             .Select(x => (field: x, attr: x.GetCustomAttribute<NamedArgumentAttribute>()))
                             .Where(x => x.attr != null)
                             .SelectMany(x => x.attr!.Names.Select(name => (name, obj, x.field, attr: x.attr!)))
                             .Concat(inheritedNamedArguments)
                             .ToList();
        var verbs = allFields
                    .Select(x => (field: x, attr: x.GetCustomAttribute<VerbAttribute>()))
                    .Where(x => x.attr != null)
                    .SelectMany(x => x.attr!.Names.Select(name => (name, x.field, attr: x.attr!)))
                    .ToList();
        var positionalArgument = allFields
                                 .Select(x => (field: x, attr: x.GetCustomAttribute<PositionalArgumentAttribute>()))
                                 .SingleOrDefault(x => x.attr != null);
        var passthroughArgument = allFields
                                  .Select(x => (field: x,
                                                   attr: x.GetCustomAttribute<PassthroughArgumentAttribute>()))
                                  .SingleOrDefault(x => x.attr != null);

        if (verbs.Any(x => namedArgumentPrefixes.Any(y => x.name.StartsWith(y))))
        {
            throw new InvalidArgumentSpecificationException(
                "Names of verbs cannot begin with a character that is used as prefixes for named arguments.");
        }

        if (namedArguments.Any(x => namedArgumentPrefixes.Any(y => !x.name.StartsWith(y))))
        {
            throw new InvalidArgumentSpecificationException(
                "Names of named arguments must begin with a character that is used as prefixes for named arguments.");
        }

        if (verbs.Any() && positionalArgument.attr is not null)
        {
            throw new InvalidArgumentSpecificationException(
                "A verb cannot accept both sub-verb and positional arguments.");
        }

        var namedArgumentMatcher = new ClosestNameMatcher();
        for (var j = 0; j < namedArguments.Count; j++)
        {
            namedArgumentMatcher.Add(j, namedArguments[j].name, namedArguments[j].attr.CaseSensitive,
                                     namedArguments[j].attr.AllowShortMatch);
            parsedValues[(obj, namedArguments[j].field)] = (
                                                               values: new UntypedList(
                                                                   GetFieldItemType(namedArguments[j].field)),
                                                               namedArguments[j].attr);
        }

        var verbMatcher = new ClosestNameMatcher();
        for (var j = 0; j < verbs.Count; j++)
        {
            verbMatcher.Add(j, verbs[j].name, verbs[j].attr.CaseSensitive, verbs[j].attr.AllowShortMatch);
            parsedValues[(obj, verbs[j].field)] = (
                                                      values: new UntypedList(GetFieldItemType(verbs[j].field)),
                                                      verbs[j].attr);
        }

        if (passthroughArgument.attr is not null)
        {
            parsedValues[(obj, passthroughArgument.field)] = (
                                                                 values: new UntypedList(
                                                                     GetFieldItemType(passthroughArgument.field)),
                                                                 passthroughArgument.attr);
        }

        if (positionalArgument.attr is not null)
        {
            parsedValues[(obj, positionalArgument.field)] = (
                                                                values: new UntypedList(
                                                                    GetFieldItemType(positionalArgument.field)),
                                                                positionalArgument.attr);
        }

        for (; i < args.Count; i++)
        {
            var arg = args[i];

            if (passthroughArgument.attr?.BeginToken == arg)
                break;

            if (!namedArgumentPrefixes.Any(x => arg.StartsWith(x)))
            {
                switch (verbMatcher.Match(arg, out var matchId))
                {
                    case ClosestMatchResult.Found:
                        var verb = verbs[matchId];
                        var value = parsedValues[(obj, verb.field)];
                        var verbObject = verb.field.FieldType
                                             .GetConstructor(Type.EmptyTypes)!
                                             .Invoke(null);

                        i++;
                        ParseInto(args, verbObject, ref i, parsedValues, namedArguments.Where(x => x.attr.Global));
                        value.values.Add(verbObject);
                        continue;

                    case ClosestMatchResult.NotFound:
                        if (!parsedValues.TryGetValue((obj, positionalArgument.field), out value))
                            throw new PositionalArgumentNotSupportedException(
                                "Positional arguments are not supported.");
                        try
                        {
                            value.values.Add(ParseValue(arg, value.values.ItemType));
                        }
                        catch (ArgumentException ae)
                        {
                            throw new InvalidArgumentException(
                                $"Failed to parse value supplied for \"{positionalArgument.attr!.Name}\" as \"{value.values.ItemType}\": {ae.Message}",
                                ae);
                        }

                        continue;

                    case ClosestMatchResult.Ambiguous:
                        throw new AmbiguousVerbException($"\"{arg}\" is an ambiguous verb.");

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                var equalSignIndex = arg.IndexOf('=');
                string? providedValue = null;
                if (equalSignIndex != -1)
                {
                    providedValue = arg[(equalSignIndex + 1)..];
                    arg = arg[..equalSignIndex];
                }

                switch (namedArgumentMatcher.Match(arg, out var matchId))
                {
                    case ClosestMatchResult.Found:
                        var namedArgument = namedArguments[matchId];
                        var value = parsedValues[(namedArgument.obj, namedArgument.field)];

                        if (namedArgument.attr.ImplicitValue == null)
                        {
                            var next = i + 1 < args.Count ? args[i + 1] : null;
                            if (next != null && !namedArgumentPrefixes.Any(x => next.StartsWith(x)))
                            {
                                providedValue = next;
                                i++;
                            }
                        }

                        if (providedValue == null)
                        {
                            if (namedArgument.attr.ImplicitValue == null)
                            {
                                throw new InvalidNamedArgumentException(
                                    $"Value must be supplied for \"{namedArgument.name}\".");
                            }
                            else
                            {
                                value.values.Add(namedArgument.attr.ImplicitValue);
                            }
                        }
                        else
                        {
                            try
                            {
                                value.values.Add(ParseValue(providedValue, value.values.ItemType));
                            }
                            catch (ArgumentException ae)
                            {
                                throw new InvalidNamedArgumentException(
                                    $"Failed to parse value supplied for \"{namedArgument.name}\" as \"{value.values.ItemType}\": {ae.Message}.",
                                    ae);
                            }
                        }

                        break;

                    case ClosestMatchResult.NotFound:
                        throw new InvalidNamedArgumentException($"\"{arg}\" is not a valid named argument.");

                    case ClosestMatchResult.Ambiguous:
                        throw new AmbiguousNamedArgumentException($"\"{arg}\" is an ambiguous named argument.");

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        if (++i < args.Count)
        {
            var passthroughList = parsedValues[(obj, passthroughArgument.field)];
            for (; i < args.Count; i++)
                passthroughList.values.Add(ParseValue(args[i], passthroughList.values.ItemType));
        }
    }

    private Type GetFieldItemType(FieldInfo field)
    {
        var type = field.FieldType;
        while (true)
        {
            var again = false;
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                if (iface.GetGenericTypeDefinition() == typeof(IList<>)
                    || iface.GetGenericTypeDefinition() == typeof(ISet<>)
                    || iface.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = iface.GetGenericArguments().First();
                    again = true;
                    break;
                }
            }

            if (!again)
                break;
        }

        return type;
    }

    private void SetValueTo(object obj, FieldInfo field, UntypedList valueSource)
    {
        object? newValue;
        var outerNullable = field.FieldType.IsGenericType &&
                            field.FieldType.GetGenericTypeDefinition() == typeof(Nullable<>);
        var outer = outerNullable ? field.FieldType.GetGenericArguments()[0] : field.FieldType;
        if (outer.IsArray)
        {
            newValue = valueSource.ListObject.GetType()
                                  .GetMethod("ToArray", Type.EmptyTypes)!
                                  .Invoke(valueSource.ListObject, null)!;
        }
        else
        {
            var listType = outer.GetInterfaces()
                                .SingleOrDefault(x =>
                                                     x.IsGenericType &&
                                                     x.GetGenericTypeDefinition()
                                                      .IsAssignableFrom(typeof(IList<>)) &&
                                                     outer.GetConstructor(Type.EmptyTypes) != null);
            if (listType != null)
            {
                newValue = outer.GetConstructor(new[]
                {
                    typeof(IEnumerable<>).MakeGenericType(valueSource.ItemType),
                })!.Invoke(new[] { valueSource.ListObject });
            }
            else
            {
                var setType = outer.GetInterfaces()
                                   .SingleOrDefault(x =>
                                                        x.IsGenericType &&
                                                        x.GetGenericTypeDefinition()
                                                         .IsAssignableFrom(typeof(ISet<>)) &&
                                                        outer.GetConstructor(Type.EmptyTypes) != null);
                if (setType != null)
                {
                    newValue = outer.GetConstructor(new[]
                    {
                        typeof(IEnumerable<>).MakeGenericType(valueSource.ItemType),
                    })!.Invoke(new[] { valueSource.ListObject });
                }
                else
                {
                    if (valueSource.Count >= 2)
                        throw new InvalidArgumentException();
                    newValue = valueSource[0];
                }
            }
        }

        if (outerNullable)
        {
            newValue = typeof(Nullable<>).MakeGenericType(valueSource.ItemType)
                                         .GetConstructor(new[] { valueSource.ItemType })!
                                         .Invoke(null, new[] { newValue })!;
        }

        field.SetValue(obj, newValue);
    }

    private object ParseValue(string arg, Type type)
    {
        try
        {
            if (valueParsers.TryGetValue(type, out var fn))
            {
                var value = fn(arg);
                if (value == default)
                    throw new InvalidArgumentException();
                return value;
            }

            if (type.IsEnum)
            {
                if (Enum.TryParse(type, arg, out var value))
                    return value!;
            }

            return Convert.ChangeType(arg, type);
        }
        catch (Exception e)
        {
            throw new ArgumentException(e.Message, e);
        }
    }

    private enum ClosestMatchResult
    {
        Found,
        NotFound,
        Ambiguous,
    }

    private class UntypedList
    {
        public Type ItemType { get; init; }
        public Type UntypedListType { get; init; }
        public object ListObject { get; init; }
        public MethodInfo UntypedListAdd { get; init; }
        public MethodInfo UntypedListAddRange { get; init; }
        public PropertyInfo UntypedListCount { get; init; }
        public PropertyInfo UntypedListIndexer { get; init; }

        public UntypedList(Type itemType)
        {
            this.ItemType = itemType;
            this.UntypedListType = typeof(List<>).MakeGenericType(itemType);
            this.ListObject = this.UntypedListType.GetConstructor(Type.EmptyTypes)!.Invoke(null);
            this.UntypedListAdd = this.UntypedListType.GetMethod("Add", new[] { itemType })!;
            this.UntypedListAddRange = this.UntypedListType.GetMethod("Add", new[] { itemType })!;
            this.UntypedListCount = this.UntypedListType.GetProperty("Count")!;
            this.UntypedListIndexer =
                this.UntypedListType.GetProperties().First(x => x.GetIndexParameters().Length == 1);
        }

        public void Add(object obj)
        {
            this.UntypedListAdd.Invoke(this.ListObject, new[] { obj });
        }

        public void AddRange(object obj)
        {
            this.UntypedListAddRange.Invoke(this.ListObject, new[] { obj });
        }

        public int Count => (int)this.UntypedListCount.GetValue(this.ListObject, null)!;

        public object this[int i] => this.UntypedListIndexer.GetValue(this.ListObject, new object?[] { i })!;
    }

    private class ClosestNameMatcher
    {
        private readonly List<(int id, string name, bool caseSensitive, bool allowShortMatch)> candidates = new();

        public void Add(int id, string name, bool caseSensitive, bool allowShortMatch)
        {
            candidates.Add((id, name, caseSensitive, allowShortMatch));
        }

        public ClosestMatchResult Match(string verb, out int matchId)
        {
            matchId = -1;
            var matches = this.candidates.ToList();
            for (var i = 0; i < verb.Length && matches.Any(); i++)
            {
                matches.RemoveAll(x =>
                {
                    if (x.name.Length < i)
                        return true;

                    if (x.caseSensitive)
                    {
                        if (x.name[i] != verb[i])
                            return true;
                    }
                    else
                    {
                        if (char.ToUpperInvariant(x.name[i]) != char.ToUpperInvariant(verb[i]))
                            return true;
                    }

                    return false;
                });
            }

            matches.RemoveAll(x => !x.allowShortMatch && x.name.Length != verb.Length);
            if (!matches.Any())
                return ClosestMatchResult.NotFound;

            var exactMatches = matches.Where(
                                          x => (x.caseSensitive && x.name == verb)
                                               || (!x.caseSensitive &&
                                                   string.Compare(x.name, verb,
                                                                  StringComparison.OrdinalIgnoreCase) ==
                                                   0))
                                      .ToList();
            if (exactMatches.Count == 1)
            {
                matches = exactMatches;
            }
            else if (exactMatches.Count >= 2)
            {
                exactMatches.RemoveAll(x => x.name != verb);
                if (exactMatches.Count >= 2)
                    return ClosestMatchResult.Ambiguous;

                matches = exactMatches;
            }

            var shortestVerbLength = matches.Select(x => x.name.Length).Min();
            matches.RemoveAll(x => x.name.Length != shortestVerbLength);
            if (!matches.Any())
                return ClosestMatchResult.NotFound;
            if (matches.Count >= 2)
                return ClosestMatchResult.Ambiguous;
            matchId = matches.First().id;
            return ClosestMatchResult.Found;
        }
    }

    public class NamedArgumentAttribute : Attribute
    {
        public readonly string[] Names;
        public readonly string? ValuePlaceholder;
        public readonly string? Summary;
        public readonly string? Help;
        public readonly bool CaseSensitive;
        public readonly bool AllowShortMatch;
        public readonly object? ImplicitValue;
        public readonly int MinCount;
        public readonly int MaxCount;
        public readonly bool Global;
        public readonly bool Required;

        public NamedArgumentAttribute(
            string names,
            string? valuePlaceholder = null,
            string? summary = null,
            string? help = null,
            bool caseSensitive = false,
            bool allowShortMatch = true,
            object? implicitValue = null,
            int minCount = 1,
            int maxCount = 1,
            bool global = false,
            bool required = false)
        {
            this.Names = WhitespaceRegex.Split(names);
            this.ValuePlaceholder = valuePlaceholder;
            this.Summary = summary;
            this.Help = help;
            this.CaseSensitive = caseSensitive;
            this.AllowShortMatch = allowShortMatch;
            this.ImplicitValue = implicitValue;
            this.MinCount = minCount;
            this.MaxCount = maxCount;
            this.Global = global;
            this.Required = required;
        }
    }

    public class PositionalArgumentAttribute : Attribute
    {
        public readonly string Name;
        public readonly string? ValuePlaceholder;
        public readonly string? Summary;
        public readonly string? Help;
        public readonly int MinCount;
        public readonly int MaxCount;
        public readonly bool Required;

        public PositionalArgumentAttribute(
            string name,
            string? valuePlaceholder = null,
            string? summary = null,
            string? help = null,
            int minCount = 0,
            int maxCount = int.MaxValue,
            bool required = false)
        {
            this.Name = name;
            this.ValuePlaceholder = valuePlaceholder;
            this.Summary = summary;
            this.Help = help;
            this.MinCount = minCount;
            this.MaxCount = maxCount;
            this.Required = required;
        }
    }

    public class PassthroughArgumentAttribute : Attribute
    {
        public readonly string Name;
        public readonly string BeginToken;
        public readonly string? ValuePlaceholder;
        public readonly string? Summary;
        public readonly string? Help;
        public readonly bool Required;

        public PassthroughArgumentAttribute(
            string name,
            string beginToken,
            string? valuePlaceholder = null,
            string? summary = null,
            string? help = null,
            bool required = false)
        {
            this.Name = name;
            this.BeginToken = beginToken;
            this.ValuePlaceholder = valuePlaceholder;
            this.Summary = summary;
            this.Help = help;
            this.Required = required;
        }
    }

    public class VerbAttribute : Attribute
    {
        public readonly string[] Names;
        public readonly string? Summary;
        public readonly string? Help;
        public readonly bool CaseSensitive;
        public readonly bool AllowShortMatch;

        public VerbAttribute(
            string names,
            string? summary = null,
            string? help = null,
            bool caseSensitive = false,
            bool allowShortMatch = true)
        {
            this.Names = WhitespaceRegex.Split(names);
            this.Summary = summary;
            this.Help = help;
            this.CaseSensitive = caseSensitive;
            this.AllowShortMatch = allowShortMatch;
        }
    }

    public class InvalidArgumentException : Exception
    {
        public InvalidArgumentException() { }

        public InvalidArgumentException(string message)
            : base(message) { }

        public InvalidArgumentException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class InvalidArgumentSpecificationException : InvalidArgumentException
    {
        public InvalidArgumentSpecificationException() { }

        public InvalidArgumentSpecificationException(string message)
            : base(message) { }

        public InvalidArgumentSpecificationException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class AmbiguousVerbException : InvalidArgumentException
    {
        public AmbiguousVerbException() { }

        public AmbiguousVerbException(string message)
            : base(message) { }

        public AmbiguousVerbException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class PositionalArgumentNotSupportedException : InvalidArgumentException
    {
        public PositionalArgumentNotSupportedException() { }

        public PositionalArgumentNotSupportedException(string message)
            : base(message) { }

        public PositionalArgumentNotSupportedException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class InvalidNamedArgumentException : InvalidArgumentException
    {
        public InvalidNamedArgumentException() { }

        public InvalidNamedArgumentException(string message)
            : base(message) { }

        public InvalidNamedArgumentException(string message, Exception inner)
            : base(message, inner) { }
    }

    public class AmbiguousNamedArgumentException : InvalidArgumentException
    {
        public AmbiguousNamedArgumentException() { }

        public AmbiguousNamedArgumentException(string message)
            : base(message) { }

        public AmbiguousNamedArgumentException(string message, Exception inner)
            : base(message, inner) { }
    }
}
