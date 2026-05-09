using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

/// <summary>
/// Top-level borderless WinForms window; positioned in screen space above Windows Terminal (no WinUI embedding).
/// </summary>
sealed class TerminalOverlayForm : Form {
    const int WsExNoactivate = unchecked((int)0x0800_0000);

    public TerminalOverlayForm() {
        TopLevel = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;

        ClientSize = new Size(TerminalOverlayInterop.OverlayWidth, TerminalOverlayInterop.OverlayHeight);
        MinimumSize = ClientSize;
        MaximumSize = ClientSize;

        using var packStream = global::System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/window_bg.png"))
            ?.Stream;

        if (packStream is not null) {
            using var ms = new MemoryStream();
            packStream.CopyTo(ms);
            ms.Position = 0;
            BackgroundImage = new Bitmap(ms);
        }

        BackgroundImageLayout = ImageLayout.Stretch;
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams {
        get {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoactivate;
            return cp;
        }
    }
}
