#include "pch.h"

#include "resource.h"

#include "veh.h"

#include <shellapi.h>

#include "logging.h"
#include "utils.h"

#include "crashhandler_shared.h"
#include "DalamudStartInfo.h"

#pragma comment(lib, "comctl32.lib")

#if defined _M_IX86
#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='x86' publicKeyToken='6595b64144ccf1df' language='*'\"")
#elif defined _M_IA64
#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='ia64' publicKeyToken='6595b64144ccf1df' language='*'\"")
#elif defined _M_X64
#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='amd64' publicKeyToken='6595b64144ccf1df' language='*'\"")
#else
#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")
#endif

PVOID g_veh_handle = nullptr;
bool g_veh_do_full_dump = false;

exception_info* g_crashhandler_shared_info;
HANDLE g_crashhandler_event;

std::chrono::time_point<std::chrono::system_clock> g_time_start;

bool is_whitelist_exception(const DWORD code)
{
    switch (code)
    {
    case STATUS_ACCESS_VIOLATION:
    case STATUS_IN_PAGE_ERROR:
    case STATUS_INVALID_HANDLE:
    case STATUS_INVALID_PARAMETER:
    case STATUS_NO_MEMORY:
    case STATUS_ILLEGAL_INSTRUCTION:
    case STATUS_NONCONTINUABLE_EXCEPTION:
    case STATUS_INVALID_DISPOSITION:
    case STATUS_ARRAY_BOUNDS_EXCEEDED:
    case STATUS_FLOAT_DENORMAL_OPERAND:
    case STATUS_FLOAT_DIVIDE_BY_ZERO:
    case STATUS_FLOAT_INEXACT_RESULT:
    case STATUS_FLOAT_INVALID_OPERATION:
    case STATUS_FLOAT_OVERFLOW:
    case STATUS_FLOAT_STACK_CHECK:
    case STATUS_FLOAT_UNDERFLOW:
    case STATUS_INTEGER_DIVIDE_BY_ZERO:
    case STATUS_INTEGER_OVERFLOW:
    case STATUS_PRIVILEGED_INSTRUCTION:
    case STATUS_STACK_OVERFLOW:
    case STATUS_DLL_NOT_FOUND:
    case STATUS_ORDINAL_NOT_FOUND:
    case STATUS_ENTRYPOINT_NOT_FOUND:
    case STATUS_DLL_INIT_FAILED:
    case STATUS_CONTROL_STACK_VIOLATION:
    case STATUS_FLOAT_MULTIPLE_FAULTS:
    case STATUS_FLOAT_MULTIPLE_TRAPS:
    case STATUS_HEAP_CORRUPTION:
    case STATUS_STACK_BUFFER_OVERRUN:
    case STATUS_INVALID_CRUNTIME_PARAMETER:
    case STATUS_THREAD_NOT_RUNNING:
    case STATUS_ALREADY_REGISTERED:
        return true;
    default:
        return false;
    }
}


bool get_module_file_and_base(const DWORD64 address, DWORD64& module_base, std::filesystem::path& module_file)
{
    HMODULE handle;
    if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, reinterpret_cast<LPCSTR>(address), &handle))
    {
        std::wstring path(PATHCCH_MAX_CCH, L'\0');
        path.resize(GetModuleFileNameW(handle, &path[0], static_cast<DWORD>(path.size())));
        if (!path.empty())
        {
            module_base = reinterpret_cast<DWORD64>(handle);
            module_file = path;
            return true;
        }
    }
    return false;
}


bool is_ffxiv_address(const wchar_t* module_name, const DWORD64 address)
{
    DWORD64 module_base;
    if (std::filesystem::path module_path; get_module_file_and_base(address, module_base, module_path))
        return _wcsicmp(module_path.filename().c_str(), module_name) == 0;
    return false;
}


