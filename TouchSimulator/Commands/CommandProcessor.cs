using System.Drawing;
using System.Globalization;
using TouchSimulator.Driver;

namespace TouchSimulator.Commands;

public enum CommandAction { None, Screenshot, MonitorStart, MonitorStop, RecordStart, RecordStop }
public sealed record CommandResult(string Message, CommandAction Action = CommandAction.None, int Fps = 5, Bitmap? Image = null);

/// <summary>Command language; deliberately no dependency on Windows Forms.</summary>
public sealed class CommandProcessor
{
    private readonly IWindowsDriver _driver;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public CommandProcessor(IWindowsDriver driver) => _driver = driver;

    public static readonly string[] Examples =
    {
        "WIN_HELP", "WIN_SCREEN_INFO", "WIN_DIAGNOSTICS", "WIN_WINDOWS", "WIN_APPS", "WIN_TARGET_APP_explorer", "WIN_TARGET_WINDOW_Notepad",
        "WIN_TARGET_CLEAR", "WIN_FOCUS_TARGET", "WIN_TOUCH_400,300", "WIN_DOUBLE_TOUCH_400,300",
        "WIN_MULTI_TOUCH_350,300|450,300@120", "WIN_SWIPE_150,200,600,200,450",
        "WIN_TOUCH_DRAG_150,200,600,200,900", "WIN_DRAG_DROP_100,200,400,500,700",
        "WIN_MOUSE_MOVE_400,300", "WIN_MOUSE_CLICK_400,300", "WIN_MOUSE_DOUBLE_CLICK_400,300",
        "WIN_MOUSE_MULTI_CLICK_400,300,3", "WIN_MOUSE_RIGHT_CLICK_400,300",
        "WIN_MOUSE_SCROLL_DOWN_3", "WIN_MOUSE_SCROLL_UP_3",
        "WIN_MOUSE_SCROLL_LEFT_3", "WIN_MOUSE_SCROLL_RIGHT_3",
        "WIN_ZOOM_IN_600,450,500", "WIN_ZOOM_OUT_600,450,500",
        "WIN_SCREENSHOT", "WIN_MONITOR_START_5", "WIN_MONITOR_STOP",
        "WIN_RECORD_START_5", "WIN_RECORD_STOP"
    };

