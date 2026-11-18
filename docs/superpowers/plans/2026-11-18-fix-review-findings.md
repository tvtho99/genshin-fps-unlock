# Fix Review Findings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa toàn bộ lỗi HIGH/MEDIUM/LOW tìm được trong lần review repo ngày 2026-11-18, kèm test cho mọi logic thuần.

**Architecture:** Giữ nguyên kiến trúc hiện có, gồm launcher C# (`unlockfps_nc`) và stub C++ (`UnlockerStub`) giao tiếp qua shared memory `Global\2DE95FDC-6AB7-4593-BFE6-760DD4AB422B`. Logic thuần được tách thành hàm `internal static` để test được. Native có thêm một self-test exe bằng `assert`.

**Tech Stack:** .NET 8 WinForms (x64), xUnit; C++23 (MinGW g++ 16 / MSVC v143), Zydis amalgamated.

**Spec:** Bản review trong hội thoại ngày 2026-11-18 (các finding #1–#15 và nhóm LOW). Bảng ánh xạ finding sang task nằm ở cuối file.

## Global Constraints

- Target giữ nguyên `net8.0-windows`, `PlatformTarget x64`. Không thêm package runtime mới; chỉ project test được thêm xUnit.
- Cấu hình `ReleaseMin` phải tiếp tục loại bỏ hoàn toàn tính năng inject DLL (`#if !RELEASEMIN`).
- Giữ nguyên tên shared memory, tên file `fps_config.json` và tên property JSON, để config cũ vẫn đọc được.
- Layout IPC chốt cứng như sau: `Status` int32 @0, `Framerate` int32 @4, `PowerSave` bool(1 byte) @8, `UseMobileUI` bool(1 byte) @9. Phía C# và C++ phải khớp layout này.
- FPS hợp lệ trong khoảng **10–1000** ở mọi nơi: C# Sanitize, UI và stub.
- Mỗi task kết thúc bằng một commit theo dạng conventional commit. Stage file theo đúng đường dẫn, không dùng `git add -A`.
- Các file build/config dùng chung (`*.csproj`, `*.sln`, `GNUmakefile`, `.gitignore`) chỉ do orchestrator sửa.

## Review Focus

1. **Mở lại unlocker khi game đang chạy:** unlocker phải attach lại ngay mà không treo 20 giây. Task 4 có test `WriteSettings_PreservesStatus`; cần kiểm tra tay thêm ở Task 11.
2. **Game bị tắt khi đang load (cửa sổ chưa hiện):** poller vẫn phải phát hiện được lần mở game kế tiếp. Kiểm tra tay ở Task 11.
3. **Đường dẫn game có dấu cách** (mặc định của HoYoPlay: `C:\Program Files\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe`): Task 5 có test `BuildCommandLine_QuotesPathWithSpaces`.
4. **Config bị hỏng hoặc sửa tay** (JSON lỗi, `"DllList": null`, FPS ngoài khoảng): app vẫn phải khởi động với giá trị mặc định. Có test ở Task 8.
5. **File `UnlockerStub.dll` bị process khác khoá hoặc thay thế:** phải báo lỗi rõ ràng, tuyệt đối không nạp file lạ. Có test ở Task 6.

## Phân việc (orchestrator mode)

- **Mình làm:** Task 1, 2 (file build/config dùng chung), Task 3 (native hook/unsafe), Task 6, 7 (bảo mật), Task 11 (verify và review).
- **codex ×5:**
  - Wave 1 (chạy song song sau Task 2): Task 4, 5, 8, 9.
  - Wave 2 (sau khi wave 1 merge xong): Task 10.
- Task 6 sửa `IpcService.cs` nên phải chạy **sau** khi Task 4 merge.

| Wave | Task | Người làm | File chính |
|---|---|---|---|
| 0 | 1, 2 | mình | GNUmakefile, .gitignore, csproj, sln |
| 1 | 3 | mình | UnlockerStub/* |
| 1 | 4, 5, 8, 9 | codex | IpcService, ProcessService, ConfigService, Forms/ProcessUtils |
| 1b | 6, 7 | mình | StubFile, UrlPolicy, MainForm, IpcService (sau Task 4), Program |
| 2 | 10 | codex | Native.cs, ProcessUtils.cs (dọn dẹp) |
| 3 | 11 | mình | verify toàn bộ |

---

### Task 1: Build native bằng MinGW (finding #4)

**Files:**
- Modify: `UnlockerStub/GNUmakefile`
- Modify: `GNUmakefile` (sửa typo `UnlockerStub.dll.dll` trong `clean`)
- Modify: `.gitignore` (thêm `UnlockerStub/*.o`, `UnlockerStub/*.dll`, `UnlockerStub/*.a`, `UnlockerStub/*.exe`, `unlockfps_nc/Resources/UnlockerStub.dll`)

**Interfaces:**
- Produces: `mingw32-make -C UnlockerStub` sinh ra `UnlockerStub/UnlockerStub.dll` export `WndProc`; target `test` chạy self-test (Task 3 thêm file test).

- [ ] **Step 1: Chạy build hiện tại để thấy lỗi**

Run: `mingw32-make -C UnlockerStub`
Expected: FAIL. Hoặc báo undefined reference tới `Utils::` hoặc `Zydis`, hoặc lỗi compile do chưa bật C++23.

- [ ] **Step 2: Viết lại `UnlockerStub/GNUmakefile`**

Biến cần đặt:
- `CXX=x86_64-w64-mingw32-g++`, `CC=x86_64-w64-mingw32-gcc`
- `CPPFLAGS=-DNDEBUG -DNOMINMAX -DZYDIS_STATIC_BUILD -DZYCORE_STATIC_BUILD`
- `CXXFLAGS=-std=c++23 -O2`, `CFLAGS=-O2`
- `OBJS=dllmain.o Utils.o Zydis.o`
- Link: `-shared -static -static-libgcc -static-libstdc++ -Wl,--out-implib,libUnlockerStub.a -lntdll`

Target `test` build ra `utils_selftest.exe` từ `tests/utils_selftest.cpp Utils.cpp` rồi chạy file đó.

- [ ] **Step 3: Build và kiểm tra export**

Run: `mingw32-make -C UnlockerStub && x86_64-w64-mingw32-objdump -p UnlockerStub/UnlockerStub.dll | grep -E "WndProc|DLL Name"`
Expected: có `WndProc`. Phần DLL Name **không** được có `libstdc++-6.dll` hay `libgcc_s_seh-1.dll`.

- [ ] **Step 4: Copy DLL vào Resources để project C# build được**

Run: `cp UnlockerStub/UnlockerStub.dll unlockfps_nc/Resources/ && dotnet build unlockfps_nc/unlockfps_nc.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add UnlockerStub/GNUmakefile GNUmakefile .gitignore
git commit -m "fix: make MinGW build of UnlockerStub link all sources"
```

---

### Task 2: Project test xUnit

**Files:**
- Create: `unlockfps_nc.Tests/unlockfps_nc.Tests.csproj`
- Create: `unlockfps_nc.Tests/SmokeTests.cs`
- Modify: `unlockfps_nc/unlockfps_nc.csproj` (thêm `<InternalsVisibleTo Include="unlockfps_nc.Tests" />`, xoá dòng `<None Remove="dm34zubt.k4i~" />`)
- Modify: `unlockfps_nc.sln` (`dotnet sln add`)

**Interfaces:**
- Produces: `dotnet test unlockfps_nc.Tests` chạy được. Các task sau thêm file `*Tests.cs` vào thư mục này và không sửa csproj.

- [ ] **Step 1: Tạo csproj test**

Cấu hình: `net8.0-windows`, `UseWindowsForms=true`, `PlatformTarget=x64`, `Nullable=enable`, `ImplicitUsings=enable`. Package: `Microsoft.NET.Test.Sdk 17.11.1`, `xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2`. Thêm `ProjectReference ../unlockfps_nc/unlockfps_nc.csproj`.

- [ ] **Step 2: Viết smoke test `Version_IsPositive` (`Assert.True(Program.Version > 0)`) rồi chạy**

Run: `dotnet test unlockfps_nc.Tests`
Expected: `Passed: 1`.

- [ ] **Step 3: Commit**

```bash
git add unlockfps_nc.Tests/unlockfps_nc.Tests.csproj unlockfps_nc.Tests/SmokeTests.cs unlockfps_nc/unlockfps_nc.csproj unlockfps_nc.sln
git commit -m "test: add xUnit test project"
```

---

### Task 3: Sửa stub native (finding #6, #7, #8, #9 và các LOW native)

**Files:**
- Modify: `UnlockerStub/Utils.h`, `UnlockerStub/Utils.cpp`
- Modify: `UnlockerStub/dllmain.cpp`
- Create: `UnlockerStub/tests/utils_selftest.cpp`

**Interfaces:**
- Produces: `std::pair<uint64_t, uint64_t> Utils::ComputeAllocRange(uint64_t target, uint64_t sysMin, uint64_t sysMax)`, trả về `[lower, upper]` để mọi địa chỉ trong khoảng đều nằm trong tầm `rel32` tính từ `target`.
- Produces: `enum class IPCStatus : int32_t` (khớp layout trong Global Constraints).

- [ ] **Step 1: Viết self-test thất bại `tests/utils_selftest.cpp`**

```cpp
// main() với assert:
// 1. Range luôn nằm trong ±2GB và trong [sysMin, sysMax]
auto [lo, hi] = Utils::ComputeAllocRange(0x7FF6'0000'0000, 0x10000, 0x7FFF'FFFE'FFFF);
assert(lo == 0x7FF6'0000'0000 - 0x7FFFFF00 && hi == 0x7FF6'0000'0000 + 0x7FFFFF00);
// 2. target thấp: không underflow
auto [lo2, hi2] = Utils::ComputeAllocRange(0x40000000, 0x10000, 0x7FFF'FFFE'FFFF);
assert(lo2 == 0x10000 && hi2 == 0x40000000 + 0x7FFFFF00);
// 3. PatternScanAll không đọc vượt cuối buffer:
//    VirtualAlloc 2 page, page 2 đặt PAGE_NOACCESS, ghi "AA BB CC" vào 3 byte cuối page 1,
//    scan span = page 1 với pattern "11 22" -> không crash, kết quả rỗng.
//    Thêm pattern "AA BB" đặt ở offset 0x100 -> tìm thấy đúng 1 kết quả tại base+0x100.
// 4. GetModulePath(nullptr) kết thúc bằng L"utils_selftest.exe"
```

- [ ] **Step 2: Chạy test, phải fail (lúc này `ComputeAllocRange` chưa tồn tại)**

Run: `mingw32-make -C UnlockerStub test`
Expected: FAIL ở bước compile, báo `ComputeAllocRange` không phải member của `Utils`.

- [ ] **Step 3: Sửa trong `Utils.cpp`**

- `ComputeAllocRange`: `lower = max(target > 0x7FFFFF00 ? target - 0x7FFFFF00 : 0, sysMin)` và `upper = min(target + 0x7FFFFF00, sysMax)`.
- `AllocatePage`: bước nhảy dùng `sysInfo.dwAllocationGranularity` thay cho `size`, căn địa chỉ theo granularity, dùng range từ `ComputeAllocRange`.
- `PatternScanAll`: `const size_t scanLen = maskCount * 16;`, nếu `bytes.size() < scanLen` thì return rỗng; `end = data + bytes.size() - scanLen`. Kèm comment `// ponytail: matches in the last <16 bytes of the section are skipped; section tail is padding in practice`.
- `GetModulePath`: lặp khi `nChars == path.size()` thay vì dựa vào `GetLastError()`.

- [ ] **Step 4: Sửa trong `dllmain.cpp`**

- `IPCStatus : int32_t`.
- `SetupData`: sau vòng chase jmp, nếu `rip[0] != 0x89 || rip[1] != 0x0D` thì `ShowError(L"outdated pattern (unexpected instruction)")` và `return false`.
- `HookProc`:
  - Đổi signature thành `uint64_t HookProc(void* rcx)`, trả về đúng giá trị của hàm gốc.
  - Thêm `static std::atomic_flag fired`: thread vào sau chỉ gọi thẳng hàm gốc qua `HookHandle`.
  - Chỉ gọi `ChangeMobileUI` khi `offset > 0 && offset <= 0x1000 - sizeof(void*)`.
  - **Bỏ** `VirtualFree(HookHandle)` khỏi `HookProc` (race với thread đang chạy trong trampoline). Thay vào đó, `HookProc` ghi `HookFiredTick = GetTickCount64()`. Vòng lặp chính `ThreadProc` gọi `VirtualFree(HookHandle, 0, MEM_RELEASE)` một lần khi `HookFiredTick != 0 && GetTickCount64() - HookFiredTick > 10000`, giữ footprint bộ nhớ như code gốc.
- Kiểm tra lại `trampolineDisp`: nếu khoảng cách vượt `INT32_MAX` thì báo lỗi và không cài hook.
- Xoá `VectoredExceptionHandler`, `#include <DbgHelp.h>`, `#pragma comment(lib, "dbghelp.lib")` (code chết).

- [ ] **Step 5: Chạy self-test và build**

Run: `mingw32-make -C UnlockerStub clean all test`
Expected: build ra DLL; `utils_selftest.exe` exit 0, không có assert nào fail.

- [ ] **Step 6: Commit**

```bash
git add UnlockerStub/Utils.h UnlockerStub/Utils.cpp UnlockerStub/dllmain.cpp UnlockerStub/tests/utils_selftest.cpp
git commit -m "fix(stub): bound hook allocation to rel32 range and harden mobile UI hook"
```

---

### Task 4: IPC phía C# (finding #1, #2, LOW IPC) — codex

**Files:**
- Modify: `unlockfps_nc/Service/IpcService.cs`
- Test: `unlockfps_nc.Tests/IpcServiceTests.cs`

**Interfaces:**
- Produces: `internal static void IpcService.WriteSettings(MemoryMappedViewAccessor accessor, Config config)` (chỉ ghi offset 4, 8, 9).
- Produces: `internal static IpcStatus IpcService.ReadStatus(MemoryMappedViewAccessor accessor)` (đọc int32 @0).
- Produces: hằng `internal const int StatusOffset = 0, FramerateOffset = 4, PowerSaveOffset = 8, UseMobileUIOffset = 9;`
- Xoá struct `IpcData`; giữ enum `IpcStatus`.

- [ ] **Step 1: Viết test fail**

```csharp
[Fact] public void WriteSettings_PreservesStatus()
{
    using var mmf = MemoryMappedFile.CreateNew(null, 4096);
    using var acc = mmf.CreateViewAccessor();
    acc.Write(IpcService.StatusOffset, (int)IpcStatus.Ready);
    IpcService.WriteSettings(acc, new Config { FPSTarget = 144, UsePowerSave = true, UseMobileUI = true });
    Assert.Equal(IpcStatus.Ready, IpcService.ReadStatus(acc));
    Assert.Equal(144, acc.ReadInt32(4));
    Assert.True(acc.ReadBoolean(8));
    Assert.True(acc.ReadBoolean(9));
}
```

- [ ] **Step 2: Chạy `dotnet test unlockfps_nc.Tests --filter IpcServiceTests`**: phải FAIL (compile error).

- [ ] **Step 3: Sửa `IpcService`**

- `Update()` gọi `WriteSettings`. `OnGameExit()` chỉ ghi `Status = None` tại offset 0.
- `Start(int processId)`:
  - Nếu `threadId == 0` thì `return false`, không hiện MessageBox (poller sẽ thử lại).
  - Nạp stub module chỉ một lần (`if (!_stubModule)`).
  - Trước khi `SetWindowsHookEx` mới, nếu `_wndHook != IntPtr.Zero` thì gọi `Native.UnhookWindowsHookEx(_wndHook)` trước.
  - Đổi `Task.Delay(1000).Wait()` thành `Thread.Sleep(1000)`.
- `Dispose()` gọi `UnhookWindowsHookEx` nếu hook còn.

- [ ] **Step 4: Chạy test, phải PASS**

Run: `dotnet test unlockfps_nc.Tests --filter IpcServiceTests`
Expected: `Passed: 1`.

- [ ] **Step 5: Commit**: `fix(ipc): stop Update() from clobbering stub status; never install a global hook`

---

### Task 5: ProcessService (finding #3, #13, #15, LOW command line/STARTUPINFO) — codex

**Files:**
- Modify: `unlockfps_nc/Service/ProcessService.cs`
- Test: `unlockfps_nc.Tests/ProcessServiceTests.cs`

**Interfaces:**
- Consumes: `IpcService.Start(int) -> bool` (Task 4; giờ trả false để báo "thử lại sau").
- Produces: `internal static string ProcessService.BuildCommandLine(Config config)`.
- Produces: `internal static uint ProcessService.PriorityClassFor(int priority)`. Bảng map index sang priority class: `0→0x100, 1→0x80, 2→0x8000, 3→0x20, 4→0x4000, 5→0x40`, khớp thứ tự combo `Realtime, High, Above Normal, Normal, Below Normal, Low`.

- [ ] **Step 1: Viết test fail**

```csharp
[Fact] public void BuildCommandLine_QuotesPathWithSpaces()
{
    var cfg = new Config { GamePath = @"C:\Program Files\HoYoPlay\games\Genshin Impact game\GenshinImpact.exe" };
    var cmd = ProcessService.BuildCommandLine(cfg);
    Assert.StartsWith("\"C:\\Program Files\\HoYoPlay\\games\\Genshin Impact game\\GenshinImpact.exe\" ", cmd);
    Assert.Contains("-screen-fullscreen 1 -window-mode borderless", cmd);
}
[Fact] public void BuildCommandLine_CustomRes()
    => Assert.Contains("-screen-width 2560 -screen-height 1440",
        ProcessService.BuildCommandLine(new Config { GamePath = "a.exe", UseCustomRes = true, CustomResX = 2560, CustomResY = 1440 }));
[Theory, InlineData(0, 0x100u), InlineData(3, 0x20u), InlineData(5, 0x40u)]
public void PriorityClassFor_MapsComboIndex(int idx, uint expected) => Assert.Equal(expected, ProcessService.PriorityClassFor(idx));
```

- [ ] **Step 2: Chạy `dotnet test unlockfps_nc.Tests --filter ProcessServiceTests`**: phải FAIL.

- [ ] **Step 3: Sửa `ProcessService`**

- `StartGame`:
  - Gán `si.cb = Marshal.SizeOf<STARTUPINFO>()`.
  - Sau `CreateProcess` thì gọi `Native.SetPriorityClass(pi.hProcess, PriorityClassFor(_config.Priority))`.
  - Command line lấy từ `BuildCommandLine(_config)`.
- `UnlockerPoll`:
  - Vòng chờ cửa sổ thêm điều kiện `&& !process.HasExited`. Nếu game đã thoát thì `continue` vòng ngoài.
  - Khi `Start` trả false thì ghi nhớ `_failedPid = process.Id`, bỏ qua PID này tới khi nó thoát, **không `return`**.
  - Thân vòng lặp bọc `try/catch (Exception ex) when (ex is not OperationCanceledException)` để log vào `Debug.WriteLine` rồi tiếp tục.
  - `Application.Exit()` đổi thành `Application.OpenForms.OfType<MainForm>().FirstOrDefault()?.BeginInvoke(Application.Exit)`.

- [ ] **Step 4: Chạy test, phải PASS. Thêm `dotnet build -c ReleaseMin` để chắc không vỡ cấu hình min.**

- [ ] **Step 5: Commit**: `fix(process): quote game path, apply priority, keep poller alive after failures`

---

### Task 6: Nạp stub an toàn và kiểm tra URL cập nhật (finding #10, #11) — mình, sau Task 4

**Files:**
- Create: `unlockfps_nc/Service/StubFile.cs`
- Create: `unlockfps_nc/Utility/UrlPolicy.cs`
- Modify: `unlockfps_nc/Service/IpcService.cs` (thay `GetUnlockerStubPath`)
- Modify: `unlockfps_nc/MainForm.cs` (`CheckVersion`)
- Test: `unlockfps_nc.Tests/StubFileTests.cs`, `unlockfps_nc.Tests/UrlPolicyTests.cs`

**Interfaces:**
- Produces: `internal sealed class StubFile : IDisposable { public string Path { get; } internal static StubFile Extract(string directory, byte[] content); public static StubFile ExtractEmbedded(string directory); }`. Object này giữ handle với `FileShare.Read` (không cho Write/Delete) cho tới khi Dispose. Ném `IOException` hoặc `InvalidDataException` nếu không ghi được hoặc SHA-256 trên đĩa khác `content`.
- Produces: `internal static bool UrlPolicy.IsTrustedReleaseUrl(string? url)`. Chỉ trả true khi URL tuyệt đối, scheme `https` và host đúng bằng `github.com` (không phân biệt hoa thường).

- [ ] **Step 1: Viết test fail**

```csharp
[Fact] public void Extract_WritesAndLocksFile() {
    var dir = Directory.CreateTempSubdirectory().FullName; var data = new byte[] { 1, 2, 3 };
    using var stub = StubFile.Extract(dir, data);
    Assert.Equal(data, File.ReadAllBytes(stub.Path));
    Assert.Throws<IOException>(() => File.OpenWrite(stub.Path).Dispose());
    Assert.Throws<IOException>(() => File.Delete(stub.Path));
}
[Fact] public void Extract_FailsWhenFileLockedByOther() {
    var dir = Directory.CreateTempSubdirectory().FullName;
    var path = Path.Combine(dir, "UnlockerStub.dll"); File.WriteAllBytes(path, new byte[] { 9 });
    using var hostile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    Assert.ThrowsAny<IOException>(() => StubFile.Extract(dir, new byte[] { 1, 2, 3 }));
}
[Theory]
[InlineData("https://github.com/34736384/genshin-fps-unlock/releases", true)]
[InlineData("http://github.com/x", false)]
[InlineData("https://github.com.evil.example/x", false)]
[InlineData("file:///C:/Windows/System32/cmd.exe", false)]
[InlineData(@"C:\Windows\System32\cmd.exe", false)]
[InlineData(null, false)]
public void IsTrustedReleaseUrl(string? url, bool expected) => Assert.Equal(expected, UrlPolicy.IsTrustedReleaseUrl(url));
```

- [ ] **Step 2: Chạy `dotnet test --filter "StubFileTests|UrlPolicyTests"`**: phải FAIL.

- [ ] **Step 3: Cài đặt**

- `StubFile.Extract`:
  1. Ghi file bằng `FileMode.Create, FileShare.None`.
  2. Đóng lại, rồi mở lại bằng `FileAccess.Read, FileShare.Read` và giữ handle này.
  3. So `SHA256.HashData(stream)` với hash của `content`.
- `IpcService.Start`:
  - `using var stub = StubFile.ExtractEmbedded(AppContext.BaseDirectory)` và giữ suốt tới khi `Ready` hoặc timeout (game nạp DLL theo đường dẫn ở thời điểm hook kích hoạt).
  - Nếu có exception thì MessageBox kèm thông báo lỗi rồi `return false`.
- `MainForm.CheckVersion`: chỉ `Process.Start` khi `UrlPolicy.IsTrustedReleaseUrl(remoteVersion.Url)`.

- [ ] **Step 4: Chạy test, phải PASS.**

- [ ] **Step 5: Commit**: `fix(security): verify and lock extracted stub; only open trusted release URLs`

---

### Task 7: Thứ tự khởi động / single instance (finding #5) — mình

**Files:**
- Modify: `unlockfps_nc/Program.cs`

- [ ] **Step 1: Sửa `Main`**

- Đưa khối `if (!IsAdministrator()) { …runas…; return; }` lên **trước** khi tạo `Mutex`/`EventWaitHandle`, để mọi instance đều tạo object kernel cùng integrity level.
- Bọc `new Mutex(...)` trong `try { } catch (UnauthorizedAccessException) { return; }` để chống trường hợp có instance cũ chạy ở integrity cao hơn.
- Task lắng nghe event: lấy form qua `Application.OpenForms` bên trong `RestoreFromTray` (đã có `Invoke`). Giữ nguyên logic.

- [ ] **Step 2: Kiểm tra tay** (không unit test được)

  1. Mở exe lần 1 rồi chấp nhận UAC; thu nhỏ xuống tray.
  2. Mở exe lần 2 rồi chấp nhận UAC: cửa sổ lần 1 phải hiện lại, instance 2 tự thoát, không có hộp thoại crash nào.

- [ ] **Step 3: Commit**: `fix(startup): elevate before creating single-instance objects`

---

### Task 8: ConfigService chịu được config hỏng (finding #12, LOW FPS bounds) — codex

**Files:**
- Modify: `unlockfps_nc/Service/ConfigService.cs`
- Modify: `unlockfps_nc/MainForm.Designer.cs` (`InputFPS.Minimum` và `SliderFPS.Minimum` đổi thành 10)
- Test: `unlockfps_nc.Tests/ConfigServiceTests.cs`

**Interfaces:**
- Produces: `internal ConfigService(string configPath)`. Constructor public không tham số giữ nguyên và gọi constructor này với `Path.Combine(AppContext.BaseDirectory, "fps_config.json")`.

- [ ] **Step 1: Viết test fail**

```csharp
[Fact] public void CorruptJson_UsesDefaults_AndBacksUp() {
    var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "fps_config.json");
    File.WriteAllText(path, "{ not json");
    var svc = new ConfigService(path);
    Assert.Equal(120, svc.Config.FPSTarget);
    Assert.True(File.Exists(path + ".bak"));
}
[Fact] public void NullsAndOutOfRange_AreSanitized() {
    var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "fps_config.json");
    File.WriteAllText(path, """{"DllList":null,"GamePath":null,"AdditionalCommandLine":null,"FPSTarget":5}""");
    var c = new ConfigService(path).Config;
    Assert.NotNull(c.DllList); Assert.Equal("", c.GamePath); Assert.Equal("", c.AdditionalCommandLine);
    Assert.Equal(10, c.FPSTarget);
}
```

- [ ] **Step 2: Chạy `dotnet test --filter ConfigServiceTests`**: phải FAIL.

- [ ] **Step 3: Sửa**

- `Load` bắt `JsonException` và `IOException`: dùng `File.Copy(path, path + ".bak", true)` rồi giữ `new Config()`.
- `Sanitize`:
  - Thêm `DllList ??= new()`, `GamePath ??= ""`, `AdditionalCommandLine ??= ""`.
  - FPS clamp trong khoảng **10..1000**.
- Mọi I/O dùng đường dẫn từ constructor.

- [ ] **Step 4: Chạy test, phải PASS.**

- [ ] **Step 5: Commit**: `fix(config): survive corrupt or partial config files`

---

### Task 9: Độ bền UI và inject DLL (finding #14, LOW forms/inject) — codex

**Files:**
- Create: `unlockfps_nc/Utility/DllValidator.cs`
- Modify: `unlockfps_nc/SetupForm.cs`, `unlockfps_nc/SettingsForm.cs`, `unlockfps_nc/Utility/ProcessUtils.cs` (chỉ sửa `InjectDlls`), `unlockfps_nc/Utility/Native.cs` (chỉ **thêm** `GetExitCodeThread`)
- Test: `unlockfps_nc.Tests/DllValidatorTests.cs`

**Interfaces:**
- Produces: `internal static bool DllValidator.IsNativeX64Dll(string path)`. Trả false khi file không tồn tại, không phải PE, là assembly managed hoặc không phải AMD64. Hàm không bao giờ ném exception.

- [ ] **Step 1: Viết test fail**

```csharp
[Fact] public void Missing_False() => Assert.False(DllValidator.IsNativeX64Dll(@"C:\nope\x.dll"));
[Fact] public void TextFile_False() { var p = Path.GetTempFileName(); File.WriteAllText(p, "hi"); Assert.False(DllValidator.IsNativeX64Dll(p)); }
[Fact] public void ManagedAssembly_False() => Assert.False(DllValidator.IsNativeX64Dll(typeof(DllValidatorTests).Assembly.Location));
[Fact] public void Kernel32_True() => Assert.True(DllValidator.IsNativeX64Dll(Path.Combine(Environment.SystemDirectory, "kernel32.dll")));
```

- [ ] **Step 2: Chạy `dotnet test --filter DllValidatorTests`**: phải FAIL.

- [ ] **Step 3: Sửa**

- `DllValidator`: chuyển logic `VerifyDll` sang đây, bắt `BadImageFormatException`, `IOException`, `UnauthorizedAccessException`. `SettingsForm` gọi hàm này.
- `SettingsForm.UpdateControlState`: sau khi đổi `_config.Fullscreen` thì gọi `CBFullscreen.DataBindings["Checked"]?.ReadValue()`.
- `SetupForm.PollProcess`:
  - `CloseHandle` mọi handle đã mở mà không dùng tới (kể cả game Unity khác).
  - Dùng `Task.Delay(1000, _cts.Token)` và bắt `OperationCanceledException` để thoát.
  - Trước mỗi lần `Invoke` hay `BeginInvoke`, kiểm tra `if (IsDisposed || !IsHandleCreated) return;`. Áp dụng tương tự cho `SearchGamePath`.
- `ProcessUtils.InjectDlls`:
  - Xoá cặp `StringToHGlobalUni`/`FreeHGlobal` và `RtlAdjustPrivilege`.
  - Nếu `bytes.Length + 2 > 0x1000` thì return false.
  - Gọi `WaitForSingleObject(thread, 10000)`. Nếu `GetExitCodeThread` cho ra 0 (`LoadLibraryW` thất bại) thì đóng handle rồi return false.
  - Đảm bảo `VirtualFreeEx` được gọi ở mọi nhánh return (`try/finally`).

- [ ] **Step 4: Chạy test, phải PASS. Thêm `dotnet build -c ReleaseMin` để chắc vẫn build được.**

- [ ] **Step 5: Commit**: `fix(ui): close leaked process handles, harden DLL validation and injection`

---

### Task 10: Dọn code chết (LOW) — codex, wave 2

**Files:**
- Modify: `unlockfps_nc/Utility/ProcessUtils.cs` (xoá `PatternScan` cả 2 overload, `PatternScanAllOccurrences`, `ParseSignature`, `GetModuleBase`)
- Modify: `unlockfps_nc/Utility/Native.cs` (xoá các P/Invoke, struct và hằng không còn ai tham chiếu)

- [ ] **Step 1: Liệt kê symbol không dùng**: với mỗi `static extern` trong `Native.cs`, chạy `rg -w <Name> unlockfps_nc --glob '!Native.cs'`. Symbol nào có 0 kết quả thì xoá.
- [ ] **Step 2: Build cả 3 cấu hình**

Run: `dotnet build -c Debug; dotnet build -c Release; dotnet build -c ReleaseMin`
Expected: cả 3 `Build succeeded`, không có warning mới.

- [ ] **Step 3: Chạy `dotnet test unlockfps_nc.Tests`**: tất cả phải PASS.
- [ ] **Step 4: Commit**: `refactor: remove unused pattern-scan helpers and P/Invokes`

---

### Task 11: Verify toàn bộ — mình

- [ ] `mingw32-make -C UnlockerStub clean all test`: DLL build được, self-test exit 0.
- [ ] Copy DLL vào `unlockfps_nc/Resources/`, rồi chạy `dotnet build` ở cả 3 cấu hình và `dotnet test`: tất cả xanh.
- [ ] Thử `msbuild UnlockerStub\UnlockerStub.vcxproj /p:Configuration=Release /p:Platform=x64` (VS 18). Nếu thiếu toolset v143 thì ghi chú lại, **không** sửa vcxproj.
- [ ] Review toàn bộ diff (`git diff netcore...HEAD`) theo tiêu chí security và code review.
- [ ] Danh sách kiểm tra tay trong game (master thực hiện):
  1. Khởi động game qua unlocker và đổi FPS: giá trị mới có hiệu lực.
  2. Bật power save rồi alt-tab: FPS giảm còn 10.
  3. Tắt unlocker rồi mở lại khi game vẫn chạy: attach lại trong khoảng 5 giây, không có lỗi.
  4. Tắt game khi đang ở màn hình load, sau đó mở lại: unlocker vẫn attach được.
  5. Bật Mobile UI: UI mobile hiện ra và game không crash.
  6. Combo Priority: kiểm tra trong Task Manager thấy đúng priority.
- [ ] Hand-off: push branch `fix/review-findings` và mở PR vào `netcore`.

---

## Bảng ánh xạ finding sang task

| Finding | Task |
|---|---|
| #1 Update đè Status, #2 hook toàn cục | 4 |
| #3 vòng chờ treo, #13 Priority chết, #15 Application.Exit, cmd line không quote, STARTUPINFO.cb | 5 |
| #4 GNUmakefile | 1 |
| #5 single-instance/elevation | 7 |
| #6 AllocatePage, #7 tràn heap, #8 race hook, #9 không xác minh opcode, đọc vượt khi SSE scan, GetModulePath, layout IPCStatus | 3 |
| #10 TOCTOU stub, #11 URL cập nhật | 6 |
| #12 config hỏng, lệch giới hạn FPS | 8 |
| #14 rò handle SetupForm, PEReader crash, binding Fullscreen, InjectDlls | 9 |
| Code chết C#, dòng rác trong csproj | 10, 2 |
