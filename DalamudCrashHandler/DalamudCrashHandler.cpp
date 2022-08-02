#include <filesystem>
#include <fstream>
#include <iostream>
#include <map>
#include <optional>
#include <ranges>
#include <span>
#include <sstream>
#include <string>
#include <thread>
#include <vector>

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>

#include <clrdata.h>
#include <comdef.h>
#include <CommCtrl.h>
#include <DbgEng.h>
#include <minidumpapiset.h>
#include <PathCch.h>
#include <Psapi.h>
#include <shellapi.h>
#include <ShlObj.h>
#include <winhttp.h>

#pragma comment(lib, "comctl32.lib")
#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

#include "resource.h"
#include "../Dalamud.Boot/crashhandler_shared.h"

typedef unsigned int mdToken;
typedef unsigned int mdTypeDef;
typedef unsigned int mdMethodDef;
typedef unsigned int mdFieldDef;
typedef unsigned long CorElementType;

#include "xclrdata/xclrdata_h.h"
#include "xclrdata/xclrdata_i.c"

_COM_SMARTPTR_TYPEDEF(IDebugClient, __uuidof(IDebugClient));
_COM_SMARTPTR_TYPEDEF(IDebugControl4, __uuidof(IDebugControl4));
_COM_SMARTPTR_TYPEDEF(IDebugSystemObjects, __uuidof(IDebugSystemObjects));
_COM_SMARTPTR_TYPEDEF(IDebugSymbols3, __uuidof(IDebugSymbols3));
_COM_SMARTPTR_TYPEDEF(IXCLRDataProcess, __uuidof(IXCLRDataProcess));

HANDLE g_hProcess = nullptr;
std::filesystem::path g_assetsDirectory;
IXCLRDataProcessPtr g_pClrDataProcess;
IDebugClientPtr g_pDebugClient;
IDebugControl4Ptr g_pDebugControl;
IDebugSymbols3Ptr g_pDebugSymbols;
IDebugSystemObjectsPtr g_pDebugSystemObjects;

const std::map<HMODULE, size_t>& get_remote_modules() {
    static const auto data = [] {
        std::map<HMODULE, size_t> data;

        std::vector<HMODULE> buf(8192);
        for (size_t i = 0; i < 64; i++) {
            if (DWORD needed; !EnumProcessModules(g_hProcess, &buf[0], static_cast<DWORD>(std::span(buf).size_bytes()), &needed)) {
                std::cerr << std::format("EnumProcessModules error: 0x{:x}", GetLastError()) << std::endl;
                break;
            } else if (needed > std::span(buf).size_bytes()) {
                buf.resize(needed / sizeof(HMODULE) + 16);
            } else {
                buf.resize(needed / sizeof(HMODULE));
                break;
            }
        }

        for (const auto& hModule : buf) {
            IMAGE_DOS_HEADER dosh;
            IMAGE_NT_HEADERS64 nth64;
            if (size_t read; !ReadProcessMemory(g_hProcess, hModule, &dosh, sizeof dosh, &read) || read != sizeof dosh) {
                std::cerr << std::format("Failed to read IMAGE_DOS_HEADER for module at 0x{:x}", reinterpret_cast<size_t>(hModule)) << std::endl;
                continue;
            }

            if (size_t read; !ReadProcessMemory(g_hProcess, reinterpret_cast<const char*>(hModule) + dosh.e_lfanew, &nth64, sizeof nth64, &read) || read != sizeof nth64) {
                std::cerr << std::format("Failed to read IMAGE_NT_HEADERS64 for module at 0x{:x}", reinterpret_cast<size_t>(hModule)) << std::endl;
                continue;
            }

            data[hModule] = nth64.OptionalHeader.SizeOfImage;
        }

        return data;
    }();

    return data;
}

const std::map<HMODULE, std::filesystem::path>& get_remote_module_paths() {
    static const auto data = [] {
        std::map<HMODULE, std::filesystem::path> data;

        std::wstring buf(PATHCCH_MAX_CCH, L'\0');
        for (const auto& hModule : get_remote_modules() | std::views::keys) {
            buf.resize(PATHCCH_MAX_CCH, L'\0');
            buf.resize(GetModuleFileNameExW(g_hProcess, hModule, &buf[0], PATHCCH_MAX_CCH));
            if (buf.empty()) {
                std::cerr << std::format("Failed to get path for module at 0x{:x}: error 0x{:x}", reinterpret_cast<size_t>(hModule), GetLastError()) << std::endl;
                continue;
            }

            data[hModule] = buf;
        }

        return data;
    }();
    return data;
}

