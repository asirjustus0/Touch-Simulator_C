using System.Drawing;
using System.Drawing.Imaging;
using TouchSimulator.Commands;
using TouchSimulator.Driver;
using TouchSimulator.Recording;

namespace TouchSimulator;

public sealed class MainForm : Form
{
    private readonly IWindowsDriver _driver;
    private readonly CommandProcessor _processor;
    private readonly TextBox _input = new();
    private readonly TextBox _log = new();
    private readonly ListBox _examples = new();
    private readonly ComboBox _targetPicker = new();
    private SplitContainer? _split;
    private readonly PictureBox _preview = new();
    private readonly Button _execute = new();
    private readonly Label _previewLabel = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly System.Windows.Forms.Timer _frameTimer = new();
    private readonly CheckBox _tapOnPreview = new();
    private Bitmap? _current;
    private Rectangle _capturedBounds;
    private MjpegAviRecorder? _recorder;
    private bool _monitorEnabled;
    private int _monitorFps = 5;
    private int _recordFps = 5;
    private DateTime _lastRecordedAt = DateTime.MinValue;
    private int _captureBusy;
    private bool _commandBusy;

    public MainForm(IWindowsDriver driver, CommandProcessor processor)
    {
        _driver = driver;
        _processor = processor;
        Text = "Touch Simulator — C# Windows Driver + Command Console";
        MinimumSize = new Size(980, 640);
        Width = 1320;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        BuildUi();
        _frameTimer.Interval = 200;
        _frameTimer.Tick += CaptureTimerTick;
        Log("Ready. Choose an example or type WIN_SCREEN_INFO. All coordinates are screen pixels until you select a target window.");
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        // IMPORTANT: do not set SplitterDistance or panel minimum sizes until layout.
        // The container is initially 0px wide; assigning a fixed distance here can
        // throw ArgumentOutOfRangeException before the form becomes visible.
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        _split = split;
        root.Controls.Add(split, 0, 0);

        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(4)
        };
        split.Panel1.Controls.Add(sidebar);

