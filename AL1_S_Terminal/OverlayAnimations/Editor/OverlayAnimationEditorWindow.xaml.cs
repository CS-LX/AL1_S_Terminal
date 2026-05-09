using System.Windows;
using System.Windows.Forms;

namespace AL1_S_Terminal.OverlayAnimations.Editor;

public partial class OverlayAnimationEditorWindow : Window {
    public OverlayAnimationEditorWindow() {
        InitializeComponent();
        PreviewHost.Child = new Panel { BackColor = System.Drawing.Color.DimGray };
    }

    protected override void OnClosed(EventArgs e) {
        PreviewHost.Child = null;
        base.OnClosed(e);
    }

    void OpenButton_Click(object sender, RoutedEventArgs e) {
        // Task 3: Open file dialog + load config
    }

    void SaveButton_Click(object sender, RoutedEventArgs e) {
        // Task 3: Save current file
    }

    void SaveAsButton_Click(object sender, RoutedEventArgs e) {
        // Task 3: Save as
    }

    void PlayButton_Click(object sender, RoutedEventArgs e) {
        // Task 4: Preview play
    }

    void PauseButton_Click(object sender, RoutedEventArgs e) {
        // Task 4: Preview pause
    }

    void RestartButton_Click(object sender, RoutedEventArgs e) {
        // Task 4: Preview restart
    }
}
