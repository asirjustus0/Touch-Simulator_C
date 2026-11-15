using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using W = TouchSimulator.Driver.WindowsNative;

namespace TouchSimulator.Driver;

/// <summary>
/// Windows input driver. All coordinates are physical desktop pixels, except when a
/// target is selected: coordinates then refer to that window's client area.
/// </summary>
public sealed class User32WindowsDriver : IWindowsDriver
{
    private readonly object _inputLock = new();
    private readonly object _targetLock = new();
    private IntPtr _targetWindow = IntPtr.Zero;
    private string? _targetTitle;
    private uint _targetProcessId;
    private bool _touchInitialized;
    private int? _lastTouchError;
    private const int MaxContacts = 10;

    public string? TargetWindowTitle
    {
        get
        {
            lock (_targetLock)
            {
                if (_targetWindow == IntPtr.Zero) return null;
                if (!TargetStillValid()) return _targetTitle + " (closed or replaced)";
                string current = WindowTitle(_targetWindow);
                return string.IsNullOrEmpty(current) ? _targetTitle : current;
            }
        }
    }

    private bool TargetStillValid() => W.IsWindow(_targetWindow) &&
        W.GetWindowThreadProcessId(_targetWindow, out uint pid) != 0 && pid == _targetProcessId;

    private static string WindowTitle(IntPtr hwnd)
    {
        int length = W.GetWindowTextLengthW(hwnd);
        if (length <= 0) return string.Empty;
        var text = new StringBuilder(length + 1);
        W.GetWindowTextW(hwnd, text, text.Capacity);
        return text.ToString().Trim();
    }