bool get_module_file_and_base(const DWORD64 address, DWORD64& module_base, std::filesystem::path& module_file) {
    for (const auto& [hModule, path] : get_remote_module_paths()) {
        const auto nAddress = reinterpret_cast<DWORD64>(hModule);
        if (address < nAddress)
            continue;

        const auto nAddressTo = nAddress + get_remote_modules().at(hModule);
        if (nAddressTo <= address)
            continue;

        module_base = nAddress;
        module_file = path;
        return true;
    }

    return false;
}

bool is_ffxiv_address(const wchar_t* module_name, const DWORD64 address) {
    DWORD64 module_base;
    if (std::filesystem::path module_path; get_module_file_and_base(address, module_base, module_path))
        return _wcsicmp(module_path.filename().c_str(), module_name) == 0;
    return false;
}

std::wstring to_address_string(const DWORD64 address, const bool try_ptrderef = true) {
    std::wstring buf(1024, L'\0');
    if (g_pClrDataProcess) {
        CLRDATA_ADDRESS displacement;
        ULONG32 len;
        if (SUCCEEDED(g_pClrDataProcess->GetRuntimeNameByAddress(address, 0, 1024, &len, &buf[0], &displacement))) {
            buf.resize(len ? len - 1 : 0);
            return std::format(L"CLR:{:X}\t({}+{:X})", address, buf, displacement);
        }
    }

    DWORD64 module_base;
    std::filesystem::path module_path;
    bool is_mod_addr = get_module_file_and_base(address, module_base, module_path);

    DWORD64 value = 0;
    if (try_ptrderef && address > 0x10000 && address < 0x7FFFFFFE0000) {
        ReadProcessMemory(g_hProcess, reinterpret_cast<void*>(address), &value, sizeof value, nullptr);
    }

    std::wstring addr_str = is_mod_addr ? std::format(L"{}+{:X}", module_path.filename().c_str(), address - module_base) : std::format(L"{:X}", address);
    {
        DWORD64 displacement;
        ULONG len;
        buf.resize(2000 /*MAX_SYM_NAME*/, L'\0');
        if (SUCCEEDED(g_pDebugSymbols->GetNameByOffsetWide(address, &buf[0], static_cast<ULONG>(buf.size()), &len, &displacement))) {
            buf.resize(len ? len - 1 : 0);
            return std::format(L"{}\t({}+{:X})", addr_str, buf, displacement);
        }
    }
    return value != 0 ? std::format(L"{} [{}]", addr_str, to_address_string(value, false)) : addr_str;
}