bool get_sym_from_addr(const DWORD64 address, DWORD64& displacement, std::wstring& symbol_name)
{
    union {
        char buffer[sizeof(SYMBOL_INFOW) + MAX_SYM_NAME * sizeof(wchar_t)]{};
        SYMBOL_INFOW symbol;
    };
    symbol.SizeOfStruct = sizeof(SYMBOL_INFO);
    symbol.MaxNameLen = MAX_SYM_NAME;

    if (SymFromAddrW(GetCurrentProcess(), address, &displacement, &symbol) && symbol.Name[0])
    {
        symbol_name = symbol.Name;
        return true;
    }
    return false;
}


std::wstring to_address_string(const DWORD64 address, const bool try_ptrderef = true)
{
    DWORD64 module_base;
    std::filesystem::path module_path;
    bool is_mod_addr = get_module_file_and_base(address, module_base, module_path);

    DWORD64 value = 0;
    if(try_ptrderef && address > 0x10000 && address < 0x7FFFFFFE0000)
    {
        ReadProcessMemory(GetCurrentProcess(), reinterpret_cast<void*>(address), &value, sizeof value, nullptr);
    }

    std::wstring addr_str = is_mod_addr ?
        std::format(L"{}+{:X}", module_path.filename().c_str(), address - module_base) :
        std::format(L"{:X}", address);

    DWORD64 displacement;
    if (std::wstring symbol; get_sym_from_addr(address, displacement, symbol))
        return std::format(L"{}\t({})", addr_str, displacement != 0 ? std::format(L"{}+0x{:X}", symbol, displacement) : std::format(L"{}", symbol));
    return value != 0 ? std::format(L"{} [{}]", addr_str, to_address_string(value, false)) : addr_str;
}

void print_exception_info_extended(const EXCEPTION_POINTERS* ex, std::wostringstream& log)
{
    CONTEXT ctx = *ex->ContextRecord;

    log << L"\nRegisters\n{";

    log << std::format(L"\n  RAX:\t{}", to_address_string(ctx.Rax));
    log << std::format(L"\n  RBX:\t{}", to_address_string(ctx.Rbx));
    log << std::format(L"\n  RCX:\t{}", to_address_string(ctx.Rcx));
    log << std::format(L"\n  RDX:\t{}", to_address_string(ctx.Rdx));
    log << std::format(L"\n  R8:\t{}", to_address_string(ctx.R8));
    log << std::format(L"\n  R9:\t{}", to_address_string(ctx.R9));
    log << std::format(L"\n  R10:\t{}", to_address_string(ctx.R10));
    log << std::format(L"\n  R11:\t{}", to_address_string(ctx.R11));
    log << std::format(L"\n  R12:\t{}", to_address_string(ctx.R12));
    log << std::format(L"\n  R13:\t{}", to_address_string(ctx.R13));
    log << std::format(L"\n  R14:\t{}", to_address_string(ctx.R14));
    log << std::format(L"\n  R15:\t{}", to_address_string(ctx.R15));

    log << std::format(L"\n  RSI:\t{}", to_address_string(ctx.Rsi));
    log << std::format(L"\n  RDI:\t{}", to_address_string(ctx.Rdi));
    log << std::format(L"\n  RBP:\t{}", to_address_string(ctx.Rbp));
    log << std::format(L"\n  RSP:\t{}", to_address_string(ctx.Rsp));
    log << std::format(L"\n  RIP:\t{}", to_address_string(ctx.Rip));

    log << L"\n}" << std::endl;

    if(0x10000 < ctx.Rsp && ctx.Rsp < 0x7FFFFFFE0000)
    {
        log << L"\nStack\n{";

        for(DWORD64 i = 0; i < 16; i++)
            log << std::format(L"\n  [RSP+{:X}]\t{}", i * 8, to_address_string(*reinterpret_cast<DWORD64*>(ctx.Rsp + i * 8ull)));

        log << L"\n}\n";
    }

    log << L"\nModules\n{";

    if(HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, GetCurrentProcessId()); snap != INVALID_HANDLE_VALUE)
    {
        MODULEENTRY32 mod;
        mod.dwSize = sizeof MODULEENTRY32;
        if(Module32First(snap, &mod))
        {
            do
            {
                log << std::format(L"\n  {:08X}\t{}", reinterpret_cast<DWORD64>(mod.modBaseAddr), mod.szExePath);
            }
            while (Module32Next(snap, &mod));
        }
        CloseHandle(snap);
    }

    log << L"\n}\n";
}

