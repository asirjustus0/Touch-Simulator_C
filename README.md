# Touch Simulator — all-C# desktop prototype v1.1

A **C#/.NET 10 Windows Forms** desktop application with an object-oriented, replaceable Win32 driver. No Python runtime is involved. The project includes a command-entry UI, an Execute button, a screenshot/live-preview window, optional tap-on-preview, genuine Windows multi-touch injection, mouse input, and no-dependency MJPEG AVI recording.

## Build and run on Windows 10/11 (x64)

**Requirements to build:** Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Visual Studio with .NET desktop development is optional).

1. Extract the ZIP archive anywhere on your Windows PC.
2. Double-click `launch_windows.bat`. If a published EXE exists it launches that; otherwise it uses `dotnet run`.
   **The launcher keeps the command window open** if the application exits early and gives you the location of the startup log.
   Alternatively, use `run_source_windows.bat` directly (the window also stays open).
3. In the UI, run `WIN_DIAGNOSTICS`, `WIN_SCREEN_INFO`, `WIN_WINDOWS`, and `WIN_SCREENSHOT` first (no input is injected).
4. Choose an expendable target such as Notepad using `WIN_TARGET_WINDOW_Notepad`. To target Explorer regardless of changing folder title use `WIN_TARGET_APP_explorer`, or pick the exact window using the application/window dropdown and bring it forward with `WIN_FOCUS_TARGET` if necessary.
5. Execute the desired touch/mouse/screenshot commands. Use the examples in the UI or `docs/COMMAND_REFERENCE.md`.
6. Double-click `build_exe_windows.bat` to compile and create `build_log.txt` for diagnostics. It publishes a **self-contained single EXE** at `publish\TouchSimulator.exe`. It includes the .NET runtime; target PCs need no separately installed .NET runtime.

Or from Windows Terminal/PowerShell:

```powershell
dotnet run --project .\TouchSimulator\TouchSimulator.csproj
dotnet publish .\TouchSimulator\TouchSimulator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

## Run command tests (no Windows input injected)

```powershell
dotnet run --project .\Tests\TouchSimulator.Tests.csproj -c Debug
```

Tests use a fake driver and cover command dispatch, invalid commands, and screenshot/monitor/record directives. Live user32 injection still needs Windows-device validation.

**Documentation:** `docs/COMMAND_REFERENCE.md` contains the entire command set with arguments and examples; `docs/ARCHITECTURE.md` describes the driver abstraction and future actor layout.

## Startup troubleshooting and launcher

- **No window appears / command prompt flashes and closes:** launch `launch_windows.bat`, which retains its console after the GUI closes. It checks for `publish\TouchSimulator.exe` and falls back to `dotnet run` if necessary.
- **Build errors:** read `build_log.txt` (created by `build_exe_windows.bat`).
- **Early GUI exceptions:** read `%LOCALAPPDATA%\TouchSimulator\logs\startup.log`. An error dialog will also show the log location when possible.
- **UI startup fix:** splitter sizing is performed in `OnShown` after WinForms layout, rather than setting a large fixed SplitterDistance during form construction.
- The application/window dropdown lists a process name, current title, and hexadecimal HWND. The target remains bound to the same HWND even if its title changes. If the target is closed or its HWND is reassigned, select it again.
- `WIN_TARGET_APP_explorer` selects the first matching visible titled Explorer window. When multiple Explorer windows exist, use the dropdown or `WIN_TARGET_HANDLE_0x...` for an exact selection. Titles shown by `WIN_WINDOWS` can still change.

## Important implementation facts

- **Native touch** uses `InitializeTouchInjection` / `InjectTouchInput`, with simultaneous contact frames for pinch zoom and multiple touch points. It is not equivalent to mouse input.
- **Mouse** uses Win32 `SetCursorPos` and `SendInput` for clicks/drag/scroll.
- **Screenshots** use `Graphics.CopyFromScreen` and capture the visible pixels within the target client area or virtual desktop.
- **Live monitor** is periodic screenshots at 1–10 FPS; **AVI** is a Motion-JPEG screen recording at 1–10 FPS with no audio (not MP4). The recorder is written entirely in C#, without third-party codecs or a Python process.
- **DPI:** the app requests per-monitor DPI awareness. All commands use physical desktop pixels; when targeting a named window, they use its client pixel coordinates.
- **Security:** elevated/secure target windows can reject injected input (UIPI). Touch commands change real Windows desktop state.
- **Limits:** AVI size <4 GiB; choose a reasonable target size and short clips. Long/high-FPS recordings need a future Media Foundation encoder. Some video players may not support MJPEG AVI.

## Verification status

This package is provided as **editable source and Windows build scripts**, not a precompiled `.exe`. The development environment here is Linux without a .NET SDK or Windows desktop session. Consequently, the C# sources and actual Windows injection/video output **have not been compiled, executed, or verified on Windows**. Build and run the read-only commands first, then test touch gestures in a safe window. Report build errors or unexpected behavior for targeted fixes.

References: [Microsoft InjectTouchInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-injecttouchinput), [Microsoft SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput), [.NET self-contained single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