void print_exception_info(HANDLE hThread, const EXCEPTION_POINTERS& ex, const CONTEXT& ctx, std::wostringstream& log) {
    std::vector<EXCEPTION_RECORD> exRecs;
    if (ex.ExceptionRecord) {
        size_t rec_index = 0;
        size_t read;
        exRecs.emplace_back();
        for (auto pRemoteExRec = ex.ExceptionRecord;
            pRemoteExRec
            && rec_index < 64
            && ReadProcessMemory(g_hProcess, pRemoteExRec, &exRecs.back(), sizeof exRecs.back(), &read)
            && read >= offsetof(EXCEPTION_RECORD, ExceptionInformation)
            && read >= static_cast<size_t>(reinterpret_cast<const char*>(&exRecs.back().ExceptionInformation[exRecs.back().NumberParameters]) - reinterpret_cast<const char*>(&exRecs.back()));
            rec_index++) {

            log << std::format(L"\nException Info #{}\n", rec_index);
            log << std::format(L"Code: {:X}\n", exRecs.back().ExceptionCode);
            log << std::format(L"Flags: {:X}\n", exRecs.back().ExceptionFlags);
            log << std::format(L"Address: {:X}\n", reinterpret_cast<size_t>(exRecs.back().ExceptionAddress));
            if (!exRecs.back().NumberParameters)
                continue;
            log << L"Parameters: ";
            for (DWORD i = 0; i < exRecs.back().NumberParameters; ++i) {
                if (i != 0)
                    log << L", ";
                log << std::format(L"{:X}", exRecs.back().ExceptionInformation[i]);
            }

            pRemoteExRec = exRecs.back().ExceptionRecord;
            exRecs.emplace_back();
        }
        exRecs.pop_back();
    }

    const auto tid = GetThreadId(hThread);

    try {
        ULONG tidl;
        if (const auto hr = g_pDebugSystemObjects->GetThreadIdBySystemId(tid, &tidl); FAILED(hr))
            throw _com_error(hr);
        if (const auto hr = g_pDebugSystemObjects->SetCurrentThreadId(tidl); FAILED(hr))
            throw _com_error(hr);

        const auto MaxFrameAndContextCount = 512;
        ULONG framesFilled;

        DEBUG_STACK_FRAME firstFrame;
        if (const auto hr = g_pDebugControl->GetContextStackTrace((void*)&ctx, sizeof ctx, &firstFrame, 1, nullptr, sizeof ctx, sizeof ctx, &framesFilled); FAILED(hr) || !framesFilled)
            throw _com_error(hr);

        log << L"\nCall Stack\n{";

        std::vector<DEBUG_STACK_FRAME> frames;
        frames.resize(MaxFrameAndContextCount);
        if (const auto hr = g_pDebugControl->GetStackTrace(firstFrame.FrameOffset, firstFrame.StackOffset, firstFrame.InstructionOffset, &frames[0], MaxFrameAndContextCount, &framesFilled); FAILED(hr) || !framesFilled)
            throw _com_error(hr);

        frames.resize(framesFilled);
        for (const auto& frame : frames)
            log << std::format(L"\n  [{}]\t{}", frame.FrameNumber, to_address_string(frame.InstructionOffset, false));

        log << L"\n}\n";
    } catch (const _com_error& e) {
        log << std::format(L"Failed to read call stack: hr=0x{:08x} message={}\n", e.Error(), e.ErrorMessage());
    }
}

void attach_debugger() {
    const auto pid = GetProcessId(g_hProcess);

    try {
        if (const auto hr = DebugCreate(__uuidof(IDebugClient), (void**)&g_pDebugClient); FAILED(hr))
            throw _com_error(hr);
        if (const auto hr = g_pDebugClient->AttachProcess(0, pid, DEBUG_ATTACH_NONINVASIVE | DEBUG_ATTACH_NONINVASIVE_NO_SUSPEND); FAILED(hr))
            throw _com_error(hr);

        if (const auto hr = g_pDebugClient.QueryInterface(__uuidof(IDebugControl4), &g_pDebugControl); FAILED(hr))
            throw _com_error(hr);

        g_pDebugControl->SetExecutionStatus(DEBUG_STATUS_GO);
        if (const auto hr = g_pDebugControl->WaitForEvent(DEBUG_WAIT_DEFAULT, INFINITE); FAILED(hr))
            throw _com_error(hr);

        if (const auto hr = g_pDebugClient.QueryInterface(__uuidof(IDebugSymbols3), &g_pDebugSymbols); FAILED(hr))
            throw _com_error(hr);

        g_pDebugSymbols->AppendSymbolPathWide((g_assetsDirectory / "UIRes" / "pdb").c_str());

        if (const auto hr = g_pDebugClient.QueryInterface(__uuidof(IDebugSystemObjects), &g_pDebugSystemObjects); FAILED(hr))
            throw _com_error(hr);
    } catch (const _com_error& e) {
        std::wcerr << std::format(L"Failed to read call stack: hr=0x{:08x} message={}\n", static_cast<unsigned int>(e.Error()), e.ErrorMessage()) << std::endl;
        throw std::exception("Debugger attach failure");
    }
}

void print_exception_info_extended(const EXCEPTION_POINTERS& ex, const CONTEXT& ctx, std::wostringstream& log) {
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

    if (0x10000 < ctx.Rsp && ctx.Rsp < 0x7FFFFFFE0000) {
        log << L"\nStack\n{";

        std::vector<DWORD64> stackData;
        stackData.resize(64);
        size_t read = 0;
        ReadProcessMemory(g_hProcess, reinterpret_cast<void*>(ctx.Rsp), &stackData[0], std::span(stackData).size_bytes(), &read);
        for (DWORD64 i = 0; i < stackData.size() && i * sizeof(size_t) < read; i++) {
            if (stackData[i])
                log << std::format(L"\n  [RSP+{:X}]\t{}", i * 8, to_address_string(stackData[i]));
        }

        log << L"\n}\n";
    }

    log << L"\nModules\n{";

    for (const auto& [hModule, path] : get_remote_module_paths())
        log << std::format(L"\n  {:08X}\t{}", reinterpret_cast<DWORD64>(hModule), path.wstring());

    log << L"\n}\n";
}

