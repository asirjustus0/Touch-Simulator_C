using System.Drawing;
using TouchSimulator.Commands;
using TouchSimulator.Driver;

var driver = new FakeWindowsDriver();
var commands = new CommandProcessor(driver);
var checks = new List<(string command, string expected)>
{
    ("WIN_TOUCH_20,30", "Touch"),
    ("WIN_DOUBLE_TOUCH_20,30", "DoubleTouch"),
    ("WIN_MULTI_TOUCH_20,30|60,80@120", "MultiTouch"),
    ("WIN_SWIPE_20,30,60,80,450", "Swipe"),
    ("WIN_TOUCH_DRAG_20,30,60,80,650", "TouchDrag"),
    ("WIN_MOUSE_CLICK_20,30", "MouseClick"),
    ("WIN_MOUSE_DOUBLE_CLICK_20,30", "MouseClick"),
    ("WIN_MOUSE_MULTI_CLICK_20,30,3", "MouseClick"),
    ("WIN_MOUSE_RIGHT_CLICK_20,30", "MouseClick"),
    ("WIN_MOUSE_MOVE_20,30", "MouseMove"),
    ("WIN_MOUSE_DRAG_20,30,60,80", "MouseDrag"),
    ("WIN_DRAG_DROP_20,30,60,80", "MouseDrag"),
    ("WIN_MOUSE_SCROLL_DOWN_3", "MouseScroll"),
    ("WIN_MOUSE_SCROLL_UP_3", "MouseScroll"),
    ("WIN_MOUSE_SCROLL_LEFT_3", "MouseScroll"),
    ("WIN_MOUSE_SCROLL_RIGHT_3", "MouseScroll"),
    ("WIN_ZOOM_IN_300,300", "Zoom"),
    ("WIN_ZOOM_OUT_300,300", "Zoom"),
    ("WIN_TARGET_WINDOW_Notepad", "SelectWindow"),
    ("WIN_TARGET_APP_explorer", "SelectApplication"),
    ("WIN_TARGET_HANDLE_0x1234", "SelectWindowByHandle"),
    ("WIN_TARGET_CLEAR", "ClearWindowTarget"),
    ("WIN_FOCUS_TARGET", "FocusTargetWindow"),
    ("WIN_SCREEN_INFO", "GetScreenInfo"),
    ("WIN_DIAGNOSTICS", "GetDiagnostics"),
    ("WIN_WINDOWS", "GetWindowTitles"),
    ("WIN_APPS", "GetWindows")
};
int total = 0;
foreach (var (command, expected) in checks)
{
    driver.LastCall = string.Empty;
    commands.Execute(command);
    if (driver.LastCall != expected) throw new Exception($"FAILED {command}: {driver.LastCall}, expected {expected}");
    total++;
}
foreach (var (command, action) in new[]
{
    ("WIN_SCREENSHOT", CommandAction.Screenshot),
    ("WIN_MONITOR_START_5", CommandAction.MonitorStart),
    ("WIN_MONITOR_STOP", CommandAction.MonitorStop),
    ("WIN_RECORD_START_5", CommandAction.RecordStart),
    ("WIN_RECORD_STOP", CommandAction.RecordStop)
})
{
    var result = commands.Execute(command);
    if (result.Action != action) throw new Exception($"FAILED {command} action");
    result.Image?.Dispose();
    total++;
}
foreach (var invalid in new[] { "WIN_SWIPE_4,5,6", "WIN_MONITOR_START_50", "WIN_MULTI_TOUCH_5,6", "WIN_MOUSE_MULTI_CLICK_10,10,0", "INVALID" })
{
    try { commands.Execute(invalid); throw new Exception("Expected rejection for " + invalid); }
    catch (ArgumentException) { total++; }
}
Console.WriteLine($"PASS: {total} command routing and validation checks. No real input was injected.");

internal sealed class FakeWindowsDriver : IWindowsDriver
{
    public string LastCall = "";
    public string? TargetWindowTitle => "Notepad";
    public Rectangle CaptureBounds => new(0, 0, 800, 600);
    public string GetScreenInfo() { LastCall = nameof(GetScreenInfo); return "800x600"; }
    public string GetDiagnostics() { LastCall = nameof(GetDiagnostics); return "fake sizes"; }
    public IReadOnlyList<string> GetWindowTitles() { LastCall = nameof(GetWindowTitles); return new[] { "Notepad" }; }
    public IReadOnlyList<WindowDescriptor> GetWindows()
    {
        LastCall = nameof(GetWindows);
        return new[] { new WindowDescriptor(new IntPtr(0x1234), 101, "explorer", "Documents") };
    }
    public void SelectApplication(string processName) => LastCall = nameof(SelectApplication);
    public void SelectWindowByHandle(IntPtr hwnd) => LastCall = nameof(SelectWindowByHandle);
    public void SelectWindow(string title) => LastCall = nameof(SelectWindow);
    public void ClearWindowTarget() => LastCall = nameof(ClearWindowTarget);
    public void FocusTargetWindow() => LastCall = nameof(FocusTargetWindow);
    public Point ToScreen(int x, int y) => new(x, y);
    public void Touch(int x, int y, int ms = 70) => LastCall = nameof(Touch);
    public void DoubleTouch(int x, int y) => LastCall = nameof(DoubleTouch);
    public void MultiTouch(IReadOnlyList<Point> points, int ms = 120) => LastCall = nameof(MultiTouch);
    public void Swipe(int x1, int y1, int x2, int y2, int ms = 450) => LastCall = nameof(Swipe);
    public void TouchDrag(int x1, int y1, int x2, int y2, int ms = 900) => LastCall = nameof(TouchDrag);
    public void Zoom(int x, int y, bool zoomIn, int ms = 500) => LastCall = nameof(Zoom);
    public void MouseMove(int x, int y) => LastCall = nameof(MouseMove);
    public void MouseClick(int x, int y, int count = 1, bool rightButton = false)
    {
        if (count < 1) throw new ArgumentException("Count must be positive.");
        LastCall = nameof(MouseClick);
    }
    public void MouseDrag(int x1, int y1, int x2, int y2, int ms = 700) => LastCall = nameof(MouseDrag);
    public void MouseScroll(int amount, bool horizontal = false) => LastCall = nameof(MouseScroll);
    public Bitmap Capture() { LastCall = nameof(Capture); return new Bitmap(5, 5); }
    public void Dispose() { }
}