    public CommandResult Execute(string raw)
    {
        string s = raw.Trim();
        if (s.Length == 0) throw new ArgumentException("Enter a command, for example WIN_SCREEN_INFO.");
        if (s.Equals("WIN_HELP", StringComparison.OrdinalIgnoreCase))
            return new CommandResult("Commands:\r\n" + string.Join("\r\n", Examples));
        if (s.Equals("WIN_SCREEN_INFO", StringComparison.OrdinalIgnoreCase)) return new(_driver.GetScreenInfo());
        if (s.Equals("WIN_DIAGNOSTICS", StringComparison.OrdinalIgnoreCase)) return new(_driver.GetDiagnostics());
        if (s.Equals("WIN_WINDOWS", StringComparison.OrdinalIgnoreCase))
        {
            var windows = _driver.GetWindowTitles();
            return new(windows.Count == 0 ? "No titled visible windows found." : string.Join("\r\n", windows));
        }
        if (s.Equals("WIN_APPS", StringComparison.OrdinalIgnoreCase))
        {
            var windows = _driver.GetWindows();
            return new(windows.Count == 0 ? "No visible titled application windows found." :
                string.Join("\r\n", windows.Select(w => w.ToString())));
        }
        if (HasPrefix(s, "WIN_TARGET_APP_"))
        {
            _driver.SelectApplication(s["WIN_TARGET_APP_".Length..]);
            return new($"Application target selected: {_driver.TargetWindowTitle}. Coordinates are now client-relative.");
        }
        if (HasPrefix(s, "WIN_TARGET_HANDLE_"))
        {
            string handleText = s["WIN_TARGET_HANDLE_".Length..].Trim();
            if (handleText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) handleText = handleText[2..];
            if (!long.TryParse(handleText, NumberStyles.HexNumber, Inv, out long hwnd) || hwnd == 0)
                throw new ArgumentException("Window handle should be hexadecimal, e.g. WIN_TARGET_HANDLE_0x1234.");
            _driver.SelectWindowByHandle(new IntPtr(hwnd));
            return new($"Window target selected: {_driver.TargetWindowTitle}.");
        }
        if (s.Equals("WIN_TARGET_CLEAR", StringComparison.OrdinalIgnoreCase))
        {
            _driver.ClearWindowTarget();
            return new("Target cleared: using absolute desktop coordinates.");
        }
        if (s.Equals("WIN_FOCUS_TARGET", StringComparison.OrdinalIgnoreCase))
        {
            _driver.FocusTargetWindow();
            return new("Requested selected target window to become foreground.");
        }
        if (HasPrefix(s, "WIN_TARGET_WINDOW_"))
        {
            _driver.SelectWindow(s["WIN_TARGET_WINDOW_".Length..]);
            return new($"Target selected: {_driver.TargetWindowTitle}. Coordinates are now client-relative.");
        }
        if (s.Equals("WIN_SCREENSHOT", StringComparison.OrdinalIgnoreCase))
            return new("Snapshot captured.", CommandAction.Screenshot, Image: _driver.Capture());
        if (s.Equals("WIN_MONITOR_STOP", StringComparison.OrdinalIgnoreCase)) return new("Live monitoring stopped.", CommandAction.MonitorStop);
        if (s.Equals("WIN_RECORD_STOP", StringComparison.OrdinalIgnoreCase)) return new("Recording stopped.", CommandAction.RecordStop);
        if (s.Equals("WIN_MONITOR_START", StringComparison.OrdinalIgnoreCase)) return new("Live monitoring started at 5 FPS.", CommandAction.MonitorStart, 5);
        if (s.Equals("WIN_RECORD_START", StringComparison.OrdinalIgnoreCase)) return new("AVI recording started at 5 FPS.", CommandAction.RecordStart, 5);
        if (HasPrefix(s, "WIN_MONITOR_START_"))
        {
            int fps = Fps(s["WIN_MONITOR_START_".Length..]);
            return new($"Live monitoring started at {fps} FPS.", CommandAction.MonitorStart, fps);
        }
        if (HasPrefix(s, "WIN_RECORD_START_"))
        {
            int fps = Fps(s["WIN_RECORD_START_".Length..]);
            return new($"AVI recording started at {fps} FPS.", CommandAction.RecordStart, fps);
        }
        if (HasPrefix(s, "WIN_TOUCH_DRAG_"))
        {
            var a = Args(s, "WIN_TOUCH_DRAG_", 4, 5);
            _driver.TouchDrag(a[0], a[1], a[2], a[3], a.Length == 5 ? a[4] : 900);
            return new("Touch drag complete.");
        }
        if (HasPrefix(s, "WIN_DOUBLE_TOUCH_"))
        {
            var a = Args(s, "WIN_DOUBLE_TOUCH_", 2);
            _driver.DoubleTouch(a[0], a[1]);
            return new("Double touch complete.");
        }
        if (HasPrefix(s, "WIN_MULTI_TOUCH_"))
        {
            var payload = s["WIN_MULTI_TOUCH_".Length..];
            string[] parts = payload.Split('@', 2, StringSplitOptions.TrimEntries);
            int hold = parts.Length == 2 ? Num(parts[1]) : 120;
            var coords = parts[0].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var points = coords.Select(pair =>
            {
                var pieces = pair.Split(',', StringSplitOptions.TrimEntries);
                if (pieces.Length != 2) throw new ArgumentException("Multi-touch format: WIN_MULTI_TOUCH_x1,y1|x2,y2@120");
                return new Point(Num(pieces[0]), Num(pieces[1]));
            }).ToArray();
            _driver.MultiTouch(points, hold);
            return new($"Simultaneous touch of {points.Length} contacts complete.");
        }
        if (HasPrefix(s, "WIN_SWIPE_"))
        {
            var a = Args(s, "WIN_SWIPE_", 4, 5);
            _driver.Swipe(a[0], a[1], a[2], a[3], a.Length == 5 ? a[4] : 450);
            return new("Touch swipe complete.");
        }
        if (HasPrefix(s, "WIN_TOUCH_"))
        {
            var a = Args(s, "WIN_TOUCH_", 2, 3);
            _driver.Touch(a[0], a[1], a.Length == 3 ? a[2] : 70);
            return new("Touch complete.");
        }
        if (HasPrefix(s, "WIN_DRAG_DROP_") || HasPrefix(s, "WIN_MOUSE_DRAG_"))
        {
            var prefix = HasPrefix(s, "WIN_DRAG_DROP_") ? "WIN_DRAG_DROP_" : "WIN_MOUSE_DRAG_";
            var a = Args(s, prefix, 4, 5);
            _driver.MouseDrag(a[0], a[1], a[2], a[3], a.Length == 5 ? a[4] : 700);
            return new("Mouse drag and drop complete.");
        }
        if (HasPrefix(s, "WIN_MOUSE_MOVE_"))
        {
            var a = Args(s, "WIN_MOUSE_MOVE_", 2);
            _driver.MouseMove(a[0], a[1]);
            return new("Cursor moved.");
        }
        if (HasPrefix(s, "WIN_MOUSE_DOUBLE_CLICK_"))
        {
            var a = Args(s, "WIN_MOUSE_DOUBLE_CLICK_", 2);
            _driver.MouseClick(a[0], a[1], 2);
            return new("Double mouse click complete.");
        }
        if (HasPrefix(s, "WIN_MOUSE_MULTI_CLICK_"))
        {
            var a = Args(s, "WIN_MOUSE_MULTI_CLICK_", 3);
            _driver.MouseClick(a[0], a[1], a[2]);
            return new($"{a[2]} mouse clicks complete.");
        }
        if (HasPrefix(s, "WIN_MOUSE_RIGHT_CLICK_"))
        {
            var a = Args(s, "WIN_MOUSE_RIGHT_CLICK_", 2);
            _driver.MouseClick(a[0], a[1], rightButton: true);
            return new("Right mouse click complete.");
        }
        if (HasPrefix(s, "WIN_MOUSE_CLICK_"))
        {
            var a = Args(s, "WIN_MOUSE_CLICK_", 2);
            _driver.MouseClick(a[0], a[1]);
            return new("Mouse click complete.");
        }
        foreach (var (prefix, sign, horizontal) in new[]
        {
            ("WIN_MOUSE_SCROLL_UP_", 1, false), ("WIN_MOUSE_SCROLL_DOWN_", -1, false),
            ("WIN_MOUSE_SCROLL_RIGHT_", 1, true), ("WIN_MOUSE_SCROLL_LEFT_", -1, true)
        })
        {
            if (!HasPrefix(s, prefix)) continue;
            int count = Num(s[prefix.Length..]);
            _driver.MouseScroll(checked(count * sign), horizontal);
            return new("Mouse wheel input complete.");
        }
        if (HasPrefix(s, "WIN_ZOOM_IN_") || HasPrefix(s, "WIN_ZOOM_OUT_"))
        {
            bool zoomIn = HasPrefix(s, "WIN_ZOOM_IN_");
            string prefix = zoomIn ? "WIN_ZOOM_IN_" : "WIN_ZOOM_OUT_";
            var a = Args(s, prefix, 2, 3);
            _driver.Zoom(a[0], a[1], zoomIn, a.Length == 3 ? a[2] : 500);
            return new(zoomIn ? "Two-finger spread complete." : "Two-finger pinch complete.");
        }
        throw new ArgumentException($"Unknown command '{s}'. Enter WIN_HELP for the full list.");
    }

    private static bool HasPrefix(string value, string prefix) => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    private static int Num(string raw) => int.TryParse(raw, NumberStyles.Integer, Inv, out var result)
        ? result : throw new ArgumentException($"Invalid integer '{raw}'.");
    private static int Fps(string raw)
    {
        int fps = Num(raw);
        if (fps < 1 || fps > 10) throw new ArgumentOutOfRangeException(nameof(raw), "FPS must be between 1 and 10.");
        return fps;
    }
    private static int[] Args(string command, string prefix, int minCount, int? maxCount = null)
    {
        var parts = command[prefix.Length..].Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < minCount || parts.Length > (maxCount ?? minCount))
            throw new ArgumentException($"Expected {minCount}{(maxCount > minCount ? $"–{maxCount}" : "")} comma-separated numbers after {prefix}");
        return parts.Select(Num).ToArray();
    }
}