    public Rectangle CaptureBounds
    {
        get
        {
            lock (_targetLock)
            {
                if (_targetWindow == IntPtr.Zero)
                    return new Rectangle(
                        W.GetSystemMetrics(W.SM_XVIRTUALSCREEN),
                        W.GetSystemMetrics(W.SM_YVIRTUALSCREEN),
                        W.GetSystemMetrics(W.SM_CXVIRTUALSCREEN),
                        W.GetSystemMetrics(W.SM_CYVIRTUALSCREEN));

                if (!TargetStillValid())
                    throw new InvalidOperationException("Target window closed or its handle was reused. Select the window again.");
                if (!W.GetClientRect(_targetWindow, out var rc))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetClientRect failed");
                var origin = new W.POINT(0, 0);
                if (!W.ClientToScreen(_targetWindow, ref origin))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "ClientToScreen failed");
                int width = rc.Right - rc.Left;
                int height = rc.Bottom - rc.Top;
                if (width <= 0 || height <= 0) throw new InvalidOperationException("Target window has no visible client area.");
                return new Rectangle(origin.X, origin.Y, width, height);
            }
        }
    }

    public string GetScreenInfo()
    {
        var r = CaptureBounds;
        return $"Target: {TargetWindowTitle ?? "entire virtual desktop"}; origin=({r.X},{r.Y}); size={r.Width}x{r.Height}; " +
               $"touch injection initialized={_touchInitialized}; last touch error={(_lastTouchError?.ToString() ?? "none")}";
    }

    public string GetDiagnostics()
    {
        int inputSize = Marshal.SizeOf<W.INPUT>();
        int pointerSize = Marshal.SizeOf<W.POINTER_INFO>();
        int touchSize = Marshal.SizeOf<W.POINTER_TOUCH_INFO>();
        bool sizesMatch = IntPtr.Size == 8 && inputSize == 40 && pointerSize == 96 && touchSize == 144;
        return $"Process {(IntPtr.Size * 8)}-bit; INPUT={inputSize} (expected 40); " +
               $"POINTER_INFO={pointerSize} (expected 96); POINTER_TOUCH_INFO={touchSize} (expected 144); " +
               $"structures={(sizesMatch ? "PASS" : "MISMATCH")}; " +
               $"display={CaptureBounds.Width}x{CaptureBounds.Height}; " +
               $"touch initialized={_touchInitialized}; " +
               $"last touch Win32 error={(_lastTouchError?.ToString() ?? "none")}.";
    }

    public IReadOnlyList<string> GetWindowTitles() => GetWindows().Select(w => w.Title).Distinct().OrderBy(x => x).ToList();

    public IReadOnlyList<WindowDescriptor> GetWindows() => FindWindows();

    public void SelectWindow(string partialTitle)
    {
        if (string.IsNullOrWhiteSpace(partialTitle)) throw new ArgumentException("Provide a title fragment.");
        var match = FindWindows().FirstOrDefault(w => w.Title.Contains(partialTitle, StringComparison.OrdinalIgnoreCase));
        if (match is null) throw new ArgumentException($"No visible window matching '{partialTitle}'. Use WIN_WINDOWS.");
        SelectWindowByHandle(match.Handle);
    }

    public void SelectApplication(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) throw new ArgumentException("Provide an application process name.");
        string needle = processName.Trim();
        if (needle.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) needle = needle[..^4];
        var matches = FindWindows()
            .Where(w => w.ProcessName.Equals(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            throw new ArgumentException($"No visible window owned by process '{needle}'. Use WIN_APPS or refresh the target list.");
        // For multiple windows choose the first enumeration match; use the dropdown
        // or WIN_TARGET_HANDLE to disambiguate a particular Explorer instance.
        SelectWindowByHandle(matches[0].Handle);
    }

    public void SelectWindowByHandle(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) throw new ArgumentException("Invalid window handle.");
        var match = FindWindows().FirstOrDefault(w => w.Handle == windowHandle);
        if (match is null) throw new ArgumentException("Window handle is no longer a visible titled window. Refresh the list.");
        lock (_targetLock)
        {
            _targetWindow = match.Handle;
            _targetTitle = match.Title;
            _targetProcessId = match.ProcessId;
        }
    }

    public void ClearWindowTarget()
    {
        lock (_targetLock) { _targetWindow = IntPtr.Zero; _targetTitle = null; _targetProcessId = 0; }
    }

    public void FocusTargetWindow()
    {
        IntPtr hwnd;
        lock (_targetLock) hwnd = _targetWindow;
        if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Select a window before focusing it.");
        lock (_targetLock)
            if (!TargetStillValid()) throw new InvalidOperationException("The target window has been closed or replaced.");
        if (!W.SetForegroundWindow(hwnd))
            throw new InvalidOperationException("Windows did not allow the target to become foreground. Try clicking the target manually.");
    }

    public Point ToScreen(int x, int y) => Map(x, y);

    // Resolve absolute coordinates without treating the desktop origin as (0,0).
    private Point Map(int x, int y)
    {
        if (TargetWindowTitle is not null)
        {
            var bounds = CaptureBounds;
            if (x < 0 || y < 0 || x >= bounds.Width || y >= bounds.Height)
                throw new ArgumentOutOfRangeException(nameof(x), $"Target client coordinate outside {bounds.Width}x{bounds.Height}.");
            return new Point(bounds.Left + x, bounds.Top + y);
        }
        var desktop = CaptureBounds;
        if (!desktop.Contains(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), "Absolute coordinate outside virtual desktop.");
        return new Point(x, y);
    }

    public void Touch(int x, int y, int holdMilliseconds = 70)
    {
        ValidateDuration(holdMilliseconds);
        TouchPath(new[] { new[] { Map(x, y) } }, 0, holdMilliseconds);
    }

    public void DoubleTouch(int x, int y)
    {
        Touch(x, y); Thread.Sleep(100); Touch(x, y);
    }

    public void MultiTouch(IReadOnlyList<Point> contacts, int holdMilliseconds = 120)
    {
        if (contacts.Count < 2 || contacts.Count > MaxContacts)
            throw new ArgumentOutOfRangeException(nameof(contacts), "Provide between 2 and 10 touch points.");
        ValidateDuration(holdMilliseconds);
        TouchPath(new[] { contacts.Select(p => Map(p.X, p.Y)).ToArray() }, 0, holdMilliseconds);
    }

    public void Swipe(int x1, int y1, int x2, int y2, int durationMilliseconds = 450)
    {
        ValidateDuration(durationMilliseconds);
        TouchPath(CreatePath(Map(x1, y1), Map(x2, y2), durationMilliseconds), durationMilliseconds, 0);
    }

    public void TouchDrag(int x1, int y1, int x2, int y2, int durationMilliseconds = 900)
    {
        ValidateDuration(durationMilliseconds);
        TouchPath(CreatePath(Map(x1, y1), Map(x2, y2), durationMilliseconds), durationMilliseconds, 250);
    }

    public void Zoom(int centreX, int centreY, bool zoomIn, int durationMilliseconds = 500)
    {
        ValidateDuration(durationMilliseconds);
        // Genuine 2-contact pinch/spread: each frame contains BOTH contacts.
        var frames = new List<Point[]>();
        int steps = Math.Clamp(durationMilliseconds / 16, 5, 120);
        for (int index = 0; index <= steps; index++)
        {
            double t = (double)index / steps;
            int radius = (int)Math.Round(zoomIn ? 35 + 90 * t : 125 - 90 * t);
            frames.Add(new[] { Map(centreX - radius, centreY), Map(centreX + radius, centreY) });
        }
        TouchPath(frames, durationMilliseconds, 0);
    }

    public void MouseMove(int x, int y)
    {
        var p = Map(x, y);
        lock (_inputLock) SetCursor(p);
    }

    public void MouseClick(int x, int y, int count = 1, bool rightButton = false)
    {
        if (count < 1 || count > 20) throw new ArgumentOutOfRangeException(nameof(count), "Click count must be between 1 and 20.");
        var point = Map(x, y);
        lock (_inputLock)
        {
            SetCursor(point);
            for (int i = 0; i < count; i++)
            {
                MouseEvent(rightButton ? W.MOUSEEVENTF_RIGHTDOWN : W.MOUSEEVENTF_LEFTDOWN);
                Thread.Sleep(35);
                MouseEvent(rightButton ? W.MOUSEEVENTF_RIGHTUP : W.MOUSEEVENTF_LEFTUP);
                if (i + 1 < count) Thread.Sleep(70);
            }
        }
    }

    public void MouseDrag(int x1, int y1, int x2, int y2, int durationMilliseconds = 700)
    {
        ValidateDuration(durationMilliseconds);
        var a = Map(x1, y1);
        var b = Map(x2, y2);
        int steps = Math.Clamp(durationMilliseconds / 15, 5, 150);
        lock (_inputLock)
        {
            SetCursor(a);
            MouseEvent(W.MOUSEEVENTF_LEFTDOWN);
            try
            {
                Thread.Sleep(140);
                for (int i = 1; i <= steps; i++)
                {
                    SetCursor(Interpolate(a, b, (double)i / steps));
                    Thread.Sleep(Math.Max(1, durationMilliseconds / steps));
                }
            }
            finally { MouseEvent(W.MOUSEEVENTF_LEFTUP); }
        }
    }

    public void MouseScroll(int detents, bool horizontal = false)
    {
        if (detents == 0 || Math.Abs((long)detents) > 50)
            throw new ArgumentOutOfRangeException(nameof(detents), "Wheel ticks must be between -50 and 50, excluding zero.");
        lock (_inputLock)
        {
            if (TargetWindowTitle is not null)
            {
                var target = CaptureBounds;
                SetCursor(new Point(target.Left + target.Width / 2, target.Top + target.Height / 2));
            }
            // SendInput MOUSEINPUT.mouseData is unsigned; wheel delta is signed.
            MouseEvent(horizontal ? W.MOUSEEVENTF_HWHEEL : W.MOUSEEVENTF_WHEEL, unchecked((uint)(detents * W.WHEEL_DELTA)));
        }
    }

    public Bitmap Capture()
    {
        var rectangle = CaptureBounds;
        var bitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format24bppRgb);
        try
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
                graphics.CopyFromScreen(rectangle.Location, Point.Empty, rectangle.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    private void EnsureTouch()
    {
        if (_touchInitialized) return;
        if (IntPtr.Size != 8 || Marshal.SizeOf<W.POINTER_INFO>() != 96 ||
            Marshal.SizeOf<W.POINTER_TOUCH_INFO>() != 144)
            throw new InvalidOperationException("Touch P/Invoke structures have unexpected sizes. Run WIN_DIAGNOSTICS.");
        if (!W.InitializeTouchInjection(MaxContacts, W.TOUCH_FEEDBACK_NONE))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeTouchInjection failed (run WIN_DIAGNOSTICS)");
        _touchInitialized = true;
    }

    private static IReadOnlyList<Point[]> CreatePath(Point a, Point b, int duration)
    {
        int steps = Math.Clamp(duration / 16, 5, 120);
        return Enumerable.Range(0, steps + 1)
            .Select(i => new[] { Interpolate(a, b, (double)i / steps) }).ToList();
    }

    private static Point Interpolate(Point a, Point b, double t) => new(
        (int)Math.Round(a.X + (b.X - a.X) * t),
        (int)Math.Round(a.Y + (b.Y - a.Y) * t));

    /// <summary>
    /// A contact must be DOWN first, then UPDATE frames while pressed, followed
    /// by UP at the last successfully injected position. For a static hold,
    /// send periodic UPDATE frames or Windows can cancel the touch contact.
    /// </summary>
    private void TouchPath(IReadOnlyList<Point[]> frames, int duration, int initialHold)
    {
        if (frames.Count == 0 || frames[0].Length == 0) throw new ArgumentException("Empty touch path");
        int contacts = frames[0].Length;
        if (frames.Any(frame => frame.Length != contacts)) throw new ArgumentException("Each touch frame must contain all active contacts.");
        lock (_inputLock)
        {
            EnsureTouch();
            var lastSuccessfulFrame = frames[0];
            // A failed DOWN must not be followed by UP.
            Inject(lastSuccessfulFrame, W.POINTER_FLAG_DOWN | W.POINTER_FLAG_INRANGE | W.POINTER_FLAG_INCONTACT, "DOWN");
            try
            {
                if (initialHold > 0)
                {
                    int remaining = initialHold;
                    while (remaining > 0)
                    {
                        int wait = Math.Min(25, remaining);
                        Thread.Sleep(wait);
                        remaining -= wait;
                        Inject(lastSuccessfulFrame, W.POINTER_FLAG_UPDATE | W.POINTER_FLAG_INRANGE | W.POINTER_FLAG_INCONTACT, "HOLD/UPDATE");
                    }
                }
                for (int i = 1; i < frames.Count; i++)
                {
                    Thread.Sleep(Math.Max(1, duration / (frames.Count - 1)));
                    Inject(frames[i], W.POINTER_FLAG_UPDATE | W.POINTER_FLAG_INRANGE | W.POINTER_FLAG_INCONTACT, "MOVE/UPDATE");
                    lastSuccessfulFrame = frames[i];
                }
            }
            catch
            {
                // Preserve the original error. A failed UPDATE can cancel contacts,
                // so cleanup UP is best-effort and uses the last accepted location.
                try { Inject(lastSuccessfulFrame, W.POINTER_FLAG_UP, "CLEANUP/UP"); }
                catch { /* Original error is more useful. */ }
                throw;
            }
            Inject(lastSuccessfulFrame, W.POINTER_FLAG_UP, "UP");
        }
    }

    private void Inject(Point[] contacts, uint flags, string phase)
    {
        var pointers = new W.POINTER_TOUCH_INFO[contacts.Length];
        // Touch contacts must lie within the virtual desktop, independent of
        // the selected target window's local coordinate system.
        Rectangle desktop = new(
            W.GetSystemMetrics(W.SM_XVIRTUALSCREEN),
            W.GetSystemMetrics(W.SM_YVIRTUALSCREEN),
            W.GetSystemMetrics(W.SM_CXVIRTUALSCREEN),
            W.GetSystemMetrics(W.SM_CYVIRTUALSCREEN));
        for (int i = 0; i < contacts.Length; i++)
        {
            int x = contacts[i].X, y = contacts[i].Y;
            if (!desktop.Contains(x, y))
                throw new ArgumentOutOfRangeException(nameof(contacts), $"Touch contact ({x},{y}) outside desktop {desktop}.");
            pointers[i] = new W.POINTER_TOUCH_INFO
            {
                pointerInfo = new W.POINTER_INFO
                {
                    pointerType = W.PT_TOUCH,
                    pointerId = (uint)i,
                    pointerFlags = flags,
                    ptPixelLocation = new W.POINT(x, y)
                },
                touchMask = W.TOUCH_MASK_CONTACTAREA | W.TOUCH_MASK_ORIENTATION | W.TOUCH_MASK_PRESSURE,
                rcContact = new W.RECT
                {
                    Left = Math.Max(desktop.Left, x - 2), Top = Math.Max(desktop.Top, y - 2),
                    Right = Math.Min(desktop.Right, x + 2), Bottom = Math.Min(desktop.Bottom, y + 2)
                },
                orientation = 90,
                pressure = 32000
            };
        }
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (W.InjectTouchInput((uint)pointers.Length, pointers))
            {
                _lastTouchError = null;
                return;
            }
            int error = Marshal.GetLastWin32Error();
            _lastTouchError = error;
            // ERROR_NOT_READY (21) can occur when frames are injected too quickly.
            // Microsoft documents retrying the exact same frame in this situation.
            if (error == 21 && attempt < 4) { Thread.Sleep(2); continue; }
            string detail = error switch
            {
                87 => "Invalid touch frame or contact coordinates. Check WIN_DIAGNOSTICS and screen bounds.",
                5 => "Access denied. Check integrity level, elevated targets and the interactive desktop.",
                21 => "Windows is not ready for the next frame. Retry after a short pause.",
                50 => "Touch injection is not supported in this environment/session.",
                _ => "Check WIN_DIAGNOSTICS and the startup log for details."
            };
            throw new Win32Exception(error,
                $"InjectTouchInput {phase} failed: Win32 error {error} ({new Win32Exception(error).Message}). {detail}");
        }
    }

    private static void SetCursor(Point p)
    {
        if (!W.SetCursorPos(p.X, p.Y)) throw new Win32Exception(Marshal.GetLastWin32Error(), "SetCursorPos failed");
    }

    private static void MouseEvent(uint flags, uint wheelData = 0)
    {
        var input = new W.INPUT
        {
            type = W.INPUT_MOUSE,
            union = new W.INPUT_UNION
            {
                mi = new W.MOUSEINPUT { dwFlags = flags, mouseData = wheelData }
            }
        };
        var data = new[] { input };
        if (W.SendInput(1, data, Marshal.SizeOf<W.INPUT>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed (check elevation/UIPI)");
    }

    private static List<WindowDescriptor> FindWindows()
    {
        var results = new List<WindowDescriptor>();
        W.EnumWindows((hwnd, _) =>
        {
            if (!W.IsWindowVisible(hwnd)) return true;
            string title = WindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return true;
            W.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return true;
            string name;
            try { using var proc = Process.GetProcessById(checked((int)pid)); name = proc.ProcessName; }
            catch (Exception ex) when (ex is Win32Exception or ArgumentException or InvalidOperationException)
            { name = $"pid:{pid}"; }
            results.Add(new WindowDescriptor(hwnd, pid, name, title));
            return true;
        }, IntPtr.Zero);
        return results;
    }

    private static void ValidateDuration(int ms)
    {
        if (ms < 20 || ms > 30_000) throw new ArgumentOutOfRangeException(nameof(ms), "Duration must be between 20 and 30000 ms.");
    }

    public void Dispose() { /* Stateless handles; Windows owns input injection context. */ }
}
