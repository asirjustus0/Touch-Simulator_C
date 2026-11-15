using System.Drawing;

namespace TouchSimulator;

/// <summary>Non-modal coordinate tool window; input remains command-driven.</summary>
public sealed class CoordinateInspectorForm : Form
{
    private readonly NumericUpDown _x = new();
    private readonly NumericUpDown _y = new();
    private readonly Label _screen = new();
    private readonly Label _state = new();
    private bool _available;

    public event Action<Point>? TouchRequested;
    public event Action<Point>? ClickRequested;

    public CoordinateInspectorForm()
    {
        Text = "Coordinate Inspector";
        Width = 375;
        Height = 335;
        MinimumSize = new Size(335, 315);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(248, 249, 252);
        Font = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(layout);
        _state.Text = "Move over the screenshot";
        _state.TextAlign = ContentAlignment.MiddleLeft;
        _state.Dock = DockStyle.Fill;
        layout.Controls.Add(_state, 0, 0);
        layout.SetColumnSpan(_state, 2);

        AddCoordinateEditor("X coordinate", _x, 0);
        AddCoordinateEditor("Y coordinate", _y, 1);

        _screen.Text = "Absolute screen: —";
        _screen.TextAlign = ContentAlignment.MiddleLeft;
        _screen.Dock = DockStyle.Fill;
        layout.Controls.Add(_screen, 0, 2);
        layout.SetColumnSpan(_screen, 2);
        var touch = MakeButton("Simulate touch", Color.FromArgb(38, 104, 186));
        var click = MakeButton("Mouse click", Color.FromArgb(57, 67, 82));
        touch.Click += (_, _) => { if (_available) TouchRequested?.Invoke(ReadPoint()); };
        click.Click += (_, _) => { if (_available) ClickRequested?.Invoke(ReadPoint()); };
        layout.Controls.Add(touch, 0, 3);
        layout.Controls.Add(click, 1, 3);
        _x.ValueChanged += (_, _) => _available = true;
        _y.ValueChanged += (_, _) => _available = true;
    }

    private void AddCoordinateEditor(string title, NumericUpDown editor, int column)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0, 0, 8, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Color.DimGray }, 0, 0);
        editor.Minimum = -100000;
        editor.Maximum = 100000;
        editor.Dock = DockStyle.Fill;
        editor.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        panel.Controls.Add(editor, 0, 1);
        layoutAdd(panel, column);
    }

    private TableLayoutPanel? _layout;
    private void layoutAdd(Control child, int column)
    {
        _layout ??= (TableLayoutPanel)Controls[0];
        _layout.Controls.Add(child, column, 1);
    }

    private static Button MakeButton(string title, Color back) => new()
    {
        Text = title, Dock = DockStyle.Fill, Height = 43, Margin = new Padding(3),
        BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold)
    };

    private Point ReadPoint() => new((int)_x.Value, (int)_y.Value);

    public void UpdatePoint(Point commandPoint, Point absolutePoint)
    {
        _x.Value = Math.Clamp(commandPoint.X, (int)_x.Minimum, (int)_x.Maximum);
        _y.Value = Math.Clamp(commandPoint.Y, (int)_y.Minimum, (int)_y.Maximum);
        _screen.Text = $"Absolute screen: ({absolutePoint.X}, {absolutePoint.Y})";
        _state.Text = "Point selected — edit X/Y or execute";
        _available = true;
    }

    public void ClearPoint()
    {
        _available = false;
        _state.Text = "Move over the screenshot";
        _screen.Text = "Absolute screen: —";
    }
}
