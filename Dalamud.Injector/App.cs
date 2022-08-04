using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Newtonsoft.Json;
using Reloaded.Memory.Buffers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using static Dalamud.Injector.NativeFunctions;

namespace Dalamud.Injector;

/// <summary>
/// Entrypoint to the program.
/// </summary>
public sealed class App
{
    private readonly string programName;
    private readonly InjectorArguments args;
    private readonly DalamudStartInfo dalamudStartInfo;

    public App(string programName, InjectorArguments args, DalamudStartInfo? templateStartInfo)
    {
        this.programName = programName;
        this.args = args;

        this.InitLogging();
        this.InitUnhandledException();

        var cwd = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
        if (cwd.FullName != Directory.GetCurrentDirectory())
        {
            Log.Debug($"Changing cwd to {cwd}");
            Directory.SetCurrentDirectory(cwd.FullName);
        }

        this.dalamudStartInfo = this.ExtractAndInitializeStartInfoFromArguments(templateStartInfo);
    }

    private static string GetLogPath(string filename)
    {
        var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

#if DEBUG
        var logPath = Path.Combine(baseDirectory, $"{filename}.log");
#else
            var logPath = Path.Combine(baseDirectory, "..", "..", "..", $"{filename}.log");
#endif

        return logPath;
    }