std::wstring escape_shell_arg(const std::wstring& arg) {
    // https://docs.microsoft.com/en-us/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way

    std::wstring res;
    if (!arg.empty() && arg.find_first_of(L" \t\n\v\"") == std::wstring::npos) {
        res.append(arg);
    } else {
        res.push_back(L'"');
        for (auto it = arg.begin(); ; ++it) {
            size_t bsCount = 0;

            while (it != arg.end() && *it == L'\\') {
                ++it;
                ++bsCount;
            }

            if (it == arg.end()) {
                res.append(bsCount * 2, L'\\');
                break;
            } else if (*it == L'"') {
                res.append(bsCount * 2 + 1, L'\\');
                res.push_back(*it);
            } else {
                res.append(bsCount, L'\\');
                res.push_back(*it);
            }
        }

        res.push_back(L'"');
    }
    return res;
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

void restart_game_using_injector(int nRadioButton, const std::vector<std::wstring>& launcherArgs) {
    std::wstring pathStr(PATHCCH_MAX_CCH, L'\0');
    pathStr.resize(GetModuleFileNameExW(GetCurrentProcess(), GetModuleHandleW(nullptr), &pathStr[0], PATHCCH_MAX_CCH));

    std::vector<std::wstring> args;
    args.emplace_back((std::filesystem::path(pathStr).parent_path() / L"Dalamud.Injector.exe").wstring());
    args.emplace_back(L"launch");
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
    args.insert(args.end(), launcherArgs.begin(), launcherArgs.end());

    std::wstring argstr;
    for (const auto& arg : args) {
        argstr.append(escape_shell_arg(arg));
        argstr.push_back(L' ');
    }
    argstr.pop_back();

    STARTUPINFOW si{};
    si.cb = sizeof si;
    si.dwFlags = STARTF_USESHOWWINDOW;
#ifndef NDEBUG
    si.wShowWindow = SW_HIDE;
#else
    si.wShowWindow = SW_SHOW;
#endif
    PROCESS_INFORMATION pi{};
    if (CreateProcessW(args[0].c_str(), &argstr[0], nullptr, nullptr, FALSE, 0, nullptr, nullptr, &si, &pi)) {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    } else {
        MessageBoxW(nullptr, std::format(L"Failed to restart: 0x{:x}", GetLastError()).c_str(), L"Dalamud Boot", MB_ICONERROR | MB_OK);
    }
}

void try_attach_xclr() {
    if (g_pClrDataProcess)
        return;

    std::filesystem::path runtimeDir;

    std::wstring buffer;
    buffer.resize(1 + GetEnvironmentVariableW(L"DALAMUD_RUNTIME", nullptr, 0));
    buffer.resize(GetEnvironmentVariableW(L"DALAMUD_RUNTIME", &buffer[0], static_cast<DWORD>(buffer.size())));

    if (buffer.empty()) {
        wchar_t* _appdata;
        SHGetKnownFolderPath(FOLDERID_RoamingAppData, KF_FLAG_DEFAULT, nullptr, &_appdata);
        runtimeDir = std::filesystem::path(_appdata) / "XIVLauncher" / "runtime";
    } else {
        runtimeDir = buffer;
    }

    const auto hCorDbgIface = LoadLibraryW((runtimeDir / "shared" / "Microsoft.NETCore.App" / "5.0.17" / "mscordaccore.dll").c_str());
    if (!hCorDbgIface)
        return;

    const auto pfnCLRDataCreateInstance = reinterpret_cast<decltype(&CLRDataCreateInstance)>(GetProcAddress(hCorDbgIface, "CLRDataCreateInstance"));
    if (!pfnCLRDataCreateInstance)
        return;

    class MyDataTarget : public ICLRDataTarget {
    public:
        ~MyDataTarget() = default;
        HRESULT QueryInterface(const IID& riid, void** ppvObject) override {
            if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_ICLRDataTarget)) {
                this->AddRef();
                *ppvObject = this;
                return S_OK;
            }
            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }
        ULONG AddRef() override { return 1; }
        ULONG Release() override { return 0; }
        HRESULT GetMachineType(ULONG32* machineType) override { *machineType = IMAGE_FILE_MACHINE_AMD64; return S_OK; }
        HRESULT GetPointerSize(ULONG32* pointerSize) override { *pointerSize = sizeof(void*); return S_OK; }
        HRESULT GetImageBase(LPCWSTR imagePath, CLRDATA_ADDRESS* baseAddress) override {
            const auto requestedPath = std::filesystem::path(imagePath);
            for (const auto& [hModule, path] : get_remote_module_paths()) {
                if (requestedPath.has_parent_path() && _wcsicmp(requestedPath.c_str(), path.c_str()) == 0) {
                    *baseAddress = reinterpret_cast<CLRDATA_ADDRESS>(hModule);
                    return S_OK;
                }
                if (!requestedPath.has_parent_path() && _wcsicmp(requestedPath.c_str(), path.filename().c_str()) == 0) {
                    *baseAddress = reinterpret_cast<CLRDATA_ADDRESS>(hModule);
                    return S_OK;
                }
            }
            return E_INVALIDARG;
        }
        HRESULT ReadVirtual(CLRDATA_ADDRESS address, BYTE* buffer, ULONG32 bytesRequested, ULONG32* bytesRead) override {
            SIZE_T bytesReadSizeT{};
            *bytesRead = 0;
            if (!ReadProcessMemory(g_hProcess, reinterpret_cast<void*>(address), buffer, bytesRequested, &bytesReadSizeT))
                return HRESULT_FROM_WIN32(GetLastError());
            *bytesRead = static_cast<ULONG32>(bytesReadSizeT);
            return S_OK;
        }
        HRESULT WriteVirtual(CLRDATA_ADDRESS address, BYTE* buffer, ULONG32 bytesRequested, ULONG32* bytesWritten) override { return E_NOTIMPL; }
        HRESULT GetTLSValue(ULONG32 threadID, ULONG32 index, CLRDATA_ADDRESS* value) override { return E_NOTIMPL; }
        HRESULT SetTLSValue(ULONG32 threadID, ULONG32 index, CLRDATA_ADDRESS value) override { return E_NOTIMPL; }
        HRESULT GetCurrentThreadID(ULONG32* threadID) override { return E_NOTIMPL; }
        HRESULT GetThreadContext(ULONG32 threadID, ULONG32 contextFlags, ULONG32 contextSize, BYTE* context) override { return E_NOTIMPL; }
        HRESULT SetThreadContext(ULONG32 threadID, ULONG32 contextSize, BYTE* context) override { return E_NOTIMPL; }
        HRESULT Request(ULONG32 reqCode, ULONG32 inBufferSize, BYTE* inBuffer, ULONG32 outBufferSize, BYTE* outBuffer) override { return E_NOTIMPL; }
    };

    static MyDataTarget myDataTarget;

    if (const auto status = pfnCLRDataCreateInstance(IID_IXCLRDataProcess, &myDataTarget, reinterpret_cast<void**>(&g_pClrDataProcess)); status != S_OK)
        return;
}