        void Add(Control control)
        {
            control.Margin = new Padding(0, 4, 6, 4);
            sidebar.Controls.Add(control);
        }
        Add(new Label { Text = "WINDOWS TOUCH SIMULATOR", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = false, Height = 34, Width = 360 });
        Add(new Label { Text = "C# / .NET 10 · native user32.dll · no Python runtime", AutoSize = false, Width = 360, Height = 25, ForeColor = Color.DimGray });
        Add(new Label { Text = "Command", Font = new Font("Segoe UI", 10, FontStyle.Bold), Width = 360, Height = 22 });
        _input.Width = 360;
        _input.Height = 34;
        _input.Font = new Font("Consolas", 10);
        _input.Text = "WIN_SCREENSHOT";
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RunCommandAsync(_input.Text); }
        };
        Add(_input);
        _execute.Width = 360;
        _execute.Height = 39;
        _execute.Text = "Execute command";
        _execute.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _execute.Click += async (_, _) => await RunCommandAsync(_input.Text);
        Add(_execute);

        Add(new Label { Text = "Target application / window", Font = new Font("Segoe UI", 9, FontStyle.Bold), Width = 360, Height = 23 });
        var targetGrid = new TableLayoutPanel { Width = 360, Height = 68, ColumnCount = 2, RowCount = 2 };
        targetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        targetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _targetPicker.Dock = DockStyle.Fill;
        _targetPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _targetPicker.DropDownWidth = 530;
        targetGrid.Controls.Add(_targetPicker, 0, 0);
        targetGrid.SetColumnSpan(_targetPicker, 2);
        var refreshTargets = new Button { Text = "Refresh windows", Dock = DockStyle.Fill };
        refreshTargets.Click += (_, _) => RefreshTargets();
        targetGrid.Controls.Add(refreshTargets, 0, 1);
        var selectTarget = new Button { Text = "Select target", Dock = DockStyle.Fill };
        selectTarget.Click += (_, _) =>
        {
            if (_targetPicker.SelectedItem is not WindowDescriptor item) { Log("Choose a window first."); return; }
            try
            {
                _driver.SelectWindowByHandle(item.Handle);
                Log($"Selected {item.ProcessName}: {_driver.TargetWindowTitle}");
                SetStatus();
            }
            catch (Exception ex) { Log("Target selection failed: " + ex.Message); }
        };
        targetGrid.Controls.Add(selectTarget, 1, 1);
        Add(targetGrid);

        var quick = new TableLayoutPanel { Width = 360, Height = 116, ColumnCount = 2, RowCount = 3 };
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        string[,] labels = { { "Screenshot", "Start live" }, { "Stop live", "Start AVI" }, { "Stop AVI", "Save PNG" } };
        string[,] commands = { { "WIN_SCREENSHOT", "WIN_MONITOR_START_5" }, { "WIN_MONITOR_STOP", "WIN_RECORD_START_5" }, { "WIN_RECORD_STOP", "" } };
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 2; col++)
            {
                string command = commands[row, col];
                var button = new Button { Text = labels[row, col], Dock = DockStyle.Fill, Margin = new Padding(2) };
                button.Click += async (_, _) =>
                {
                    if (command.Length == 0) SaveCurrentImage();
                    else await RunCommandAsync(command);
                };
                quick.Controls.Add(button, col, row);
            }
        Add(quick);

        _tapOnPreview.Text = "Touch at clicked position in screenshot";
        _tapOnPreview.AutoSize = true;
        Add(_tapOnPreview);
        Add(new Label { Text = "Command examples (double-click to execute)", Font = new Font("Segoe UI", 9, FontStyle.Bold), Width = 360, Height = 23 });
        _examples.Items.AddRange(CommandProcessor.Examples.Cast<object>().ToArray());
        _examples.Font = new Font("Consolas", 9);
        _examples.Width = 360;
        _examples.Height = 195;
        _examples.HorizontalScrollbar = true;
        _examples.SelectedIndexChanged += (_, _) =>
        {
            if (_examples.SelectedItem is string command) _input.Text = command;
        };
        _examples.DoubleClick += async (_, _) =>
        {
            if (_examples.SelectedItem is string command) await RunCommandAsync(command);
        };
        Add(_examples);
        Add(new Label { Text = "Execution log", Font = new Font("Segoe UI", 9, FontStyle.Bold), Width = 360, Height = 23 });
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9);
        _log.Width = 360;
        _log.Height = 155;
        Add(_log);

        var previewRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        split.Panel2.Controls.Add(previewRoot);
        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.Text = "Screenshot / Live Monitor / AVI Recording Preview";
        _previewLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _previewLabel.TextAlign = ContentAlignment.MiddleLeft;
        previewRoot.Controls.Add(_previewLabel, 0, 0);

        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(25, 30, 38);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.MouseClick += async (_, e) =>
        {
            if (!_tapOnPreview.Checked) return;
            if (!TryMapPreviewToCommand(e.Location, out int x, out int y)) return;
            await RunCommandAsync($"WIN_TOUCH_{x},{y}");
        };
        previewRoot.Controls.Add(_preview, 0, 1);
        var note = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Screenshots copy visible pixels (covered/minimised windows are not captured). Touch input may interact with other running software.",
            ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft
        };
        previewRoot.Controls.Add(note, 0, 2);

        var status = new StatusStrip { SizingGrip = false };
        status.Items.Add(_status);
        root.Controls.Add(status, 0, 1);
        SetStatus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Delay splitter sizing until WinForms has calculated the actual client width.
        try
        {
            if (_split is not null)
            {
                int available = _split.ClientSize.Width - _split.SplitterWidth;
                if (available > 650)
                {
                    _split.SplitterDistance = Math.Clamp(395, 300, available - 350);
                    _split.Panel1MinSize = 300;
                    _split.Panel2MinSize = 350;
                }
            }
        }
        catch (Exception ex) { StartupLog.Write("Splitter layout", ex); Log("Layout warning: " + ex.Message); }
        RefreshTargets();
    }

    private void RefreshTargets()
    {
        try
        {
            var previous = (_targetPicker.SelectedItem as WindowDescriptor)?.Handle;
            var windows = _driver.GetWindows();
            _targetPicker.BeginUpdate();
            try
            {
                _targetPicker.Items.Clear();
                foreach (var item in windows) _targetPicker.Items.Add(item);
                if (previous is { } existing)
                    foreach (var item in _targetPicker.Items)
                        if (item is WindowDescriptor info && info.Handle == existing)
                        { _targetPicker.SelectedItem = item; break; }
                if (_targetPicker.SelectedIndex < 0 && _targetPicker.Items.Count > 0)
                    _targetPicker.SelectedIndex = 0;
            }
            finally { _targetPicker.EndUpdate(); }
            Log($"Found {windows.Count} visible application windows. To select File Explorer by process name, enter WIN_TARGET_APP_explorer.");
        }
        catch (Exception ex) { StartupLog.Write("Refresh window list", ex); Log("Window list failed: " + ex.Message); }
    }

    private async Task RunCommandAsync(string command)
    {
        if (_commandBusy) { Log("A command is already running."); return; }
        _commandBusy = true;
        _execute.Enabled = false;
        try
        {
            Log($"> {command}");
            CommandResult result = await Task.Run(() => _processor.Execute(command));
            switch (result.Action)
            {
                case CommandAction.Screenshot:
                    if (result.Image is not null) Display(result.Image);
                    break;
                case CommandAction.MonitorStart:
                    _monitorEnabled = true;
                    _monitorFps = result.Fps;
                    ConfigureTimer();
                    break;
                case CommandAction.MonitorStop:
                    _monitorEnabled = false;
                    ConfigureTimer();
                    break;
                case CommandAction.RecordStart:
                    StartRecording(result.Fps);
                    break;
                case CommandAction.RecordStop:
                    StopRecording();
                    break;
            }
            Log(result.Message);
            SetStatus();
        }
        catch (Exception ex) { StartupLog.Write("Command failed: " + command, ex); Log("ERROR: " + ex.Message); }
        finally { _commandBusy = false; _execute.Enabled = true; }
    }

    private void StartRecording(int fps)
    {
        if (_recorder is not null) throw new InvalidOperationException("Recording is already active. Use WIN_RECORD_STOP.");
        var r = _driver.CaptureBounds;
        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(videos)) videos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string path = Path.Combine(videos, "TouchSimulator", $"touch_{DateTime.Now:yyyyMMdd_HHmmss_fff}.avi");
        _recorder = new MjpegAviRecorder(path, r.Width, r.Height, fps);
        _recordFps = fps;
        _lastRecordedAt = DateTime.MinValue;
        ConfigureTimer();
        Log("Recording to: " + path);
    }

    private void StopRecording()
    {
        if (_recorder is null) { Log("No recording is active."); return; }
        var writer = _recorder;
        _recorder = null;
        try
        {
            writer.Dispose();
            Log($"Saved AVI ({writer.FramesWritten} frames): {writer.FilePath}");
        }
        finally { ConfigureTimer(); }
    }

    private void ConfigureTimer()
    {
        bool active = _monitorEnabled || _recorder is not null;
        _frameTimer.Enabled = active;
        if (active)
        {
            int fps = Math.Max(_monitorEnabled ? _monitorFps : 0, _recorder is not null ? _recordFps : 0);
            _frameTimer.Interval = Math.Max(100, 1000 / fps);
        }
        SetStatus();
    }

    private async void CaptureTimerTick(object? sender, EventArgs args)
    {
        if (Interlocked.Exchange(ref _captureBusy, 1) != 0) return;
        try
        {
            Bitmap frame = await Task.Run(() => _driver.Capture());
            try
            {
                if (_recorder is not null &&
                    (DateTime.UtcNow - _lastRecordedAt).TotalMilliseconds >= 1000.0 / _recordFps)
                {
                    _recorder.AddFrame(frame);
                    _lastRecordedAt = DateTime.UtcNow;
                }
                Display(frame);
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }
        catch (Exception ex)
        {
            _frameTimer.Stop();
            _monitorEnabled = false;
            Log("Capture failed: " + ex.Message);
            if (_recorder is not null)
            {
                try { StopRecording(); }
                catch (Exception e) { Log("AVI finalise error: " + e.Message); }
            }
        }
        finally { Interlocked.Exchange(ref _captureBusy, 0); }
    }

    private void Display(Bitmap bitmap)
    {
        Bitmap? prior = _current;
        _current = bitmap;
        _capturedBounds = _driver.CaptureBounds;
        _preview.Image = bitmap;
        _previewLabel.Text = $"Preview: {bitmap.Width} × {bitmap.Height} · Origin {_capturedBounds.X},{_capturedBounds.Y} · {DateTime.Now:HH:mm:ss}";
        prior?.Dispose();
    }

    private bool TryMapPreviewToCommand(Point click, out int x, out int y)
    {
        x = 0; y = 0;
        if (_current is null || _preview.ClientSize.Width < 1 || _preview.ClientSize.Height < 1) return false;
        float scale = Math.Min((float)_preview.ClientSize.Width / _current.Width, (float)_preview.ClientSize.Height / _current.Height);
        float w = _current.Width * scale, h = _current.Height * scale;
        float left = (_preview.ClientSize.Width - w) / 2, top = (_preview.ClientSize.Height - h) / 2;
        if (click.X < left || click.Y < top || click.X >= left + w || click.Y >= top + h) return false;
        int localX = Math.Clamp((int)((click.X - left) / scale), 0, _current.Width - 1);
        int localY = Math.Clamp((int)((click.Y - top) / scale), 0, _current.Height - 1);
        if (_driver.TargetWindowTitle is null)
        {
            x = _capturedBounds.X + localX;
            y = _capturedBounds.Y + localY;
        }
        else { x = localX; y = localY; }
        return true;
    }

    private void SaveCurrentImage()
    {
        if (_current is null) { Log("Capture a screenshot first."); return; }
        using var dialog = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", FileName = $"touch_{DateTime.Now:yyyyMMdd_HHmmss}.png" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { _current.Save(dialog.FileName, ImageFormat.Png); Log("Saved screenshot: " + dialog.FileName); }
        catch (Exception ex) { Log("Screenshot save failed: " + ex.Message); }
    }

    private void Log(string message)
    {
        if (IsDisposed) return;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void SetStatus()
    {
        _status.Text = $"Target: {_driver.TargetWindowTitle ?? "Desktop"}     Live: {(_monitorEnabled ? "ON" : "OFF")}     " +
                       $"AVI: {(_recorder is not null ? $"RECORDING ({_recorder.FramesWritten} frames)" : "OFF")}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _frameTimer.Stop();
        if (_recorder is not null)
        {
            try { StopRecording(); }
            catch (Exception ex) { Log("Closing recorder: " + ex.Message); }
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frameTimer.Dispose();
            _current?.Dispose();
        }
        base.Dispose(disposing);
    }
}
