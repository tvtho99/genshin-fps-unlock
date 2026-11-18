using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace unlockfps_nc.Utility
{
    internal class ProcessUtils
    {
        public static string GetProcessPath(IntPtr hProcess)
        {
            if (hProcess == IntPtr.Zero)
                return string.Empty;

            StringBuilder sb = new StringBuilder(1024);
            uint bufferSize = (uint)sb.Capacity;
            if (!Native.QueryFullProcessImageName(hProcess, 0, sb, ref bufferSize))
                return string.Empty;

            return sb.ToString();
        }

        public static IntPtr GetWindowFromProcessId(int processId)
        {
            IntPtr windowHandle = IntPtr.Zero;

            Native.EnumWindows((hWnd, lParam) =>
            {
                Native.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == processId)
                {
                    windowHandle = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return windowHandle;
        }

        public static bool InjectDlls(IntPtr processHandle, List<string> dllPaths)
        {
#if !RELEASEMIN
            if (dllPaths.Count == 0)
                return true;

            var kernel32 = Native.GetModuleHandle("kernel32.dll");
            if (kernel32 == IntPtr.Zero)
                return false;

            var loadLibrary = Native.GetProcAddress(kernel32, "LoadLibraryW");
            if (loadLibrary == IntPtr.Zero)
                return false;

            const int bufferSize = 0x1000;
            const uint waitObject0 = 0;
            var remoteVa = Native.VirtualAllocEx(processHandle, IntPtr.Zero, bufferSize,
                AllocationType.COMMIT | AllocationType.RESERVE, MemoryProtection.READWRITE);
            if (remoteVa == IntPtr.Zero)
                return false;

            var canFreeRemotePath = true;
            try
            {
                foreach (var dllPath in dllPaths)
                {
                    if (Encoding.Unicode.GetByteCount(dllPath) > bufferSize - sizeof(char))
                        return false;

                    var bytes = Encoding.Unicode.GetBytes(dllPath + '\0');
                    if (!Native.WriteProcessMemory(processHandle, remoteVa, bytes, bytes.Length, out var bytesWritten) ||
                        bytesWritten != bytes.Length)
                        return false;

                    var thread = Native.CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibrary, remoteVa, 0, out _);
                    if (thread == IntPtr.Zero)
                        return false;

                    canFreeRemotePath = false;
                    try
                    {
                        canFreeRemotePath = Native.WaitForSingleObject(thread, 10000) == waitObject0;
                        if (!canFreeRemotePath || !Native.GetExitCodeThread(thread, out var exitCode) || exitCode == 0)
                            return false;
                    }
                    finally
                    {
                        Native.CloseHandle(thread);
                    }
                }
            }
            finally
            {
                // An unsignaled thread may still read the path after the wait returns.
                if (canFreeRemotePath)
                    Native.VirtualFreeEx(processHandle, remoteVa, 0, FreeType.RELEASE);
            }
#endif
            return true;
        }

        public static bool IsWindowDrawing(IntPtr hWnd)
        {
            if (!Native.IsWindowVisible(hWnd))
                return false;

            Native.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, 0x122); // RDW_INTERNALPAINT | RDW_NOERASE | RDW_UPDATENOW
            Native.UpdateWindow(hWnd);

            var hdc = Native.GetDC(hWnd);
            if (hdc == IntPtr.Zero)
                return false;

            Native.ReleaseDC(hWnd, hdc);
            return true;
        }

    }
}