void print_exception_info(const EXCEPTION_POINTERS* ex, std::wostringstream& log)
{
    size_t rec_index = 0;
    for (auto rec = ex->ExceptionRecord; rec; rec = rec->ExceptionRecord)
    {
        log << std::format(L"\nException Info #{}\n", ++rec_index);
        log << std::format(L"Address: {:X}\n", rec->ExceptionCode);
        log << std::format(L"Flags: {:X}\n", rec->ExceptionFlags);
        log << std::format(L"Address: {:X}\n", reinterpret_cast<size_t>(rec->ExceptionAddress));
        if (!rec->NumberParameters)
            continue;
        log << L"Parameters: ";
        for (DWORD i = 0; i < rec->NumberParameters; ++i)
        {
            if (i != 0)
                log << L", ";
            log << std::format(L"{:X}", rec->ExceptionInformation[i]);
        }
    }
    
    log << L"\nCall Stack\n{";

    STACKFRAME64 sf;
    sf.AddrPC.Offset = ex->ContextRecord->Rip;
    sf.AddrPC.Mode = AddrModeFlat;
    sf.AddrStack.Offset = ex->ContextRecord->Rsp;
    sf.AddrStack.Mode = AddrModeFlat;
    sf.AddrFrame.Offset = ex->ContextRecord->Rbp;
    sf.AddrFrame.Mode = AddrModeFlat;
    CONTEXT ctx = *ex->ContextRecord;
    int frame_index = 0;

    log << std::format(L"\n  [{}]\t{}", frame_index++, to_address_string(sf.AddrPC.Offset, false));

    do
    {
        if (!StackWalk64(IMAGE_FILE_MACHINE_AMD64, GetCurrentProcess(), GetCurrentThread(), &sf, &ctx, nullptr, SymFunctionTableAccess64, SymGetModuleBase64, nullptr))
            break;

        log << std::format(L"\n  [{}]\t{}", frame_index++, to_address_string(sf.AddrPC.Offset, false));

    } while (sf.AddrReturn.Offset != 0 && sf.AddrPC.Offset != sf.AddrReturn.Offset);

    log << L"\n}\n";
}

enum {
    IdRadioRestartNormal = 101,
    IdRadioRestartWithout3pPlugins,
    IdRadioRestartWithoutPlugins,
    IdRadioRestartWithoutDalamud,

    IdButtonRestart = 201,
    IdButtonHelp = IDHELP,
    IdButtonExit = IDCANCEL,
};