    private static void CullLogFile(string logPath, int cullingFileSize)
    {
        try
        {
            var bufferSize = 4096;

            var logFile = new FileInfo(logPath);

            if (!logFile.Exists)
                logFile.Create();

            if (logFile.Length <= cullingFileSize)
                return;

            var amountToCull = logFile.Length - cullingFileSize;

            if (amountToCull < bufferSize)
                return;

            using var reader = new BinaryReader(logFile.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            using var writer = new BinaryWriter(logFile.Open(FileMode.Open, FileAccess.Write, FileShare.ReadWrite));

            reader.BaseStream.Seek(amountToCull, SeekOrigin.Begin);

            var read = -1;
            var total = 0;
            var buffer = new byte[bufferSize];
            while (read != 0)
            {
                read = reader.Read(buffer, 0, buffer.Length);
                writer.Write(buffer, 0, read);
                total += read;
            }

            writer.BaseStream.SetLength(total);
        }
        catch (Exception)
        {
            /*
            var caption = "XIVLauncher Error";
            var message = $"Log cull threw an exception: {ex.Message}\n{ex.StackTrace ?? string.Empty}";
            _ = MessageBoxW(IntPtr.Zero, message, caption, MessageBoxType.IconError | MessageBoxType.Ok);
            */
        }
    }

    private static Process GetInheritableCurrentProcessHandle()
    {
        if (!DuplicateHandle(
                Process.GetCurrentProcess().Handle,
                Process.GetCurrentProcess().Handle,
                Process.GetCurrentProcess().Handle,
                out var inheritableCurrentProcessHandle,
                0,
                true,
                DuplicateOptions.SameAccess))
        {
            Log.Error("Failed to call DuplicateHandle: Win32 error code {0}", Marshal.GetLastWin32Error());
            return null;
        }

        return new ExistingProcess(inheritableCurrentProcessHandle);
    }

    private static DalamudStartInfo AdjustStartInfo(DalamudStartInfo startInfo, string gamePath)
    {
        var ffxivDir = Path.GetDirectoryName(gamePath);
        var gameVerStr = File.ReadAllText(Path.Combine(ffxivDir, "ffxivgame.ver"));
        var gameVer = GameVersion.Parse(gameVerStr);

        return new DalamudStartInfo(startInfo)
        {
            GameVersion = gameVer,
        };
    }

    private static void Inject(Process process, DalamudStartInfo startInfo, bool tryFixAcl = false)
    {
        if (tryFixAcl)
        {
            try
            {
                GameStart.CopyAclFromSelfToTargetProcess(process.SafeHandle.DangerousGetHandle());
            }
            catch (Win32Exception e1)
            {
                Log.Warning(e1, "Failed to copy ACL");
            }
        }

        var bootName = "Dalamud.Boot.dll";
        var bootPath = Path.GetFullPath(bootName);

        // ======================================================

        using var injector = new Injector(process, false);

        injector.LoadLibrary(bootPath, out var bootModule);

        // ======================================================

        var startInfoJson = JsonConvert.SerializeObject(startInfo);
        var startInfoBytes = Encoding.UTF8.GetBytes(startInfoJson);

        using var startInfoBuffer =
            new MemoryBufferHelper(process).CreatePrivateMemoryBuffer(startInfoBytes.Length + 0x8);
        var startInfoAddress = startInfoBuffer.Add(startInfoBytes);

        if (startInfoAddress == 0)
            throw new Exception("Unable to allocate start info JSON");

        injector.GetFunctionAddress(bootModule, "Initialize", out var initAddress);
        injector.CallRemoteFunction(initAddress, startInfoAddress, out var exitCode);

        // ======================================================

        if (exitCode > 0)
        {
            Log.Error($"Dalamud.Boot::Initialize returned {exitCode}");
            return;
        }

        Log.Information("Done");
    }

    [DllImport("Dalamud.Boot.dll")]
    private static extern int RewriteRemoteEntryPointW(
        IntPtr hProcess,
        [MarshalAs(UnmanagedType.LPWStr)] string gamePath,
        [MarshalAs(UnmanagedType.LPWStr)] string loadInfoJson);

    /// <summary>
    ///     This routine appends the given argument to a command line such that
    ///     CommandLineToArgvW will return the argument string unchanged. Arguments
    ///     in a command line should be separated by spaces; this function does
    ///     not add these spaces.
    ///
    ///     Taken from https://stackoverflow.com/questions/5510343/escape-command-line-arguments-in-c-sharp
    ///     and https://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/.
    /// </summary>
    /// <param name="argument">Supplies the argument to encode.</param>
    /// <param name="force">
    ///     Supplies an indication of whether we should quote the argument even if it
    ///     does not contain any characters that would ordinarily require quoting.
    /// </param>
    private static string EncodeParameterArgument(string argument, bool force = false)
    {
        if (argument == null) throw new ArgumentNullException(nameof(argument));

        // Unless we're told otherwise, don't quote unless we actually
        // need to do so --- hopefully avoid problems if programs won't
        // parse quotes properly
        if (force == false
            && argument.Length > 0
            && argument.IndexOfAny(" \t\n\v\"".ToCharArray()) == -1)
        {
            return argument;
        }

        var quoted = new StringBuilder();
        quoted.Append('"');

        var numberBackslashes = 0;

        foreach (var chr in argument)
        {
            switch (chr)
            {
                case '\\':
                    numberBackslashes++;
                    continue;
                case '"':
                    // Escape all backslashes and the following
                    // double quotation mark.
                    quoted.Append('\\', (numberBackslashes * 2) + 1);
                    quoted.Append(chr);
                    break;
                default:
                    // Backslashes aren't special here.
                    quoted.Append('\\', numberBackslashes);
                    quoted.Append(chr);
                    break;
            }

            numberBackslashes = 0;
        }

        // Escape all backslashes, but let the terminating
        // double quotation mark we add below be interpreted
        // as a metacharacter.
        quoted.Append('\\', numberBackslashes * 2);
        quoted.Append('"');

        return quoted.ToString();
    }

    public int Run()
    {
        if (this.args.Inject != null)
            return this.ProcessInjectCommand(this.args.Inject);
        if (this.args.Launch != null)
            return this.ProcessLaunchCommand(this.args.Launch);
        if (this.args.Help)
        {
            Console.WriteLine(new ArgumentParser().GetHelpMessage<InjectorArguments>(this.programName));
            return 0;
        }

        Console.WriteLine("No command specified. Type -h for help.");
        return -1;
    }

    private void InitUnhandledException()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
        {
            var exObj = eventArgs.ExceptionObject;

            if (exObj is ArgumentParser.InvalidArgumentException clex)
            {
                Console.WriteLine();
                Console.WriteLine("Command line error: {0}", clex.Message);
                Console.WriteLine();
                Console.WriteLine(new ArgumentParser().GetHelpMessage<InjectorArguments>(this.programName));
            }
            else if (Log.Logger == null)
            {
                Console.WriteLine($"A fatal error has occurred: {eventArgs.ExceptionObject}");
            }
            else if (exObj is Exception ex)
            {
                Log.Error(ex, "A fatal error has occurred");
            }
            else
            {
                Log.Error("A fatal error has occurred: {Exception}", eventArgs.ExceptionObject.ToString());
            }

            Environment.Exit(-1);
        };
    }

