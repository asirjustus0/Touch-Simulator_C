using System.Drawing;

namespace TouchSimulator.Driver;

/// <summary>Abstraction to allow alternative Windows input backends and future actor wrappers.</summary>
public interface IWindowsDriver : IDisposable
{
    string? TargetWindowTitle { get; }
    Rectangle CaptureBounds { get; }
    string GetScreenInfo();
    string GetDiagnostics();
    IReadOnlyList<string> GetWindowTitles();
    IReadOnlyList<WindowDescriptor> GetWindows();
    void SelectWindow(string partialTitle);
    void SelectApplication(string processName);
    void SelectWindowByHandle(IntPtr windowHandle);
    void ClearWindowTarget();
    void FocusTargetWindow();
    Point ToScreen(int x, int y);
    void Touch(int x, int y, int holdMilliseconds = 70);
    void DoubleTouch(int x, int y);
    void MultiTouch(IReadOnlyList<Point> contacts, int holdMilliseconds = 120);
    void Swipe(int x1, int y1, int x2, int y2, int durationMilliseconds = 450);
    void TouchDrag(int x1, int y1, int x2, int y2, int durationMilliseconds = 900);
    void Zoom(int centreX, int centreY, bool zoomIn, int durationMilliseconds = 500);
    void MouseMove(int x, int y);
    void MouseClick(int x, int y, int count = 1, bool rightButton = false);
    void MouseDrag(int x1, int y1, int x2, int y2, int durationMilliseconds = 700);
    void MouseScroll(int detents, bool horizontal = false);
    Bitmap Capture();
}
