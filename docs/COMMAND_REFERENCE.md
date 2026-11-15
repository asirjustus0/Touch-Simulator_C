# Touch Simulator C# — Complete command reference (v1.2)

All commands are **case-insensitive**. Enter one command at a time in the text box and click **Execute command**. Numbers are integers. The default coordinate system is **absolute virtual-desktop pixel coordinates** (including negative coordinates on monitors to the left/top of the primary display). After selecting a target window, coordinates refer to **its client area**, with `(0,0)` at the client area's top-left corner. The screenshot displays the same selected region. Set Windows display scaling normally; the application uses per-monitor DPI awareness.

## Information and targeting

| Command | Meaning |
|---|---|
| `WIN_HELP` | Display available command examples in the log. |
| `WIN_SCREEN_INFO` | Show capture bounds, selected target, touch-initialization status. |
| `WIN_DIAGNOSTICS` | Read-only architecture check: process bitness and expected x64 native interop structure sizes. |
| `WIN_WINDOWS` | List visible top-level windows with titles. |
| `WIN_APPS` | List visible windows with process names, titles, and HWND hex IDs. |
| `WIN_TARGET_APP_explorer` | Select the first visible File Explorer window using the `explorer.exe` process identity, independent of its current title. |
| `WIN_TARGET_APP_notepad` | Select the first visible Notepad window by process identity. |
| `WIN_TARGET_HANDLE_0x1234` | Select a specific window handle (copy the real ID from `WIN_APPS`; `0x1234` is an example). |
| `WIN_TARGET_WINDOW_Notepad` | Target the first visible window whose title contains `Notepad` (case-insensitive). Exact title matching is not required. |
| `WIN_TARGET_CLEAR` | Return to the absolute desktop coordinate system. |
| `WIN_FOCUS_TARGET` | Ask Windows to bring the selected window to the foreground (may be denied by Windows focus restrictions). |

## True Windows touch — native `InitializeTouchInjection` / `InjectTouchInput`

| Command | Meaning |
|---|---|
| `WIN_TOUCH_400,300` | Single true touch at `(400,300)` (70 ms hold). |
| `WIN_TOUCH_400,300,200` | Single touch held for 200 ms. |
| `WIN_DOUBLE_TOUCH_400,300` | Two consecutive touch taps. |
| `WIN_MULTI_TOUCH_350,300\|450,300@120` | Two simultaneous touch contacts held 120 ms. Between 2 and 10 contacts allowed; `@milliseconds` optional. |
| `WIN_SWIPE_150,200,600,200` | Touch swipe from `(150,200)` to `(600,200)` in 450 ms. |
| `WIN_SWIPE_150,200,600,200,700` | Same swipe in 700 ms. |
| `WIN_TOUCH_DRAG_150,200,600,200,900` | Touch long-press (250 ms) followed by drag over 900 ms. Duration optional (default 900). |
| `WIN_ZOOM_IN_600,450` | Two-finger outward spread centered on `(600,450)` in 500 ms. |
| `WIN_ZOOM_OUT_600,450,650` | Two-finger inward pinch centered on `(600,450)` in 650 ms. |

`WIN_ZOOM_IN` and `WIN_ZOOM_OUT` create two contacts horizontally, spanning between 70 and 250 pixels. Make sure the centre is at least 125 pixels from both edges of the current input region. Genuine touch depends on whether the receiving application supports touch gestures.

## Mouse input — native `SendInput`

| Command | Meaning |
|---|---|
| `WIN_MOUSE_MOVE_400,300` | Move pointer. |
| `WIN_MOUSE_CLICK_400,300` | Single left click. |
| `WIN_MOUSE_DOUBLE_CLICK_400,300` | Double left click. |
| `WIN_MOUSE_MULTI_CLICK_400,300,3` | Repeat left click 3 times (1–20 allowed). |
| `WIN_MOUSE_RIGHT_CLICK_400,300` | Single right click. |
| `WIN_DRAG_DROP_100,200,400,500,700` | Press left mouse button, drag to destination over 700 ms, release. Duration optional. |
| `WIN_MOUSE_DRAG_100,200,400,500,700` | Alias for `WIN_DRAG_DROP`. |
| `WIN_MOUSE_SCROLL_UP_3` | Vertical scroll up 3 wheel detents. |
| `WIN_MOUSE_SCROLL_DOWN_3` | Vertical scroll down 3 wheel detents. |
| `WIN_MOUSE_SCROLL_LEFT_3` | Horizontal scroll left 3 wheel detents. |
| `WIN_MOUSE_SCROLL_RIGHT_3` | Horizontal scroll right 3 wheel detents. |

Scroll commands target the centre of a selected window; otherwise they operate at the current mouse location. Wheel detents must be 1–50.

## Screenshot, live preview and video recording

