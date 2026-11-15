# Architecture — pure C# Windows driver + WinForms UI

```text
TouchSimulator.exe  (C#, .NET 10 Windows Forms)
│
├── MainForm               Presentation; target picker, Execute, preview, live/AVI
│     └── CommandProcessor  Text commands → typed method calls / UI directives
│           └── IWindowsDriver (contract / replaceable driver)
│                 └── User32WindowsDriver (implementation)
│                       ├── WindowsNative (P/Invoke)
│                       │     ├── InitializeTouchInjection / InjectTouchInput
│                       │     ├── SendInput / SetCursorPos
│                       │     └── EnumWindows / GetClientRect / ClientToScreen
│                       └── GDI+ Bitmap / Graphics.CopyFromScreen
├── CoordinateInspectorForm  floating X/Y tool window and touch/click actions
├── PreviewCoordinateMapper image-zoom / letterbox aware coordinate mapping
├── StartupLog             persistent early-startup/crash log
└── MjpegAviRecorder        pure C# RIFF AVI writer; JPEG frame encoding
```

The UI does not P/Invoke Win32 APIs and does not implement gesture logic. The driver has no dependency on UI controls, command syntax, or the AVI encoder. `CommandProcessor` is deliberately small and synchronous for future actor-message adaptation. The UI uses `Task.Run` to keep commands from blocking the WinForms message loop. Touch and mouse gestures are serialized by the driver's private `_inputLock`.

Future Actor Framework integration: wrap `IWindowsDriver` with an InputActor, place `CommandProcessor` in a CommandActor, screenshot capture in a CaptureActor, and video writing in a RecorderActor; communicate through strongly typed records over channels. These actors are not implemented in this sample.

**Coordinate mapping:** without a target, input coordinates are absolute virtual-desktop pixels. With a selected target, points are client-area-relative; the backend converts to screen pixels and screenshots the target's screen rectangle. Occluded/minimised windows are not magically rendered: `CopyFromScreen` captures visible screen content. Windows may need the selected target to be visible and foreground for input to reach it. Use `WIN_FOCUS_TARGET` (or click the target manually); Windows may deny foreground requests.

**Design limitations:** Target selection supports a title substring, an application process name (first matching visible window), or a particular HWND chosen from the UI dropdown. The selected HWND is retained when a title changes, and its process ID is checked before use. True-touch injection support varies between applications. The sample does not implement global recording of real human touch inputs, Android ADB, privilege escalation, device emulation, automated script replay, or audio/video compression beyond Motion JPEG. Video timing is nominal. This is a functional Windows driver demonstration; it has not yet been validated on physical Windows hardware in this environment.
