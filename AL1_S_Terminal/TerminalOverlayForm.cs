using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text.Json;
using AL1_S_Terminal.OverlayAnimations.Config;
using AL1_S_Terminal.OverlayAnimations.Runtime;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

/// <summary>
/// Top-level borderless WinForms window; positioned in screen space above Windows Terminal (no WinUI embedding).
/// </summary>
sealed class TerminalOverlayForm : Form {
    const int WsExNoactivate = unchecked((int)0x0800_0000);

    readonly OverlayAnimationHost? _host;
    readonly IOverlayAnimator _animator;

    /// <summary>Overlay animation controller; call <see cref="IOverlayAnimator.SetState"/> to switch states.</summary>
    public IOverlayAnimator Animator => _animator;

    public TerminalOverlayForm() {
        TopLevel = true;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;

        var s = new Size(TerminalOverlayInterop.OverlayWidth, TerminalOverlayInterop.OverlayHeight);
        ClientSize = s;
        MinimumSize = s;
        MaximumSize = s;

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

        var baseDir = AppContext.BaseDirectory;
        var cfgPath = Path.Combine(baseDir, "Assets", "overlay_animations", "default.json");

        try {
            var cfg = OverlayAnimationConfigLoader.LoadFromFile(cfgPath);
            _host = OverlayAnimationHost.CreateAndAttach(this, cfg, baseDir);
            _animator = _host;
        }
        catch (Exception ex) when (ex is IOException or JsonException) {
            Debug.WriteLine($"[TerminalOverlayForm] Failed to load default overlay animation config: {cfgPath}");
            Debug.WriteLine(ex);
            _animator = new NullOverlayAnimator();
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing)
            _host?.Dispose();
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams {
        get {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoactivate;
            return cp;
        }
    }

    sealed class NullOverlayAnimator : IOverlayAnimator {
        public string? CurrentState => null;
        public void PlayDefault() { }
        public void SetState(string stateName, bool restart = true) { }
        public bool TrySetState(string stateName, bool restart = true) => false;
        public void Stop() { }
        public void ReloadConfig() { }
    }
}
