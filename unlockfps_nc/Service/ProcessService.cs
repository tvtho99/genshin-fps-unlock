using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using unlockfps_nc.Model;
using unlockfps_nc.Utility;

namespace unlockfps_nc.Service
{
    public class ProcessService
    {
        private readonly CancellationTokenSource _cts = new();
        private IntPtr _gameHandle = IntPtr.Zero;
        private int _gamePid = 0;
        private int _failedPid;

        private readonly Config _config;

        private readonly IpcService _ipcService;

        public ProcessService(ConfigService configService, IpcService ipcService)
        {
            _config = configService.Config;
            _ipcService = ipcService;

            _ = Task.Run(UnlockerPoll, _cts.Token);
        }

        public bool StartGame()
        {
            if (!File.Exists(_config.GamePath)) {
                MessageBox.Show(@"Game path is invalid.", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (IsGameRunning()) {
                MessageBox.Show(@"An instance of the game is already running.", @"Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            if (_gameHandle != IntPtr.Zero) {
                Native.CloseHandle(_gameHandle);
                _gameHandle = IntPtr.Zero;
            }

            if (_config.UseHDR) {
                var subKeyName = Path.GetFileName(_config.GamePath) == "YuanShen.exe" ? "原神" : "Genshin Impact";
                try {
                    using var key = Registry.CurrentUser.CreateSubKey($@"Software\miHoYo\{subKeyName}");
                    key.SetValue("WINDOWS_HDR_ON_h3132281285", 1);
                }
                catch(Exception e) {
                    MessageBox.Show($@"Failed to enable HDR: {e.Message}", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            STARTUPINFO si = new();
            si.cb = Marshal.SizeOf<STARTUPINFO>();
            uint creationFlag = _config.SuspendLoad ? 4u : 0u;
            var gameFolder = Path.GetDirectoryName(_config.GamePath);

            if (!Native.CreateProcess(_config.GamePath, BuildCommandLine(_config), IntPtr.Zero, IntPtr.Zero, false, creationFlag, IntPtr.Zero, gameFolder, ref si, out var pi)) {
                MessageBox.Show(
                    $@"CreateProcess failed ({Marshal.GetLastWin32Error()}){Environment.NewLine} {Marshal.GetLastPInvokeErrorMessage()}",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            Native.SetPriorityClass(pi.hProcess, PriorityClassFor(_config.Priority));

            if (!ProcessUtils.InjectDlls(pi.hProcess, _config.DllList)) {
                MessageBox.Show(
                    $@"Dll Injection failed ({Marshal.GetLastWin32Error()}){Environment.NewLine} {Marshal.GetLastPInvokeErrorMessage()}",
                    @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (_config.SuspendLoad)
                Native.ResumeThread(pi.hThread);

            _gamePid = pi.dwProcessId;
            _gameHandle = pi.hProcess;

            Native.CloseHandle(pi.hThread);
            return true;
        }

        public void OnFormClosing()
        {
            _cts.Cancel();
            Native.CloseHandle(_gameHandle);
        }

        private bool IsGameRunning()
        {
            if (_gameHandle == IntPtr.Zero)
                return false;

            if (!Native.GetExitCodeProcess(_gameHandle, out var exitCode))
                return false;

            return exitCode == 259; // STILL_ACTIVE
        }

        private async Task UnlockerPoll()
        {
            while (!_cts.IsCancellationRequested) {
                try {
                    await Task.Delay(1000, _cts.Token);
                    var processes = Process.GetProcesses();
                    if (_failedPid != 0) {
                        if (processes.Any(candidate => candidate.Id == _failedPid))
                            continue;

                        _failedPid = 0;
                        _ipcService.OnGameExit();
                    }

                    using var process = processes
                        .FirstOrDefault(x => x is { ProcessName: "GenshinImpact" } or { ProcessName: "YuanShen" });
                    if (process == null)
                        continue;

                    while (!_cts.IsCancellationRequested && !process.HasExited
                        && !ProcessUtils.IsWindowDrawing(process.MainWindowHandle))
                        await Task.Delay(1000, _cts.Token);

                    if (process.HasExited || _cts.IsCancellationRequested)
                        continue;

                    if (!_ipcService.Start(process.Id)) {
                        _failedPid = process.Id;
                        continue;
                    }

                    while (!process.HasExited && !_cts.IsCancellationRequested) {
                        _ipcService.Update();
                        await Task.Delay(62, _cts.Token);
                    }

                    if (_gameHandle != IntPtr.Zero && _config.AutoClose) {
                        Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.BeginInvoke(Application.Exit);
                    }

                    _ipcService.OnGameExit();
                    await Task.Delay(5000, _cts.Token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) {
                    Debug.WriteLine(ex);
                }
            }
        }

        internal static uint PriorityClassFor(int priority) => priority switch
        {
            0 => 0x100u,
            1 => 0x80u,
            2 => 0x8000u,
            3 => 0x20u,
            4 => 0x4000u,
            5 => 0x40u,
            _ => 0x20u,
        };

        internal static string BuildCommandLine(Config config)
        {
            string commandLine = $"\"{config.GamePath}\" ";
            if (config.PopupWindow)
                commandLine += "-popupwindow ";

            if (config.UseCustomRes)
                commandLine += $"-screen-width {config.CustomResX} -screen-height {config.CustomResY} ";

            commandLine += $"-screen-fullscreen {(config.Fullscreen ? 1 : 0)} ";
            if (config.Fullscreen)
                commandLine += $"-window-mode {(config.IsExclusiveFullscreen ? "exclusive" : "borderless")} ";

            commandLine += $"-monitor {config.MonitorNum} ";
            commandLine += $"{config.AdditionalCommandLine} ";
            return commandLine;
        }

    }
}
