using System.Windows;
using System.Windows.Threading;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    readonly DispatcherTimer _cursorPollTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public MainWindow() {
        InitializeComponent();
        _cursorPollTimer.Tick += (_, _) => RefreshCursorPosition();
        Loaded += (_, _) => _cursorPollTimer.Start();
        Closed += (_, _) => _cursorPollTimer.Stop();
    }

    void RefreshCursorPosition() {
        if (ScreenCursor.TryGetScreenPosition(out var p))
            CursorPositionText.Text = $"屏幕坐标: ({p.X}, {p.Y})";
        else
            CursorPositionText.Text = "GetCursorPos 失败";
    }
}