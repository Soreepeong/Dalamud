using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace Dalamud.Injector;

public sealed class EntryPoint
{
    /// <summary>
    /// A delegate used during initialization of the CLR from Dalamud.Injector.Boot.
    /// </summary>
    /// <param name="argc">Count of arguments.</param>
    /// <param name="argvPtr">char** string arguments.</param>
    public delegate void MainDelegate(int argc, IntPtr argvPtr);

    /// <summary>
    /// Start the Dalamud injector.
    /// </summary>
    /// <param name="argc">Count of arguments.</param>
    /// <param name="argvPtr">byte** string arguments.</param>
    public static void Main(int argc, IntPtr argvPtr)
    {
        List<string> rawArgs = Enumerable.Range(0, argc)
                                         .Select(i => Marshal.PtrToStringUni(
                                                     Marshal.ReadIntPtr(argvPtr, i * IntPtr.Size))!)
                                         .ToList();

        DalamudStartInfo? templateStartInfo = null;
        if (rawArgs.Count == 1)
        {
            rawArgs.Add("inject");
            rawArgs.Add("--all");

#if !DEBUG
            rawArgs.Add("--warn");
#endif
        }
        else if (int.TryParse(rawArgs[1], out _))
        {
            // Assume that PID has been passed.
            rawArgs.Insert(1, "inject");

            // If originally second parameter exists, then assume that it's a base64 encoded start info.
            // Dalamud.Injector.exe inject [pid] [base64]
            if (rawArgs.Count == 4)
            {
                templateStartInfo = JsonConvert.DeserializeObject<DalamudStartInfo>(
                    Encoding.UTF8.GetString(Convert.FromBase64String(rawArgs[3])));
                rawArgs.RemoveAt(3);
            }
        }

        InjectorArguments args;
        try
        {
            var parser = new ArgumentParser();
            parser.AddValueParser(value => value.ToLowerInvariant() switch
            {
                _ when CompareShortMatch(value, "english") => ClientLanguage.English,
                _ when CompareShortMatch(value, "japanese", "日本語") => ClientLanguage.Japanese,
                _ when CompareShortMatch(value, "german", "deutsche") => ClientLanguage.German,
                _ when CompareShortMatch(value, "french", "français") => ClientLanguage.French,
                _ when int.TryParse(value, out var x) && Enum.IsDefined((ClientLanguage)x) => (ClientLanguage)x,
                _ => throw new ArgumentException($"\"{value}\" is not a valid language."),
            });
            parser.AddValueParser(value => value.ToLowerInvariant() switch
            {
                _ when CompareShortMatch(value, "entrypoint") => InjectorArguments.InjectMode.Entrypoint,
                _ when CompareShortMatch(value, "inject") => InjectorArguments.InjectMode.Inject,
                _ when Enum.TryParse(typeof(InjectorArguments.InjectMode), value, out var parsed) =>
                    (InjectorArguments.InjectMode)parsed!,
                _ => throw new ArgumentException($"\"{value}\" is not a valid inject mode."),
            });
            parser.AddValueParser(value =>
            {
                if (long.TryParse(value, out var r))
                    return (IntPtr)r;
                throw new ArgumentException($"\"{value}\" is not a valid handle.");
            });
            parser.AddValueParser(value =>
            {
                if (int.TryParse(value, out var r))
                    return Process.GetProcessById(r);
                throw new ArgumentException($"PID \"{value}\" is not a valid process.");
            });
            args = parser.Parse<InjectorArguments>(rawArgs);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Environment.Exit(-1);
            return;
        }

        Environment.Exit(new App(rawArgs[0], args, templateStartInfo).Run());
    }

    private static bool CompareShortMatch(string input, params string[] values)
    {
        // Compare input to a substring of value, the same length as input
        // i.e. "eng" == "english"[..3]
        return values.Any(value => input == value[..Math.Min(value.Length, input.Length)]);
    }
}