    private void InitLogging()
    {
#if DEBUG
        var verbose = true;
#else
            var verbose = this.args.VerboseLogging;
#endif

        var levelSwitch = new LoggingLevelSwitch
        {
            MinimumLevel = verbose ? LogEventLevel.Verbose : LogEventLevel.Information,
        };

        var logPath = GetLogPath("dalamud.injector");

        CullLogFile(logPath, 1 * 1024 * 1024);

        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Verbose)
                     .WriteTo.Async(a => a.File(logPath))
                     .MinimumLevel.ControlledBy(levelSwitch)
                     .CreateLogger();
    }

    private DalamudStartInfo ExtractAndInitializeStartInfoFromArguments(DalamudStartInfo? startInfo)
    {
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var xivlauncherDir = Path.Combine(appDataDir, "XIVLauncher");

        startInfo ??= new DalamudStartInfo();
        startInfo.WorkingDirectory = this.args.DalamudWorkingDirectory ??
                                     startInfo.WorkingDirectory ?? Directory.GetCurrentDirectory();
        startInfo.ConfigurationPath = this.args.DalamudConfigurationPath ??
                                      startInfo.ConfigurationPath ??
                                      Path.Combine(xivlauncherDir, "dalamudConfig.json");
        startInfo.PluginDirectory = this.args.DalamudPluginDirectory ??
                                    startInfo.PluginDirectory ?? Path.Combine(xivlauncherDir, "installedPlugins");
        startInfo.DefaultPluginDirectory = this.args.DalamudDevPluginDirectory ??
                                           startInfo.DefaultPluginDirectory ??
                                           Path.Combine(xivlauncherDir, "devPlugins");
        startInfo.AssetDirectory = this.args.DalamudAssetDirectory ??
                                   startInfo.AssetDirectory ?? Path.Combine(xivlauncherDir, "dalamudAssets", "dev");
        startInfo.DelayInitializeMs =
            this.args.DalamudDelayInitialize.GetValueOrDefault(startInfo.DelayInitializeMs);
        startInfo.Language = this.args.DalamudClientLanguage.GetValueOrDefault(startInfo.Language);

        startInfo.GameVersion = null;

        // Set boot defaults
        startInfo.BootShowConsole = this.args.BootShowConsole;
        startInfo.BootEnableEtw = this.args.BootEnableEtw;
        startInfo.BootLogPath = GetLogPath("dalamud.boot");
        startInfo.BootEnabledGameFixes = new List<string>
        {
            "prevent_devicechange_crashes",
            "disable_game_openprocess_access_check",
            "redirect_openprocess",
            "backup_userdata_save",
            "clr_failfast_hijack",
        };
        startInfo.BootDotnetOpenProcessHookMode = 0;
        startInfo.BootWaitMessageBox |= this.args.BootShowMsgbox1 ? 1 : 0;
        startInfo.BootWaitMessageBox |= this.args.BootShowMsgbox2 ? 2 : 0;
        startInfo.BootWaitMessageBox |= this.args.BootShowMsgbox3 ? 4 : 0;
        startInfo.BootVehEnabled = this.args.BootEnableVeh; // true by default
        startInfo.BootVehFull = this.args.BootEnableVehFull;
        startInfo.NoLoadPlugins = this.args.NoPlugin;
        startInfo.NoLoadThirdPartyPlugins = this.args.NoThirdPartyPlugin;
        // startInfo.BootUnhookDlls = new List<string>() { "kernel32.dll", "ntdll.dll", "user32.dll" };

        return startInfo;
    }

    private int ProcessInjectCommand(InjectorArguments.InjectVerbArguments injectorArgs)
    {
        List<Process> processes = new();

        if (injectorArgs.Help)
        {
            Console.WriteLine(
                new ArgumentParser().GetHelpMessage<InjectorArguments.InjectVerbArguments>(
                    this.programName + " inject"));
            return 0;
        }

        if (injectorArgs.InjectToAll)
        {
            processes.AddRange(Process.GetProcessesByName("ffxiv_dx11"));
        }

        if (!injectorArgs.Pids.Any() && !injectorArgs.InjectToAll)
        {
            throw new ArgumentParser.InvalidArgumentException(
                "No target process has been specified. Use -a(--all) option to inject to all ffxiv_dx11.exe processes.");
        }

        processes.AddRange(injectorArgs.Pids);

        if (!processes.Any())
        {
            Log.Error("No suitable target process has been found.");
            return -1;
        }

        if (injectorArgs.Warn)
        {
            var result = MessageBoxW(
                IntPtr.Zero,
                $"Take care: you are manually injecting Dalamud into FFXIV({string.Join(", ", processes.Select(x => $"{x.Id}"))}).\n\nIf you are doing this to use plugins before they are officially whitelisted on patch days, things may go wrong and you may get into trouble.\nWe discourage you from doing this and you won't be warned again in-game.",
                "Dalamud",
                MessageBoxType.IconWarning | MessageBoxType.OkCancel);

            // IDCANCEL
            if (result == 2)
            {
                Log.Information("User cancelled injection");
                return -2;
            }
        }

        if (injectorArgs.ObtainSeDebugPrivilege)
        {
            try
            {
                GameStart.ClaimSeDebug();
                Log.Information("SeDebugPrivilege claimed.");
            }
            catch (Win32Exception e2)
            {
                Log.Warning(e2, "Failed to claim SeDebugPrivilege");
            }
        }

        foreach (var process in processes)
            Inject(process, AdjustStartInfo(this.dalamudStartInfo, process.MainModule!.FileName), injectorArgs.FixAcl);

        return 0;
    }

    private int ProcessLaunchCommand(InjectorArguments.LaunchVerbArguments launchArgs)
    {
        if (launchArgs.Help)
        {
            Console.WriteLine(
                new ArgumentParser().GetHelpMessage<InjectorArguments.LaunchVerbArguments>(
                    this.programName + " launch"));
            return 0;
        }

        var encryptArguments = false;
        var checksumTable = "fX1pGtdS5CAP4_VL";
        var argDelimiterRegex = new Regex(" (?<!(?:^|[^ ])(?:  )*)/");
        var kvDelimiterRegex = new Regex(" (?<!(?:^|[^ ])(?:  )*)=");
        var gameArguments = launchArgs.GameArguments.SelectMany(x =>
        {
            if (!x.StartsWith("//**sqex0003") || !x.EndsWith("**//"))
                return new List<string>() { x };

            var checksum = checksumTable.IndexOf(x[x.Length - 5]);
            if (checksum == -1)
                return new List<string>() { x };

            var encData = Convert.FromBase64String(x.Substring(12, x.Length - 12 - 5).Replace('-', '+')
                                                    .Replace('_', '/').Replace('*', '='));
            var rawData = new byte[encData.Length];

            for (var i = (uint)checksum; i < 0x10000u; i += 0x10)
            {
                var bf = new LegacyBlowfish(Encoding.UTF8.GetBytes($"{i << 16:x08}"));
                Buffer.BlockCopy(encData, 0, rawData, 0, rawData.Length);
                bf.Decrypt(ref rawData);
                var rawString = Encoding.UTF8.GetString(rawData).Split('\0', 2).First();
                encryptArguments = true;
                var args = argDelimiterRegex.Split(rawString).Skip(1)
                                            .Select(y => string.Join('=', kvDelimiterRegex.Split(y, 2))
                                                               .Replace("  ", " ")).ToList();
                if (!args.Any())
                    continue;
                if (!args.First().StartsWith("T="))
                    continue;
                if (!uint.TryParse(args.First().Substring(2), out var tickCount))
                    continue;
                if (tickCount >> 16 != i)
                    continue;
                return args.Skip(1);
            }

            return new List<string>() { x };
        }).ToList();

        if (launchArgs.GamePath == null)
        {
            try
            {
                var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var xivlauncherDir = Path.Combine(appDataDir, "XIVLauncher");
                var launcherConfigPath = Path.Combine(xivlauncherDir, "launcherConfigV3.json");
                launchArgs.GamePath = Path.Combine(
                    JsonSerializer.CreateDefault()
                                  .Deserialize<Dictionary<string, string>>(
                                      new JsonTextReader(new StringReader(
                                                             File.ReadAllText(launcherConfigPath))))!["GamePath"],
                    "game",
                    "ffxiv_dx11.exe");
                Log.Information(
                    "Using game installation path configuration from from XIVLauncher: {0}",
                    launchArgs.GamePath);
            }
            catch (Exception)
            {
                Log.Error(
                    "Failed to read launcherConfigV3.json to get the set-up game path, please specify one using -g");
                return -1;
            }

            if (!File.Exists(launchArgs.GamePath))
            {
                Log.Error("File not found: {0}", launchArgs.GamePath);
                return -1;
            }
        }

        if (launchArgs.FakeArguments)
        {
            var gameVersion =
                File.ReadAllText(Path.Combine(Directory.GetParent(launchArgs.GamePath)!.FullName, "ffxivgame.ver"));
            var sqpackPath = Path.Combine(Directory.GetParent(launchArgs.GamePath)!.FullName, "sqpack");
            var maxEntitledExpansionId = 0;
            while (File.Exists(Path.Combine(
                                   sqpackPath,
                                   $"ex{maxEntitledExpansionId + 1}",
                                   $"ex{maxEntitledExpansionId + 1}.ver")))
                maxEntitledExpansionId++;

            gameArguments.InsertRange(0, new[]
            {
                "DEV.TestSID=0",
                "DEV.UseSqPack=1",
                "DEV.DataPathType=1",
                "DEV.LobbyHost01=127.0.0.1",
                "DEV.LobbyPort01=54994",
                "DEV.LobbyHost02=127.0.0.2",
                "DEV.LobbyPort02=54994",
                "DEV.LobbyHost03=127.0.0.3",
                "DEV.LobbyPort03=54994",
                "DEV.LobbyHost04=127.0.0.4",
                "DEV.LobbyPort04=54994",
                "DEV.LobbyHost05=127.0.0.5",
                "DEV.LobbyPort05=54994",
                "DEV.LobbyHost06=127.0.0.6",
                "DEV.LobbyPort06=54994",
                "DEV.LobbyHost07=127.0.0.7",
                "DEV.LobbyPort07=54994",
                "DEV.LobbyHost08=127.0.0.8",
                "DEV.LobbyPort08=54994",
                "DEV.LobbyHost09=127.0.0.9",
                "DEV.LobbyPort09=54994",
                "DEV.LobbyHost10=127.0.0.10",
                "DEV.LobbyPort10=54994",
                "SYS.Region=0",
                $"language={(int)this.dalamudStartInfo.Language}",
                $"ver={gameVersion}",
                $"DEV.MaxEntitledExpansionID={maxEntitledExpansionId}",
                "DEV.GMServerHost=127.0.0.100",
                "DEV.GameQuitMessageBox=0",
            });
        }

        string gameArgumentString;
        if (encryptArguments)
        {
            var rawTickCount = (uint)Environment.TickCount;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                [System.Runtime.InteropServices.DllImport("c")]
                static extern ulong clock_gettime_nsec_np(int clock_id);

                const int CLOCK_MONOTONIC_RAW = 4;
                var rawTickCountFixed = clock_gettime_nsec_np(CLOCK_MONOTONIC_RAW) / 1000000;
                Log.Information(
                    "ArgumentBuilder::DeriveKey() fixing up rawTickCount from {0} to {1} on macOS",
                    rawTickCount,
                    rawTickCountFixed);
                rawTickCount = (uint)rawTickCountFixed;
            }

            var ticks = rawTickCount & 0xFFFF_FFFFu;
            var key = ticks & 0xFFFF_0000u;
            gameArguments.Insert(0, $"T={ticks}");

            var escapeValue = (string x) => x.Replace(" ", "  ");
            gameArgumentString = gameArguments.Select(x => x.Split('=', 2))
                                              .Aggregate(
                                                  new StringBuilder(),
                                                  (whole, part) =>
                                                      whole.Append(
                                                          $" /{escapeValue(part[0])} ={escapeValue(part.Length > 1 ? part[1] : string.Empty)}"))
                                              .ToString();
            var bf = new LegacyBlowfish(Encoding.UTF8.GetBytes($"{key:x08}"));
            var ciphertext = bf.Encrypt(Encoding.UTF8.GetBytes(gameArgumentString));
            var base64Str = Convert.ToBase64String(ciphertext).Replace('+', '-').Replace('/', '_')
                                   .Replace('=', '*');
            var checksum = checksumTable[(int)(key >> 16) & 0xF];
            gameArgumentString = $"//**sqex0003{base64Str}{checksum}**//";
        }
        else
        {
            gameArgumentString = string.Join(" ", gameArguments.Select(x => EncodeParameterArgument(x)));
        }

        var process = GameStart.LaunchGame(
            Path.GetDirectoryName(launchArgs.GamePath)!,
            launchArgs.GamePath,
            gameArgumentString,
            launchArgs.NoFixAcl,
            p =>
            {
                if (!launchArgs.WithoutDalamud && launchArgs.InjectMode == InjectorArguments.InjectMode.Entrypoint)
                {
                    var startInfo = AdjustStartInfo(this.dalamudStartInfo, launchArgs.GamePath);
                    Log.Information("Using start info: {0}", JsonConvert.SerializeObject(startInfo));
                    if (RewriteRemoteEntryPointW(
                            p.Handle,
                            launchArgs.GamePath,
                            JsonConvert.SerializeObject(startInfo)) != 0)
                    {
                        Log.Error("[HOOKS] RewriteRemoteEntryPointW failed");
                        throw new Exception("RewriteRemoteEntryPointW failed");
                    }

                    Log.Verbose("RewriteRemoteEntryPointW called!");
                }
            },
            !launchArgs.NoWait);

        Log.Verbose("Game process started with PID {0}", process.Id);

        if (!launchArgs.WithoutDalamud && launchArgs.InjectMode == InjectorArguments.InjectMode.Inject)
        {
            var startInfo = AdjustStartInfo(this.dalamudStartInfo, launchArgs.GamePath);
            Log.Information("Using start info: {0}", JsonConvert.SerializeObject(startInfo));
            Inject(process, startInfo, !launchArgs.NoFixAcl);
        }

        var processHandleForOwner = IntPtr.Zero;
        if (launchArgs.HandleOwner != IntPtr.Zero)
        {
            if (!DuplicateHandle(
                    Process.GetCurrentProcess().Handle,
                    process.Handle,
                    launchArgs.HandleOwner,
                    out processHandleForOwner,
                    0,
                    false,
                    DuplicateOptions.SameAccess))
            {
                Log.Warning("Failed to call DuplicateHandle: Win32 error code {0}", Marshal.GetLastWin32Error());
            }
        }

        Console.WriteLine($"{{\"pid\": {process.Id}, \"handle\": {processHandleForOwner}}}");

        return 0;
    }
}
