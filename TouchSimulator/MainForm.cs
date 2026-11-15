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
    private readonly PictureBox _preview = new();
    private readonly Button _execute = new();
    private readonly Button _touchPoint = new();
    private readonly Button _clickPoint = new();
    private readonly Button _copyPoint = new();
    private readonly Label _previewLabel = new();
    private readonly Label _coordLabel = new();
    private readonly Label _screenLabel = new();
    private readonly ToolStripStatusLabel _status = new();
    private readonly System.Windows.Forms.Timer _frameTimer = new();
    private readonly CheckBox _tapOnPreview = new();
    private readonly FlowLayoutPanel _sidebar = new();
    private readonly List<Control> _sideItems = new();
    private SplitContainer? _split;
    private CoordinateInspectorForm? _inspector;
    private Bitmap? _current;
    private Rectangle _capturedBounds;
    private bool _captureRelativeToTarget;
    private Point? _hoverCommand;
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
        Text = "Touch Simulator — Windows Driver Console v1.2";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(246, 248, 251);
        MinimumSize = new Size(1020, 690);
        Width = 1440;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        BuildUi();
        _frameTimer.Interval = 200;
        _frameTimer.Tick += CaptureTimerTick;
        Log("Ready. Select a target, capture a screenshot, then move over the preview for X/Y coordinates.");
    }

    private static Button ActionButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text, Dock = DockStyle.Fill, Height = 48,
            FlatStyle = FlatStyle.Flat, Margin = new Padding(4),
            BackColor = primary ? Color.FromArgb(29, 103, 186) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(39, 54, 75),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            MinimumSize = new Size(0, 40)
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(209, 216, 227);
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        return button;
    }

    private void AddSide(Control control)
    {
        control.Margin = new Padding(0, 4, 0, 8);
        _sideItems.Add(control);
        _sidebar.Controls.Add(control);
    }

    private void SizeSideItems()
    {
        int usable = Math.Max(320, _sidebar.ClientSize.Width - _sidebar.Padding.Horizontal - 20);
        foreach (var item in _sideItems)
            item.Width = usable;
    }

    private static Label SideTitle(string text) => new()
    {
        Text = text, Height = 30, TextAlign = ContentAlignment.BottomLeft,
        Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(45, 59, 79)
    };

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(12, 10, 12, 4) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(root);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 7 };
        _split = split;
        root.Controls.Add(split, 0, 0);
        _sidebar.Dock = DockStyle.Fill;
        _sidebar.FlowDirection = FlowDirection.TopDown;
        _sidebar.WrapContents = false;
        _sidebar.AutoScroll = true;
        _sidebar.Padding = new Padding(10, 4, 10, 4);
        _sidebar.BackColor = Color.FromArgb(239, 244, 250);
        _sidebar.Resize += (_, _) => SizeSideItems();
        split.Panel1.Controls.Add(_sidebar);

        AddSide(new Label
        {
            Text = "TOUCH SIMULATOR", Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.FromArgb(27, 59, 97), Height = 48, TextAlign = ContentAlignment.MiddleLeft
        });
        AddSide(new Label
        {
            Text = "Windows input driver · C# / .NET 10", Font = new Font("Segoe UI", 9),
            ForeColor = Color.DimGray, Height = 26
        });
        AddSide(SideTitle("Command console"));
        _input.AutoSize = false;
        _input.Height = 43;
        _input.Font = new Font("Consolas", 11);
        _input.Text = "WIN_SCREENSHOT";
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RunCommandAsync(_input.Text); }
        };
        AddSide(_input);
        _execute.Text = "Execute command";
        _execute.Height = 51;
        _execute.FlatStyle = FlatStyle.Flat;
        _execute.BackColor = Color.FromArgb(29, 103, 186);
        _execute.ForeColor = Color.White;
        _execute.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _execute.Cursor = Cursors.Hand;
        _execute.Click += async (_, _) => await RunCommandAsync(_input.Text);
        AddSide(_execute);

        AddSide(SideTitle("Target application / window"));
        var targetGrid = new TableLayoutPanel { Height = 112, ColumnCount = 2, RowCount = 2 };
        targetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        targetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        targetGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        targetGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        _targetPicker.Dock = DockStyle.Fill;
        _targetPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _targetPicker.DropDownWidth = 630;
        _targetPicker.Font = new Font("Segoe UI", 10);
        _targetPicker.Margin = new Padding(4, 8, 4, 5);
        targetGrid.Controls.Add(_targetPicker, 0, 0);
        targetGrid.SetColumnSpan(_targetPicker, 2);
        var refresh = ActionButton("Refresh windows");
        refresh.Click += (_, _) => RefreshTargets();
        targetGrid.Controls.Add(refresh, 0, 1);
        var select = ActionButton("Select target", true);
        select.Click += (_, _) =>
        {
            if (_targetPicker.SelectedItem is not WindowDescriptor item) { Log("Choose a window first."); return; }
            try
            {
                _driver.SelectWindowByHandle(item.Handle);
                InvalidatePreview();
                Log($"Selected {item.ProcessName}: {_driver.TargetWindowTitle}");
                SetStatus();
            }
            catch (Exception ex) { Log("Target selection failed: " + ex.Message); }
        };
        targetGrid.Controls.Add(select, 1, 1);
        AddSide(targetGrid);

        AddSide(SideTitle("Screenshot / live / AVI"));
        var quick = new TableLayoutPanel { Height = 168, ColumnCount = 2, RowCount = 3 };
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 3; r++) quick.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        string[,] labels = { { "Screenshot", "Start live" }, { "Stop live", "Start AVI" }, { "Stop AVI", "Save PNG" } };
        string[,] commands = { { "WIN_SCREENSHOT", "WIN_MONITOR_START_5" }, { "WIN_MONITOR_STOP", "WIN_RECORD_START_5" }, { "WIN_RECORD_STOP", "" } };
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 2; col++)
            {
                string command = commands[row, col];
                var button = ActionButton(labels[row, col]);
                button.Click += async (_, _) =>
                {
                    if (command.Length == 0) SaveCurrentImage();
                    else await RunCommandAsync(command);
                };
                quick.Controls.Add(button, col, row);
            }
        AddSide(quick);
        _tapOnPreview.Text = "Touch when clicking the screenshot";
        _tapOnPreview.Height = 40;
        _tapOnPreview.Font = new Font("Segoe UI", 10);
        AddSide(_tapOnPreview);
        AddSide(SideTitle("Command examples (double-click to run)"));
        _examples.Items.AddRange(CommandProcessor.Examples.Cast<object>().ToArray());
        _examples.Font = new Font("Consolas", 10);
        _examples.Height = 215;
        _examples.HorizontalScrollbar = true;
        _examples.SelectedIndexChanged += (_, _) =>
        {
            if (_examples.SelectedItem is string cmd) _input.Text = cmd;
        };
        _examples.DoubleClick += async (_, _) =>
        {
            if (_examples.SelectedItem is string cmd) await RunCommandAsync(cmd);
        };
        AddSide(_examples);
        AddSide(SideTitle("Execution log"));
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Both;
        _log.WordWrap = false;
        _log.Font = new Font("Consolas", 9.5f);
        _log.Height = 200;
        AddSide(_log);

        var previewRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12, 0, 2, 0) };
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 67));
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        previewRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        split.Panel2.Controls.Add(previewRoot);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
        _previewLabel.Dock = DockStyle.Fill;
        _previewLabel.Text = "Screenshot / Live Preview";
        _previewLabel.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        _previewLabel.TextAlign = ContentAlignment.MiddleLeft;
        _previewLabel.ForeColor = Color.FromArgb(27, 59, 97);
        heading.Controls.Add(_previewLabel, 0, 0);
        var openInspector = ActionButton("Open X/Y Window", true);
        openInspector.Click += (_, _) => ShowCoordinateInspector();
        heading.Controls.Add(openInspector, 1, 0);
        previewRoot.Controls.Add(heading, 0, 0);

        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.FromArgb(25, 30, 38);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.Cursor = Cursors.Cross;
        _preview.MouseMove += (_, e) => UpdateHover(e.Location);
        _preview.MouseLeave += (_, _) => SetHoverNotice("Move over the screenshot to inspect coordinates");
        _preview.MouseClick += async (_, e) =>
        {
            UpdateHover(e.Location);
            if (!_tapOnPreview.Checked || _hoverCommand is not { } p) return;
            await ExecuteAtPointAsync(p, touch: true);
        };
        previewRoot.Controls.Add(_preview, 0, 1);

        var coordinates = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 3, Padding = new Padding(2, 8, 2, 0) };
        coordinates.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        coordinates.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        coordinates.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var coordText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        coordText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        coordText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        _coordLabel.Text = "Command X/Y: —";
        _screenLabel.Text = "Screen X/Y: —";
        foreach (Label label in new[] { _coordLabel, _screenLabel })
        {
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }
        coordText.Controls.Add(_coordLabel, 0, 0);
        coordText.Controls.Add(_screenLabel, 1, 0);
        coordinates.Controls.Add(coordText, 0, 0);
        coordinates.SetColumnSpan(coordText, 3);
        _touchPoint.Text = "Touch at X/Y";
        _clickPoint.Text = "Mouse click at X/Y";
        _copyPoint.Text = "Copy touch command";
        foreach (Button button in new[] { _touchPoint, _clickPoint, _copyPoint })
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(3);
            button.Height = 46;
            button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            button.Enabled = false;
        }
        _touchPoint.Click += async (_, _) => { if (_hoverCommand is { } p) await ExecuteAtPointAsync(p, true); };
        _clickPoint.Click += async (_, _) => { if (_hoverCommand is { } p) await ExecuteAtPointAsync(p, false); };
        _copyPoint.Click += (_, _) =>
        {
            if (_hoverCommand is not { } p) return;
            string command = $"WIN_TOUCH_{p.X},{p.Y}";
            _input.Text = command;
            try { Clipboard.SetText(command); Log("Copied: " + command); }
            catch (Exception ex) { Log("Clipboard failed: " + ex.Message); }
        };
        coordinates.Controls.Add(_touchPoint, 0, 1);
        coordinates.Controls.Add(_clickPoint, 1, 1);
        coordinates.Controls.Add(_copyPoint, 2, 1);
        previewRoot.Controls.Add(coordinates, 0, 2);

        var note = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Coordinates exclude Zoom letterboxing. A fresh screenshot is needed after switching/moving the target. Covered windows cannot be captured.",
            ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9)
        };
        previewRoot.Controls.Add(note, 0, 3);
        var status = new StatusStrip { SizingGrip = false };
        status.Items.Add(_status);
        root.Controls.Add(status, 0, 1);
        SetStatus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            if (_split is { } split)
            {
                int usable = split.ClientSize.Width - split.SplitterWidth;
                if (usable > 820)
                {
                    split.SplitterDistance = Math.Clamp(445, 365, usable - 410);
                    split.Panel1MinSize = 355;
                    split.Panel2MinSize = 390;
                }
                SizeSideItems();
            }
        }
        catch (Exception ex) { StartupLog.Write("Splitter layout", ex); Log("Layout warning: " + ex.Message); }
        RefreshTargets();
    }

    private void ShowCoordinateInspector()
    {
        if (_inspector is null || _inspector.IsDisposed)
        {
            _inspector = new CoordinateInspectorForm();
            _inspector.TouchRequested += p => _ = ExecuteAtPointAsync(p, true);
            _inspector.ClickRequested += p => _ = ExecuteAtPointAsync(p, false);
            var area = Screen.FromControl(this).WorkingArea;
            _inspector.Location = new Point(Math.Min(area.Right - _inspector.Width, Right - _inspector.Width - 25),
                                             Math.Min(area.Bottom - _inspector.Height, Top + 95));
            _inspector.Show(this);
        }
        else { _inspector.Show(); _inspector.BringToFront(); }
        if (_hoverCommand is { } cmd)
        {
            var screen = _captureRelativeToTarget ? new Point(_capturedBounds.Left + cmd.X, _capturedBounds.Top + cmd.Y) : cmd;
            _inspector.UpdatePoint(cmd, screen);
        }
    }

    private void SetHoverNotice(string text)
    {
        if (_hoverCommand is null) _coordLabel.Text = text;
    }

    private void UpdateHover(Point mouse)
    {
        if (_current is null ||
            !PreviewCoordinateMapper.TryMap(mouse, _preview.ClientSize, _current.Size,
                _capturedBounds, _captureRelativeToTarget, out var commandPoint, out var screenPoint))
        {
            // Do not execute a stale location when the pointer is in black margins.
            _hoverCommand = null;
            _coordLabel.Text = "Command X/Y: —";
            _screenLabel.Text = "Screen X/Y: —";
            _touchPoint.Enabled = _clickPoint.Enabled = _copyPoint.Enabled = false;
            return;
        }
        _hoverCommand = commandPoint;
        _coordLabel.Text = $"Command X/Y: {commandPoint.X}, {commandPoint.Y}";
        _screenLabel.Text = $"Screen X/Y: {screenPoint.X}, {screenPoint.Y}";
        _touchPoint.Enabled = _clickPoint.Enabled = _copyPoint.Enabled = true;
        if (_inspector is { IsDisposed: false }) _inspector.UpdatePoint(commandPoint, screenPoint);
    }

    private async Task ExecuteAtPointAsync(Point p, bool touch)
    {
        if (_current is null) { Log("Capture a screenshot before selecting an image point."); return; }
        try
        {
            var now = _driver.CaptureBounds;
            if (now != _capturedBounds || (_driver.TargetWindowTitle is not null) != _captureRelativeToTarget)
            {
                Log("Target/window position changed. Take a new screenshot before using image coordinates.");
                return;
            }
        }
        catch (Exception ex) { Log("Target validation failed: " + ex.Message); return; }
        if (_captureRelativeToTarget)
        {
            try { _driver.FocusTargetWindow(); }
            catch (Exception ex)
            {
                Log("Cannot bring the selected target to the foreground: " + ex.Message);
                return;
            }
        }
        string command = $"{(touch ? "WIN_TOUCH" : "WIN_MOUSE_CLICK")}_{p.X},{p.Y}";
        _input.Text = command;
        await RunCommandAsync(command);
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
                    foreach (var entry in _targetPicker.Items)
                        if (entry is WindowDescriptor info && info.Handle == existing)
                        { _targetPicker.SelectedItem = entry; break; }
                if (_targetPicker.SelectedIndex < 0 && _targetPicker.Items.Count > 0)
                    _targetPicker.SelectedIndex = 0;
            }
            finally { _targetPicker.EndUpdate(); }
            Log($"Found {windows.Count} visible windows. Explorer by process: WIN_TARGET_APP_explorer.");
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
                    _monitorEnabled = true; _monitorFps = result.Fps; ConfigureTimer(); break;
                case CommandAction.MonitorStop:
                    _monitorEnabled = false; ConfigureTimer(); break;
                case CommandAction.RecordStart:
                    StartRecording(result.Fps); break;
                case CommandAction.RecordStop:
                    StopRecording(); break;
            }
            if (command.StartsWith("WIN_TARGET_", StringComparison.OrdinalIgnoreCase))
                InvalidatePreview();
            Log(result.Message);
            SetStatus();
        }
        catch (Exception ex) { StartupLog.Write("Command failed: " + command, ex); Log("ERROR: " + ex.Message); }
        finally { _commandBusy = false; _execute.Enabled = true; }
    }

    private void InvalidatePreview()
    {
        var old = _current;
        _current = null;
        _preview.Image = null;
        old?.Dispose();
        _hoverCommand = null;
        _coordLabel.Text = "Command X/Y: —";
        _screenLabel.Text = "Screen X/Y: —";
        _touchPoint.Enabled = _clickPoint.Enabled = _copyPoint.Enabled = false;
        _inspector?.ClearPoint();
        _previewLabel.Text = "Screenshot / Live Preview — refresh after selecting target";
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
        try { writer.Dispose(); Log($"Saved AVI ({writer.FramesWritten} frames): {writer.FilePath}"); }
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
            catch { frame.Dispose(); throw; }
        }
        catch (Exception ex)
        {
            _frameTimer.Stop(); _monitorEnabled = false;
            Log("Capture failed: " + ex.Message);
            if (_recorder is not null)
            {
                try { StopRecording(); }
                catch (Exception err) { Log("AVI finalise error: " + err.Message); }
            }
        }
        finally { Interlocked.Exchange(ref _captureBusy, 0); }
    }

    private void Display(Bitmap bitmap)
    {
        Bitmap? prior = _current;
        _current = bitmap;
        _capturedBounds = _driver.CaptureBounds;
        _captureRelativeToTarget = _driver.TargetWindowTitle is not null;
        _preview.Image = bitmap;
        _previewLabel.Text = $"Preview: {bitmap.Width} × {bitmap.Height} · {DateTime.Now:HH:mm:ss}";
        prior?.Dispose();
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
        if (_inspector is { IsDisposed: false }) _inspector.Close();
        if (_recorder is not null)
        {
            try { StopRecording(); }
            catch (Exception ex) { Log("Closing recorder: " + ex.Message); }
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _frameTimer.Dispose(); _current?.Dispose(); }
        base.Dispose(disposing);
    }
}