int main() {
    enum crash_handler_special_exit_codes {
        InvalidParameter = -101,
        ProcessExitedUnknownExitCode = -102,
    };

    HANDLE hPipeRead = nullptr;
    std::filesystem::path logDir;
    std::optional<std::vector<std::wstring>> launcherArgs;

    std::vector<std::wstring> args;
    if (int argc = 0; const auto argv = CommandLineToArgvW(GetCommandLineW(), &argc)) {
        for (auto i = 0; i < argc; i++)
            args.emplace_back(argv[i]);
        LocalFree(argv);
    }
    for (size_t i = 1; i < args.size(); i++) {
        const auto arg = std::wstring_view(args[i]);
        if (launcherArgs) {
            launcherArgs->emplace_back(arg);
        } else if (constexpr wchar_t pwszArgPrefix[] = L"--process-handle="; arg.starts_with(pwszArgPrefix)) {
            g_hProcess = reinterpret_cast<HANDLE>(std::wcstoull(&arg[ARRAYSIZE(pwszArgPrefix) - 1], nullptr, 0));
        } else if (constexpr wchar_t pwszArgPrefix[] = L"--exception-info-pipe-read-handle="; arg.starts_with(pwszArgPrefix)) {
            hPipeRead = reinterpret_cast<HANDLE>(std::wcstoull(&arg[ARRAYSIZE(pwszArgPrefix) - 1], nullptr, 0));
        } else if (constexpr wchar_t pwszArgPrefix[] = L"--asset-directory="; arg.starts_with(pwszArgPrefix)) {
            g_assetsDirectory = arg.substr(ARRAYSIZE(pwszArgPrefix) - 1);
        } else if (constexpr wchar_t pwszArgPrefix[] = L"--log-directory="; arg.starts_with(pwszArgPrefix)) {
            logDir = arg.substr(ARRAYSIZE(pwszArgPrefix) - 1);
        } else if (arg == L"--") {
            launcherArgs.emplace();
        } else {
            std::wcerr << L"Invalid argument: " << arg << std::endl;
            return InvalidParameter;
        }
    }

    if (g_hProcess == nullptr) {
        std::wcerr << L"Target process not specified" << std::endl;
        return InvalidParameter;
    }

    if (hPipeRead == nullptr) {
        std::wcerr << L"Read pipe handle not specified" << std::endl;
        return InvalidParameter;
    }

    if (g_assetsDirectory.empty()) {
        std::wcerr << L"Assets directory not specified" << std::endl;
        return InvalidParameter;
    }

    const auto dwProcessId = GetProcessId(g_hProcess);
    if (!dwProcessId) {
        std::wcerr << L"Target process handle is invalid" << std::endl;
        return InvalidParameter;
    }

    while (true) {
        std::cout << "Waiting for crash...\n";

        exception_info exinfo;
        if (DWORD exsize{}; !ReadFile(hPipeRead, &exinfo, static_cast<DWORD>(sizeof exinfo), &exsize, nullptr) || exsize != sizeof exinfo) {
            if (WaitForSingleObject(g_hProcess, 0) == WAIT_OBJECT_0) {
                auto excode = static_cast<DWORD>(ProcessExitedUnknownExitCode);
                if (!GetExitCodeProcess(g_hProcess, &excode))
                    std::cerr << std::format("Process exited, but failed to read exit code; error: 0x{:x}", GetLastError()) << std::endl;
                else
                    std::cout << std::format("Process exited with exit code {0} (0x{0:x})", excode) << std::endl;
                break;
            }

            const auto err = GetLastError();
            std::cerr << std::format("Failed to read exception information; error: 0x{:x}", err) << std::endl;
            std::cerr << "Terminating target process." << std::endl;
            TerminateProcess(g_hProcess, -1);
            break;
        }

        if (exinfo.ExceptionRecord.ExceptionCode == 0x12345678) {
            std::cout << "Restart requested" << std::endl;
            TerminateProcess(g_hProcess, 0);
            restart_game_using_injector(IdRadioRestartNormal, *launcherArgs);
            break;
        }

        std::cout << "Crash triggered" << std::endl;

        try_attach_xclr();
        attach_debugger();

        std::wstring stackTrace(exinfo.dwStackTraceLength, L'\0');
        if (exinfo.dwStackTraceLength) {
            if (DWORD read; !ReadFile(hPipeRead, &stackTrace[0], 2 * exinfo.dwStackTraceLength, &read, nullptr)) {
                std::cout << std::format("Failed to read supplied stack trace: error 0x{:x}", GetLastError()) << std::endl;
            }
        }

        SYSTEMTIME st;
        GetLocalTime(&st);
        const auto dumpPath = logDir.empty() ? std::filesystem::path() : logDir / std::format("dalamud_appcrash_{:04}{:02}{:02}_{:02}{:02}{:02}_{:03}_{}.dmp", st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, st.wMilliseconds, dwProcessId);
        const auto logPath = logDir.empty() ? std::filesystem::path() : logDir / std::format("dalamud_appcrash_{:04}{:02}{:02}_{:02}{:02}{:02}_{:03}_{}.log", st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, st.wMilliseconds, dwProcessId);
        std::wstring dumpError;
        if (dumpPath.empty()) {
            std::cout << "Skipping dump path, as log directory has not been specified" << std::endl;
        } else {
            MINIDUMP_EXCEPTION_INFORMATION mdmp_info{};
            mdmp_info.ThreadId = GetThreadId(exinfo.hThreadHandle);
            mdmp_info.ExceptionPointers = exinfo.pExceptionPointers;
            mdmp_info.ClientPointers = TRUE;

            do {
                const auto hDumpFile = CreateFileW(dumpPath.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr);
                if (hDumpFile == INVALID_HANDLE_VALUE) {
                    std::wcerr << (dumpError = std::format(L"CreateFileW({}, GENERIC_READ | GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr) error: 0x{:x}", dumpPath.wstring(), GetLastError())) << std::endl;
                    break;
                }

                std::unique_ptr<std::remove_pointer_t<HANDLE>, decltype(&CloseHandle)> hDumpFilePtr(hDumpFile, &CloseHandle);
                if (false && !MiniDumpWriteDump(g_hProcess, dwProcessId, hDumpFile, static_cast<MINIDUMP_TYPE>(MiniDumpWithDataSegs | MiniDumpWithModuleHeaders), &mdmp_info, nullptr, nullptr)) {
                    std::wcerr << (dumpError = std::format(L"MiniDumpWriteDump(0x{:x}, {}, 0x{:x}({}), MiniDumpWithFullMemory, ..., nullptr, nullptr) error: 0x{:x}", reinterpret_cast<size_t>(g_hProcess), dwProcessId, reinterpret_cast<size_t>(hDumpFile), dumpPath.wstring(), GetLastError())) << std::endl;
                    break;
                }

                std::wcout << "Dump written to path: " << dumpPath << std::endl;
            } while (false);
        }

        std::wostringstream log;
        log << std::format(L"Unhandled native exception occurred at {}", to_address_string(exinfo.ContextRecord.Rip, false)) << std::endl;
        log << std::format(L"Code: {:X}", exinfo.ExceptionRecord.ExceptionCode) << std::endl;
        if (dumpPath.empty())
            log << L"Dump skipped" << std::endl;
        else if (dumpError.empty())
            log << std::format(L"Dump at: {}", dumpPath.wstring()) << std::endl;
        else
            log << std::format(L"Dump error: {}", dumpError) << std::endl;
        log << L"Time: " << std::chrono::zoned_time{ std::chrono::current_zone(), std::chrono::system_clock::now() } << std::endl;
        log << L"\n" << stackTrace << std::endl;

        print_exception_info(exinfo.hThreadHandle, exinfo.ExceptionPointers, exinfo.ContextRecord, log);
        auto window_log_str = log.str();
        print_exception_info_extended(exinfo.ExceptionPointers, exinfo.ContextRecord, log);

        std::wofstream(logPath) << log.str();

        std::thread submitThread;
        if (!getenv("DALAMUD_NO_METRIC")) {
            auto url = std::format(L"/Dalamud/Metric/ReportCrash/?lt={}&code={:x}", exinfo.nLifetime, exinfo.ExceptionRecord.ExceptionCode);

            submitThread = std::thread([url = std::move(url)]{
                const auto hInternet = WinHttpOpen(L"DALAMUDCRASHHANDLER", WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY, nullptr, nullptr, WINHTTP_FLAG_SECURE_DEFAULTS);
                const auto hConnect = !hInternet ? nullptr : WinHttpConnect(hInternet, L"kamori.goats.dev", INTERNET_DEFAULT_HTTPS_PORT, 0);
                const auto hRequest = !hConnect ? nullptr : WinHttpOpenRequest(hConnect, L"GET", url.c_str(), nullptr, nullptr, nullptr, 0);
                const auto bSent = !hRequest
                                       ? false
                                       : WinHttpSendRequest(hRequest,
                                                            WINHTTP_NO_ADDITIONAL_HEADERS,
                                                            0, WINHTTP_NO_REQUEST_DATA, 0,
                                                            0, 0);

                if (!bSent)
                    std::cerr << std::format("Failed to send metric: 0x{:x}", GetLastError()) << std::endl;

                if (hRequest) WinHttpCloseHandle(hRequest);
                if (hConnect) WinHttpCloseHandle(hConnect);
                if (hInternet) WinHttpCloseHandle(hInternet);
                });
        }

        TASKDIALOGCONFIG config = { 0 };

        const TASKDIALOG_BUTTON radios[]{
            {IdRadioRestartNormal, L"Restart"},
            {IdRadioRestartWithout3pPlugins, L"Restart without 3rd party plugins"},
            {IdRadioRestartWithoutPlugins, L"Restart without any plugins"},
            {IdRadioRestartWithoutDalamud, L"Restart without Dalamud"},
        };

        const TASKDIALOG_BUTTON buttons[]{
            {IdButtonRestart, L"Restart\nRestart the game, optionally without plugins or Dalamud."},
            {IdButtonExit, L"Exit\nExit the game."},
        };

        config.cbSize = sizeof(config);
        config.hInstance = GetModuleHandleW(nullptr);
        config.dwFlags = TDF_ENABLE_HYPERLINKS | TDF_CAN_BE_MINIMIZED | TDF_ALLOW_DIALOG_CANCELLATION | TDF_USE_COMMAND_LINKS;
        config.pszMainIcon = MAKEINTRESOURCE(IDI_ICON1);
        config.pszMainInstruction = L"An error occurred";
        config.pszContent = (L""
            R"aa(This may be caused by a faulty plugin, a broken TexTools modification, any other third-party tool, or simply a bug in the game.)aa" "\n"
            "\n"
            R"aa(Try running integrity check in the XIVLauncher settings, and disabling plugins you don't need.)aa"
            );
        config.pButtons = buttons;
        config.cButtons = ARRAYSIZE(buttons);
        config.nDefaultButton = IdButtonRestart;
        config.pszExpandedInformation = window_log_str.c_str();
        config.pszWindowTitle = L"Dalamud Error";
        config.pRadioButtons = radios;
        config.cRadioButtons = ARRAYSIZE(radios);
        config.nDefaultRadioButton = IdRadioRestartNormal;
        config.cxWidth = 300;
        config.pszFooter = (L""
            R"aa(<a href="help">Help</a> | <a href="logdir">Open log directory</a> | <a href="logfile">Open log file</a> | <a href="resume">Attempt to resume</a>)aa"
            );

        // Can't do this, xiv stops pumping messages here
        //config.hwndParent = FindWindowA("FFXIVGAME", NULL);

        auto attemptResume = false;
        const auto callback = [&](HWND hwnd, UINT uNotification, WPARAM wParam, LPARAM lParam) -> HRESULT {
            switch (uNotification) {
                case TDN_CREATED:
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    return S_OK;
                }
                case TDN_HYPERLINK_CLICKED:
                {
                    const auto link = std::wstring_view(reinterpret_cast<const wchar_t*>(lParam));
                    if (link == L"help") {
                        ShellExecuteW(hwnd, nullptr, L"https://goatcorp.github.io/faq?utm_source=vectored", nullptr, nullptr, SW_SHOW);
                    } else if (link == L"logdir") {
                        ShellExecuteW(hwnd, nullptr, L"explorer.exe", escape_shell_arg(std::format(L"/select,{}", logPath.wstring())).c_str(), nullptr, SW_SHOW);
                    } else if (link == L"logfile") {
                        ShellExecuteW(hwnd, nullptr, logPath.c_str(), nullptr, nullptr, SW_SHOW);
                    } else if (link == L"resume") {
                        attemptResume = true;
                        DestroyWindow(hwnd);
                    }
                    return S_OK;
                }
            }

            return S_OK;
        };

        config.pfCallback = [](HWND hwnd, UINT uNotification, WPARAM wParam, LPARAM lParam, LONG_PTR dwRefData) {
            return (*reinterpret_cast<decltype(callback)*>(dwRefData))(hwnd, uNotification, wParam, lParam);
        };
        config.lpCallbackData = reinterpret_cast<LONG_PTR>(&callback);

        if (submitThread.joinable()) {
            submitThread.join();
            submitThread = {};
        }

        int nButtonPressed = 0, nRadioButton = 0;
        if (FAILED(TaskDialogIndirect(&config, &nButtonPressed, &nRadioButton, nullptr))) {
            ResumeThread(exinfo.hThreadHandle);
        } else {
            switch (nButtonPressed) {
                case IdButtonRestart:
                {
                    TerminateProcess(g_hProcess, exinfo.ExceptionRecord.ExceptionCode);
                    restart_game_using_injector(nRadioButton, *launcherArgs);
                    break;
                }
                default:
                    if (attemptResume)
                        ResumeThread(exinfo.hThreadHandle);
                    else
                        TerminateProcess(g_hProcess, exinfo.ExceptionRecord.ExceptionCode);
            }
        }
    }

    return 0;
}
