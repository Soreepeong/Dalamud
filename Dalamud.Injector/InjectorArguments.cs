using System;
using System.Collections.Generic;
using System.Diagnostics;


#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1602

namespace Dalamud.Injector;

public class InjectorArguments
{
    [ArgumentParser.NamedArgumentAttribute(
        "-h --help",
        implicitValue: true,
        summary: "display help message")]
    public bool Help = false;

    [ArgumentParser.VerbAttribute(
        "inject",
        summary: "inject Dalamud into game")]
    public InjectVerbArguments? Inject;

    [ArgumentParser.VerbAttribute(
        "launch",
        summary: "launch game with Dalamud")]
    public LaunchVerbArguments? Launch;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-working-directory",
        global: true,
        summary: "specify Dalamud working directory",
        valuePlaceholder: "path")]
    public string? DalamudWorkingDirectory = string.Empty;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-configuration-path",
        global: true,
        summary: "specify Dalamud configuration path",
        valuePlaceholder: "path")]
    public string? DalamudConfigurationPath = string.Empty;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-plugin-directory",
        global: true,
        summary: "specify Dalamud plugin directory",
        valuePlaceholder: "path")]
    public string? DalamudPluginDirectory = string.Empty;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-dev-plugin-directory",
        global: true,
        summary: "specify Dalamud dev plugin directory",
        valuePlaceholder: "path")]
    public string? DalamudDevPluginDirectory = string.Empty;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-asset-directory",
        global: true,
        summary: "specify Dalamud asset directory",
        valuePlaceholder: "path")]
    public string? DalamudAssetDirectory = string.Empty;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-delay-initialize",
        global: true,
        summary: "specify time to wait in milliseconds before loading Dalamud",
        valuePlaceholder: "duration in ms")]
    public int? DalamudDelayInitialize = null;

    [ArgumentParser.NamedArgumentAttribute(
        "--dalamud-client-language",
        global: true,
        summary: "specify game client language to pass to Dalamud",
        valuePlaceholder: "j(apanese)/e(nglish)/g(erman)/f(rench)")]
    public ClientLanguage? DalamudClientLanguage = ClientLanguage.English;

    [ArgumentParser.NamedArgumentAttribute(
        "-v --verbose",
        implicitValue: true,
        global: true,
        summary: "enable verbose injector logging")]
    public bool VerboseLogging = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--console",
        implicitValue: true,
        global: true,
        summary: "show Windows console to display stderr upon loading Dalamud")]
    public bool BootShowConsole = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--etw",
        implicitValue: true,
        global: true,
        summary: "enable ETW support")]
    public bool BootEnableEtw = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--veh",
        implicitValue: true,
        global: true,
        summary: "enable VEH handler")]
    public bool BootEnableVeh = true;

    [ArgumentParser.NamedArgumentAttribute(
        "--veh-full",
        implicitValue: true,
        global: true,
        summary: "dump full memory on crash when VEH handler is enabled")]
    public bool BootEnableVehFull = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--msgbox1",
        implicitValue: true,
        global: true,
        summary: "show a message box on loading Dalamud Boot")]
    public bool BootShowMsgbox1 = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--msgbox2",
        implicitValue: true,
        global: true,
        summary: "show a message box before loading CLR")]
    public bool BootShowMsgbox2 = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--msgbox3",
        implicitValue: true,
        global: true,
        summary: "show a message box before Dalamud ctor")]
    public bool BootShowMsgbox3 = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--disable-prevent-devicechange-crashes",
        implicitValue: true,
        global: true,
        summary: "disable gamefix \"prevent_devicechange_crashes\"")]
    public bool BootDisablePreventDeviceChangeCrashes = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--disable-disable-game-openprocess-access-check",
        implicitValue: true,
        global: true,
        summary: "disable gamefix \"disable_game_openprocess_access_check\"")]
    public bool BootDisableGameOpenProcessAccessCheck = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--disable-redirect-openprocess",
        implicitValue: true,
        global: true,
        summary: "disable gamefix \"redirect_openprocess\"")]
    public bool BootRedirectOpenProcess = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--disable-backup-userdata-save",
        implicitValue: true,
        global: true,
        summary: "disable gamefix \"backup_userdata_save\"")]
    public bool BootBackupUserdataSave = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--disable-clr-failfast-hijack",
        implicitValue: true,
        global: true,
        summary: "disable gamefix \"clr_failfast_hijack\"")]
    public bool BootClrFailfastHijack = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--unhook-dlls",
        global: true,
        summary: "specify dlls to reset upon loading Dalamud")]
    public List<string> BootUnhookDlls = new();

    [ArgumentParser.NamedArgumentAttribute(
        "--no-plugin",
        implicitValue: true,
        global: true,
        summary: "disable all plugins")]
    public bool NoPlugin = false;

    [ArgumentParser.NamedArgumentAttribute(
        "--no-3rd-plugin",
        implicitValue: true,
        global: true,
        summary: "disable 3rd party plugins")]
    public bool NoThirdPartyPlugin = false;

    public enum InjectMode
    {
        Entrypoint,
        Inject,
    }

    public class InjectVerbArguments
    {
        [ArgumentParser.NamedArgumentAttribute(
            "-h --help",
            implicitValue: true,
            summary: "display help message")]
        public bool Help;

        [ArgumentParser.NamedArgumentAttribute(
            "-a --all",
            implicitValue: true,
            summary: "inject into all ffxiv_dx11.exe processes detected")]
        public bool InjectToAll = false;

        [ArgumentParser.NamedArgumentAttribute(
            "--warn",
            implicitValue: true,
            summary: "warn and confirm before injecting")]
        public bool Warn = false;

        [ArgumentParser.NamedArgumentAttribute(
            "--fix-acl --acl-fix",
            implicitValue: true,
            summary: "fix ACL")]
        public bool FixAcl = false;

        [ArgumentParser.NamedArgumentAttribute(
            "--se-debug-privilege",
            implicitValue: true,
            summary: "obtain SeDebugPrivilege to inject")]
        public bool ObtainSeDebugPrivilege = false;

        [ArgumentParser.PositionalArgumentAttribute(
            "pid",
            summary: "specify ffxiv_dx11.exe PIDs to inject",
            valuePlaceholder: "pid")]
        public List<Process> Pids = new();
    }

    public class LaunchVerbArguments
    {
        [ArgumentParser.NamedArgumentAttribute(
            "-h --help",
            implicitValue: true,
            summary: "display help message")]
        public bool Help;

        [ArgumentParser.NamedArgumentAttribute(
            "-f --fake-arguments",
            implicitValue: true,
            summary: "launch game with placeholder credentials")]
        public bool FakeArguments = false;

        [ArgumentParser.NamedArgumentAttribute(
            "-g --game-path",
            summary: "specify game executable path (ffxiv_dx11.exe)",
            valuePlaceholder: "path")]
        public string? GamePath = string.Empty;

        [ArgumentParser.NamedArgumentAttribute(
            "-m --mode",
            summary: "specify inject mode",
            valuePlaceholder: "e(ntrypoint)/i(nject)")]
        public InjectMode InjectMode = InjectMode.Entrypoint;

        [ArgumentParser.NamedArgumentAttribute(
            "--handle-owner",
            summary: "create a handle for use in specified process handle")]
        public IntPtr HandleOwner = IntPtr.Zero;

        [ArgumentParser.NamedArgumentAttribute(
            "--without-dalamud",
            implicitValue: true,
            summary: "launch game without Dalamud")]
        public bool WithoutDalamud = false;

        [ArgumentParser.NamedArgumentAttribute(
            "--no-fix-acl --no-acl-fix",
            implicitValue: true,
            summary: "do not fix ACL")]
        public bool NoFixAcl = false;

        [ArgumentParser.NamedArgumentAttribute(
            "--no-wait",
            implicitValue: true,
            summary: "do not wait for Dalamud to initialize")]
        public bool NoWait = false;

        [ArgumentParser.PassthroughArgumentAttribute(
            "GameArgument=Value",
            "--",
            summary: "specify arguments to game to pass",
            valuePlaceholder: "arg")]
        public string[] GameArguments = Array.Empty<string>();
    }
}
