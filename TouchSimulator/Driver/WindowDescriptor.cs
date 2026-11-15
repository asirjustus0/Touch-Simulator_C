namespace TouchSimulator.Driver;

/// <summary>Identity of a visible top-level application window.</summary>
public sealed record WindowDescriptor(IntPtr Handle, uint ProcessId, string ProcessName, string Title)
{
    public override string ToString() => $"{ProcessName} — {Title} [HWND 0x{Handle.ToInt64():X}]";
}