| Command | Meaning |
|---|---|
| `WIN_SCREENSHOT` | Capture the current desktop/window client region and display it in the preview. |
| `WIN_MONITOR_START` | Start live screenshot updates at 5 frames per second. |
| `WIN_MONITOR_START_8` | Start live preview at 8 frames per second (range 1–10). |
| `WIN_MONITOR_STOP` | Stop live preview. Recording, if running, continues. |
| `WIN_RECORD_START` | Start recording to a **Motion JPEG AVI** file at 5 FPS. |
| `WIN_RECORD_START_5` | Start recording at 5 FPS (range 1–10). Live preview appears while recording. |
| `WIN_RECORD_STOP` | Stop recording and finalize the `.avi` file. |

Recorded files are written to `%USERPROFILE%\Videos\TouchSimulator\` (or the Documents folder if no Videos folder is configured). The recorder uses built-in `System.Drawing` JPEG encoding and a small C# RIFF/AVI writer, so there is no FFmpeg/Python dependency and no audio track. AVI playback should work in players that support MJPEG. Nominal FPS may differ from capture cadence under load. **4 GiB AVI size limit**; stop and save shorter clips. Preview frame rate is capped at 10 FPS; this version is for functional testing, not high-performance video capture.

The **Save PNG** button exports the currently displayed frame to a chosen path. The optional **Touch at clicked position in screenshot** checkbox maps PictureBox zoom/letterboxing back to driver coordinates.

## Examples to try in order

```text
WIN_SCREEN_INFO
WIN_DIAGNOSTICS
WIN_WINDOWS
WIN_TARGET_WINDOW_Notepad
WIN_FOCUS_TARGET
WIN_SCREENSHOT
WIN_MOUSE_CLICK_80,60
WIN_TOUCH_80,60
WIN_MOUSE_SCROLL_DOWN_3
WIN_MONITOR_START_5
WIN_MONITOR_STOP
WIN_RECORD_START_5
WIN_RECORD_STOP
WIN_TARGET_CLEAR
```

Touch/drag/click commands affect **real desktop windows**. Use a disposable test window. Do not inject into secure desktops, elevated windows from an unelevated process, or other users' sessions. `SendInput` can be blocked by Windows User Interface Privilege Isolation (UIPI). Touch injection is supported on Windows 8+ desktops, but this sample targets Windows 10/11 with .NET 10.

## Launcher and crash logs

Run `launch_windows.bat` to start the published EXE, or start from source if the EXE has not been built. The launcher stays open when the UI exits. If no window appears, consult `%LOCALAPPDATA%\TouchSimulator\logs\startup.log` and, for build failures, `build_log.txt`.

The title is displayed dynamically for a selected HWND, so changing File Explorer folders normally does not lose targeting. Closed/reused HWNDs are rejected.

## Version 1.2: screenshot coordinate inspector and troubleshooting

1. Select the target with `WIN_TARGET_APP_explorer`, `WIN_TARGET_WINDOW_...`, or the dropdown.
2. Run `WIN_SCREENSHOT`. Move the pointer over the displayed image. The **Command X/Y** and **Screen X/Y** readouts update without clicking. Letterboxing from the zoomed preview is excluded.
3. Click **Open X/Y Window** to show an independent floating Coordinate Inspector. It displays the current image pixel mapped into command coordinates, plus the absolute screen coordinates. X/Y fields are editable for precise input.
4. Use **Touch at X/Y**, **Mouse click at X/Y**, or **Copy touch command** beneath the image. The floating inspector provides its own **Simulate touch** and **Mouse click** buttons. The original optional **Touch when clicking the screenshot** checkbox remains available.
5. After changing or moving the target, capture a fresh screenshot before executing an image coordinate. Preview clicks use the selected target's client-area coordinates; with no target, they use absolute virtual desktop coordinates, including negative values on secondary monitors.

**Native touch failure diagnostics:** v1.2 reports the actual Windows error code and phase (`DOWN`, `HOLD/UPDATE`, `MOVE/UPDATE`, `UP`). Error `87` often indicates invalid contact data/coordinates; `5` is access denied; `21` is a timing issue (driver retries these frames). The driver uses periodic UPDATE frames during a touch hold and reports the latest touch code through `WIN_DIAGNOSTICS`. A generic `InjectTouchInput failed` message is insufficient to identify a specific cause. Windows touch-injection support is session/application dependent; some targets respond to mouse input but not true touch. Test in a normal interactive Windows desktop with a simple, non-elevated application, not on a secure desktop or inside a session that blocks touch injection.

The screenshot is a capture of **visible desktop pixels**; the program does not obtain hidden application surfaces. The simulator's own window can obstruct the target window. Bring the target to the foreground or arrange it beside the simulator if your touch command seems to hit the simulator instead of the target. `WIN_FOCUS_TARGET` may help, but focus changes are restricted by Windows. `WIN_MOUSE_CLICK` remains a useful control comparison when `WIN_TOUCH` fails.
