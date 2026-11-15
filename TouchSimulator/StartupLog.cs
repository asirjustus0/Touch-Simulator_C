namespace TouchSimulator;

/// <summary>Best-effort persistent diagnostics for startup and unhandled exceptions.</summary>
internal static class StartupLog
{
    internal static string Path
    {
        get
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(folder)) folder = System.IO.Path.GetTempPath();
            return System.IO.Path.Combine(folder, "TouchSimulator", "logs", "startup.log");
        }
    }

    internal static void Write(string context, Exception? ex)
    {
        try
        {
            string destination = Path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            File.AppendAllText(destination,
                $"[{DateTimeOffset.Now:O}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* Logging must not cause another startup failure. */ }
    }
}
