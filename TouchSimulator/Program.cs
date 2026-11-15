using System.Windows.Forms;
using TouchSimulator.Driver;
using TouchSimulator.Commands;

namespace TouchSimulator;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Failures during startup must be visible even when launched by double-click.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            StartupLog.Write("Unhandled application error", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLog.Write("Unobserved task error", args.Exception);
            args.SetObserved();
        };
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (_, args) =>
            {
                StartupLog.Write("UI error", args.Exception);
                ShowError(args.Exception);
            };

            using IWindowsDriver driver = new User32WindowsDriver();
            var processor = new CommandProcessor(driver);
            Application.Run(new MainForm(driver, processor));
        }
        catch (Exception ex)
        {
            StartupLog.Write("Startup failure", ex);
            ShowError(ex);
        }
    }

    private static void ShowError(Exception ex)
    {
        try
        {
            MessageBox.Show(
                "Touch Simulator encountered an error:\n" + ex.Message +
                "\n\nDiagnostic log: " + StartupLog.Path,
                "Touch Simulator — error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { /* Log is the fallback when a GUI message cannot be displayed. */ }
    }
}
