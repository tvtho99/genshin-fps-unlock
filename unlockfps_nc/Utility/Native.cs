using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace unlockfps_nc.Utility
{
    internal class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowsHookEx(int idHook, IntPtr lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll")]
        public static extern void FreeLibrary(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        public static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    }

    internal class ModuleGuard(IntPtr module) : IDisposable
    {
        public IntPtr BaseAddress { get => module & ~3; }

        public static implicit operator ModuleGuard(IntPtr module) => new(module);
        public static implicit operator IntPtr(ModuleGuard guard) => guard.BaseAddress;
        public static implicit operator bool(ModuleGuard guard) => guard.BaseAddress != IntPtr.Zero;

        public void Dispose()
        {
            if (this)
                Native.FreeLibrary(module);
        }
    }

    internal static class ProcessAccess
    {
        public const uint TERMINATE = 0x0001;
        public const uint CREATE_THREAD = 0x0002;
        public const uint SET_SESSIONID = 0x0004;
        public const uint VM_OPERATION = 0x0008;
        public const uint VM_READ = 0x0010;
        public const uint VM_WRITE = 0x0020;
        public const uint DUP_HANDLE = 0x0040;
        public const uint CREATE_PROCESS = 0x0080;
        public const uint SET_QUOTA = 0x0100;
        public const uint SET_INFORMATION = 0x0200;
        public const uint QUERY_INFORMATION = 0x0400;
        public const uint SUSPEND_RESUME = 0x0800;
        public const uint QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint SET_LIMITED_INFORMATION = 0x2000;
        public const uint ALL_ACCESS = 0x1FFFFF;
    }
    
    internal static class StandardAccess
    {
        public const uint DELETE = 0x00010000;
        public const uint READ_CONTROL = 0x00020000;
        public const uint WRITE_DAC = 0x00040000;
        public const uint WRITE_OWNER = 0x00080000;
        public const uint SYNCHRONIZE = 0x00100000;
        public const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        public const uint STANDARD_RIGHTS_READ = READ_CONTROL;
        public const uint STANDARD_RIGHTS_WRITE = READ_CONTROL;
        public const uint STANDARD_RIGHTS_EXECUTE = READ_CONTROL;
        public const uint STANDARD_RIGHTS_ALL = 0x001F0000;
        public const uint SPECIFIC_RIGHTS_ALL = 0x0000FFFF;
    }

    internal static class AllocationType
    {
        public const uint COMMIT = 0x1000;
        public const uint RESERVE = 0x2000;
        public const uint RESET = 0x80000;
        public const uint LARGE_PAGES = 0x20000000;
        public const uint PHYSICAL = 0x400000;
        public const uint TOP_DOWN = 0x100000;
        public const uint WRITE_WATCH = 0x200000;
        public const uint RESET_UNDO = 0x1000000;
    }

    internal static class FreeType
    {
        public const uint DECOMMIT = 0x4000;
        public const uint RELEASE = 0x8000;
    }

    internal static class MemoryProtection
    {
        public const uint EXECUTE = 0x10;
        public const uint EXECUTE_READ = 0x20;
        public const uint EXECUTE_READWRITE = 0x40;
        public const uint EXECUTE_WRITECOPY = 0x80;
        public const uint NOACCESS = 0x01;
        public const uint READONLY = 0x02;
        public const uint READWRITE = 0x04;
        public const uint WRITECOPY = 0x08;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }
}