static void restart_game_using_injector(int nRadioButton)
{
    std::vector<std::wstring> args;
    args.emplace_back((utils::loaded_module(g_hModule).path().parent_path() / L"Dalamud.Injector.exe").wstring());
    args.emplace_back(L"launch");
    args.emplace_back(L"-g");
    args.emplace_back(utils::loaded_module::current_process().path().wstring());
    if (g_startInfo.BootShowConsole)
        args.emplace_back(L"--console");
    if (g_startInfo.BootEnableEtw)
        args.emplace_back(L"--etw");
    if (g_startInfo.BootVehEnabled)
        args.emplace_back(L"--veh");
    if (g_startInfo.BootVehFull)
        args.emplace_back(L"--veh-full");
    if ((g_startInfo.BootWaitMessageBox & DalamudStartInfo::WaitMessageboxFlags::BeforeInitialize) != DalamudStartInfo::WaitMessageboxFlags::None)
        args.emplace_back(L"--msgbox1");
    if ((g_startInfo.BootWaitMessageBox & DalamudStartInfo::WaitMessageboxFlags::BeforeDalamudEntrypoint) != DalamudStartInfo::WaitMessageboxFlags::None)
        args.emplace_back(L"--msgbox2");
    if ((g_startInfo.BootWaitMessageBox & DalamudStartInfo::WaitMessageboxFlags::BeforeDalamudConstruct) != DalamudStartInfo::WaitMessageboxFlags::None)
        args.emplace_back(L"--msgbox3");
    switch (nRadioButton) {
        case IdRadioRestartWithout3pPlugins:
            args.emplace_back(L"--no-3rd-plugin");
            break;
        case IdRadioRestartWithoutPlugins:
            args.emplace_back(L"--no-plugin");
            break;
        case IdRadioRestartWithoutDalamud:
            args.emplace_back(L"--without-dalamud");
            break;
    }
    args.emplace_back(L"--");

    if (int nArgs; LPWSTR * szArgList = CommandLineToArgvW(GetCommandLineW(), &nArgs)) {
        for (auto i = 1; i < nArgs; i++)
            args.emplace_back(szArgList[i]);
        LocalFree(szArgList);
    }

    std::wstring argstr;
    for (const auto& arg : args) {
        argstr.append(utils::escape_shell_arg(arg));
        argstr.push_back(L' ');
    }
    argstr.pop_back();

    STARTUPINFOW si{};
    si.cb = sizeof si;
    si.dwFlags = STARTF_USESHOWWINDOW;
#ifdef _DEBUG
    si.wShowWindow = SW_SHOW;
#else
    si.wShowWindow = SW_HIDE;
#endif
    PROCESS_INFORMATION pi{};
    if (CreateProcessW(args[0].c_str(), &argstr[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    } else {
        MessageBoxW(nullptr, std::format(L"Failed to restart: 0x{:x}", GetLastError()).c_str(), L"Dalamud Boot", MB_ICONERROR | MB_OK);
    }
}

LONG exception_handler(EXCEPTION_POINTERS* ex)
{
    static std::recursive_mutex s_exception_handler_mutex;

    if (ex->ExceptionRecord->ExceptionCode == 0x12345678) {
        restart_game_using_injector(IdRadioRestartNormal);
        TerminateProcess(GetCurrentProcess(), 0);
        return EXCEPTION_CONTINUE_SEARCH;
    }

    if (!is_whitelist_exception(ex->ExceptionRecord->ExceptionCode))
        return EXCEPTION_CONTINUE_SEARCH;

    if (!is_ffxiv_address(L"ffxiv_dx11.exe", ex->ContextRecord->Rip) &&
        !is_ffxiv_address(L"cimgui.dll", ex->ContextRecord->Rip))
        return EXCEPTION_CONTINUE_SEARCH;

    // block any other exceptions hitting the veh while the messagebox is open
    const auto lock = std::lock_guard(s_exception_handler_mutex);

    const auto log_base_dir = g_startInfo.BootLogPath.empty()
        ? utils::loaded_module(g_hModule).path().parent_path()
        : std::filesystem::path(g_startInfo.BootLogPath).parent_path();
#ifndef NDEBUG
    const auto dmp_path = (log_base_dir / L"dalamud_appcrashd.dmp").wstring();
#else
    const auto dmp_path = (log_base_dir / L"dalamud_appcrash.dmp").wstring();
#endif
    const auto log_path = (log_base_dir / L"dalamud_appcrash.log").wstring();

    std::wostringstream log;
    log << std::format(L"Unhandled native exception occurred at {}", to_address_string(ex->ContextRecord->Rip, false)) << std::endl;
    log << std::format(L"Code: {:X}", ex->ExceptionRecord->ExceptionCode) << std::endl;
    log << std::format(L"Dump at: {}", dmp_path) << std::endl;
    log << L"Time: " << std::chrono::zoned_time{ std::chrono::current_zone(), std::chrono::system_clock::now() } << std::endl;

    SymRefreshModuleList(GetCurrentProcess());
    print_exception_info(ex, log);
    auto window_log_str = log.str();
    print_exception_info_extended(ex, log);
    
    if (g_crashhandler_shared_info && g_crashhandler_event)
    {
        memset(g_crashhandler_shared_info, 0, sizeof(exception_info));
        
        wcsncpy_s(g_crashhandler_shared_info->DumpPath, dmp_path.c_str(), 1000);
        g_crashhandler_shared_info->ThreadId = GetThreadId(GetCurrentThread());
        g_crashhandler_shared_info->ProcessId = GetCurrentProcessId();
        g_crashhandler_shared_info->ExceptionPointers = ex;
        g_crashhandler_shared_info->DoFullDump = g_veh_do_full_dump;
        g_crashhandler_shared_info->ExceptionCode = ex->ExceptionRecord->ExceptionCode;

        const auto time_now = std::chrono::system_clock::now();
        auto lifetime = std::chrono::duration_cast<std::chrono::seconds>(
                time_now.time_since_epoch()).count()
            - std::chrono::duration_cast<std::chrono::seconds>(
                g_time_start.time_since_epoch()).count();

        g_crashhandler_shared_info->Lifetime = lifetime;

        SetEvent(g_crashhandler_event);
    }
    
    std::wstring message;
    void* fn;
    if (const auto err = static_cast<DWORD>(g_clr->get_function_pointer(
        L"Dalamud.EntryPoint, Dalamud",
        L"VehCallback",
        L"Dalamud.EntryPoint+VehDelegate, Dalamud", 
        nullptr, nullptr, &fn)))
    {
        message = std::format(
            L"An error within the game has occurred.\n\n"
            L"This may be caused by a faulty plugin, a broken TexTools modification, any other third-party tool or simply a bug in the game.\n"
            L"Please try \"Start Over\" or \"Download Index Backup\" in TexTools, an integrity check in the XIVLauncher settings, and disabling plugins you don't need.\n\n"
            L"The log file is located at:\n"
            L"{1}\n\n"
            L"Press OK to exit the application.\n\nFailed to read stack trace: 0x{2:08x}",
            dmp_path, log_path, err);
    }
    else
    {
        const auto pMessage = ((wchar_t*(__stdcall*)(const void*, const void*, const void*))fn)(dmp_path.c_str(), log_path.c_str(), log.str().c_str());
        message = pMessage;
        // Don't free it, as the program's going to be quit anyway
    }

    logging::E(std::format(L"Trapped in VEH handler: {}", message));

    // show in another thread to prevent messagebox from pumping messages of current thread
    std::thread([&]()
    {
        TASKDIALOGCONFIG config = {0};

        const TASKDIALOG_BUTTON radios[] {
            {IdRadioRestartNormal, L"Restart"},
            {IdRadioRestartWithout3pPlugins, L"Restart without 3rd party plugins"},
            {IdRadioRestartWithoutPlugins, L"Restart without any plugin"},
            {IdRadioRestartWithoutDalamud, L"Restart without Dalamud"},
        };
        
        const TASKDIALOG_BUTTON buttons[] {
            {IdButtonRestart, L"Restart\nRestart the game, optionally without plugins or Dalamud."},
            {IdButtonExit, L"Exit\nExit the game."},
        };
        
        config.cbSize = sizeof(config);
        config.hInstance = g_hModule;
        config.dwFlags = TDF_ENABLE_HYPERLINKS | TDF_CAN_BE_MINIMIZED | TDF_ALLOW_DIALOG_CANCELLATION | TDF_USE_COMMAND_LINKS;
        config.pszMainIcon = MAKEINTRESOURCE(IDI_ICON1);
        config.pszMainInstruction = L"An error occurred";
        config.pszContent = message.c_str();
        config.pButtons = buttons;
        config.cButtons = ARRAYSIZE(buttons);
        config.nDefaultButton = IdButtonRestart;
        config.pszExpandedInformation = window_log_str.c_str();
        config.pszWindowTitle = L"Dalamud Error";
        config.pRadioButtons = radios;
        config.cRadioButtons = ARRAYSIZE(radios);
        config.nDefaultRadioButton = IdRadioRestartNormal;
        config.cxWidth = 300;
        config.pszFooter = LR"aaaaa(<a href="help">Open help</a> | <a href="log">Open log directory</a>)aaaaa";

        // Can't do this, xiv stops pumping messages here
        //config.hwndParent = FindWindowA("FFXIVGAME", NULL);

        const auto callback = [&](HWND hwnd, UINT uNotification, WPARAM wParam, LPARAM lParam) -> HRESULT
        {
            switch (uNotification)
            {
                case TDN_CREATED:
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                    return S_OK;
                }
                case TDN_HYPERLINK_CLICKED:
                {
                    const auto link = std::wstring_view(reinterpret_cast<const wchar_t*>(lParam));
                    if (link == L"help")
                        ShellExecuteW(hwnd, nullptr, L"https://goatcorp.github.io/faq?utm_source=vectored", nullptr, nullptr, SW_SHOW);
                    else if (link == L"log")
                        ShellExecuteW(hwnd, nullptr, log_base_dir.c_str(), nullptr, nullptr, SW_SHOW);
                    return S_OK;
                }
            }

            return S_OK;
        };
        
        config.pfCallback = [](HWND hwnd, UINT uNotification, WPARAM wParam, LPARAM lParam, LONG_PTR dwRefData)
        {
            return (*reinterpret_cast<decltype(callback)*>(dwRefData))(hwnd, uNotification, wParam, lParam);
        };
        config.lpCallbackData = reinterpret_cast<LONG_PTR>(&callback);

        int nButtonPressed = 0, nRadioButton = 0;
        if (FAILED(TaskDialogIndirect(&config, &nButtonPressed, &nRadioButton, NULL)))
            return EXCEPTION_CONTINUE_SEARCH;

        switch (nButtonPressed)
        {
            case IdButtonRestart:
            {
                restart_game_using_injector(nRadioButton);
                // attempt to prevent mutex clobbing by quitting as soon as possible
                TerminateProcess(GetCurrentProcess(), ex->ExceptionRecord->ExceptionCode);
                break;
            }
        }
        return 0;
    }).join();

    return EXCEPTION_CONTINUE_SEARCH;
}

bool veh::add_handler(bool doFullDump, std::string workingDirectory)
{
    if (g_veh_handle)
        return false;
    g_veh_handle = AddVectoredExceptionHandler(1, exception_handler);
    SetUnhandledExceptionFilter(nullptr);

    g_veh_do_full_dump = doFullDump;
    g_time_start = std::chrono::system_clock::now();

    auto file_mapping = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, sizeof(exception_info), SHARED_INFO_FILE_NAME);
    if (!file_mapping) {
        std::cout << "Could not map info share file.\n";
        g_crashhandler_shared_info = nullptr;
    }
    else
    {
        g_crashhandler_shared_info = (exception_info*)MapViewOfFile(file_mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(exception_info));
        if (!g_crashhandler_shared_info) {
            std::cout << "Could not map view of info share file.\n";
        }
    }

    g_crashhandler_event = CreateEvent(
        NULL,               // default security attributes
        TRUE,               // manual-reset event
        FALSE,              // initial state is nonsignaled
        CRASHDUMP_EVENT_NAME  // object name
    );

    if (!g_crashhandler_event)
    {
        std::cout << "Couldn't acquire event handle\n";
    }

    auto handler_path = std::filesystem::path(workingDirectory) / "DalamudCrashHandler.exe";
    
    // additional information
    STARTUPINFO si;     
    PROCESS_INFORMATION pi;

    // set the size of the structures
    ZeroMemory( &si, sizeof(si) );
    si.cb = sizeof(si);
    ZeroMemory( &pi, sizeof(pi) );

    CreateProcess( handler_path.c_str(),   // the path
    NULL,        // Command line
    NULL,           // Process handle not inheritable
    NULL,           // Thread handle not inheritable
    FALSE,          // Set handle inheritance to FALSE
    0,              // No creation flags
    NULL,           // Use parent's environment block
    NULL,           // Use parent's starting directory 
    &si,            // Pointer to STARTUPINFO structure
    &pi             // Pointer to PROCESS_INFORMATION structure (removed extra parentheses)
    );

    // Close process and thread handles. 
    CloseHandle( pi.hProcess );
    CloseHandle( pi.hThread );

    return g_veh_handle != nullptr;
}

bool veh::remove_handler()
{
    if (g_veh_handle && RemoveVectoredExceptionHandler(g_veh_handle) != 0)
    {
        g_veh_handle = nullptr;
        return true;
    }
    return false;
}
