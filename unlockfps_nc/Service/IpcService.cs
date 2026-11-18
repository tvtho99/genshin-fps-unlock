using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using unlockfps_nc.Model;
using unlockfps_nc.Utility;

namespace unlockfps_nc.Service
{
    public enum IpcStatus
    {
        None = 0,
        Error,
        Ready
    }

    public class IpcService(ConfigService configService) : IDisposable
    {
        internal const int StatusOffset = 0, FramerateOffset = 4, PowerSaveOffset = 8, UseMobileUIOffset = 9;

        private MemoryMappedFile? _sharedMemory = null;
        private MemoryMappedViewAccessor? _sharedMemoryAccessor = null;
        // Kept for the service lifetime: the game loads the stub by path whenever the hook fires,
        // so the file must stay locked against replacement the whole time.
        private StubFile? _stubFile;
        private ModuleGuard _stubModule = IntPtr.Zero;
        private IntPtr _wndHook = IntPtr.Zero;

        public bool Start(int processId)
        {
            _sharedMemory ??= MemoryMappedFile.CreateOrOpen(@"Global\2DE95FDC-6AB7-4593-BFE6-760DD4AB422B", 4096, MemoryMappedFileAccess.ReadWrite);
            _sharedMemoryAccessor ??= _sharedMemory.CreateViewAccessor();
            if (_sharedMemoryAccessor == null) {
                MessageBox.Show(@"Failed to create shared memory.", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            Update();

            var status = ReadStatus(_sharedMemoryAccessor);
            if (status == IpcStatus.Ready)
                return true;
            if (status == IpcStatus.Error)
                return false;

            var targetWindow = ProcessUtils.GetWindowFromProcessId(processId);
            var threadId = Native.GetWindowThreadProcessId(targetWindow, out uint _);
            if (threadId == 0)
                return false;

            if (!_stubModule)
            {
                try {
                    _stubFile ??= StubFile.ExtractEmbedded(AppContext.BaseDirectory);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) {
                    MessageBox.Show($@"Failed to extract UnlockerStub.dll:{Environment.NewLine}{e.Message}", @"Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                _stubModule = Native.LoadLibrary(_stubFile.Path);
                if (!_stubModule)
                {
                    string errorMessage = $@"Failed to load stub module: {Marshal.GetLastWin32Error()}{Environment.NewLine}{Marshal.GetLastPInvokeErrorMessage()}";
                    MessageBox.Show(errorMessage, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            var stubWndProc = Native.GetProcAddress(_stubModule, "WndProc");
            if (_wndHook != IntPtr.Zero)
                Native.UnhookWindowsHookEx(_wndHook);

            _wndHook = Native.SetWindowsHookEx(3, stubWndProc, _stubModule, threadId);
            if (_wndHook == IntPtr.Zero)
            {
                string errorMessage = $@"Failed to set window hook: {Marshal.GetLastWin32Error()}{Environment.NewLine}{Marshal.GetLastPInvokeErrorMessage()}";
                MessageBox.Show(errorMessage, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (!Native.PostThreadMessage(threadId, 0, IntPtr.Zero, IntPtr.Zero))
            {
                string errorMessage = $@"Failed to post thread message: {Marshal.GetLastWin32Error()}{Environment.NewLine}{Marshal.GetLastPInvokeErrorMessage()}";
                MessageBox.Show(errorMessage, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            int retryCount = 0;
            while (true) {
                status = ReadStatus(_sharedMemoryAccessor);

                if (status == IpcStatus.Ready)
                    break;

                if (status == IpcStatus.Error)
                    return false;

                if (retryCount >= 20) {
                    MessageBox.Show(@"Failed to start the unlocker.", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                retryCount++;
                Thread.Sleep(1000);
            }

            return true;
        }

        public void OnGameExit()
        {
            _sharedMemoryAccessor?.Write(StatusOffset, (int)IpcStatus.None);
        }

        public void Update()
        {
            if (_sharedMemoryAccessor != null)
                WriteSettings(_sharedMemoryAccessor, configService.Config);
        }

        internal static void WriteSettings(MemoryMappedViewAccessor accessor, Config config)
        {
            accessor.Write(FramerateOffset, config.FPSTarget);
            accessor.Write(PowerSaveOffset, config.UsePowerSave);
            accessor.Write(UseMobileUIOffset, config.UseMobileUI);
        }

        internal static IpcStatus ReadStatus(MemoryMappedViewAccessor accessor)
            => (IpcStatus)accessor.ReadInt32(StatusOffset);

        public void Dispose()
        {
            if (_wndHook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_wndHook);
                _wndHook = IntPtr.Zero;
            }

            _sharedMemoryAccessor?.Dispose();
            _sharedMemory?.Dispose();
            _stubModule.Dispose();
            _stubFile?.Dispose();
        }
    }
}
