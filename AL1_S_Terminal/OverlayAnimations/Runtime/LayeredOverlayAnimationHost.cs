using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Model;
using AL1_S_Terminal.OverlayAnimations.Rendering;

namespace AL1_S_Terminal.OverlayAnimations.Runtime;

/// <summary>
/// Drives overlay animation with per-pixel alpha via <see cref="LayeredWindowInterop"/> (no child <see cref="Control"/>).
/// The owning <see cref="Form"/> must use <c>WS_EX_LAYERED</c> before its handle is created.
/// </summary>
public sealed class LayeredOverlayAnimationHost : IOverlayAnimator, IDisposable {
	readonly Form _form;
	readonly OverlayAnimator _animator;
	readonly OverlayImageAtlas _atlas;
	readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
	readonly Stopwatch _stopwatch = new();
	Bitmap? _buffer;
	bool _disposed;

	LayeredOverlayAnimationHost(Form form, OverlayAnimationConfig config, OverlayImageAtlas atlas) {
		_form = form;
		_animator = new OverlayAnimator(config);
		_atlas = atlas;
		_timer.Tick += OnTimerTick;
	}

	public static LayeredOverlayAnimationHost Attach(Form form, OverlayAnimationConfig cfg, string baseDir) {
		ArgumentNullException.ThrowIfNull(form);
		ArgumentNullException.ThrowIfNull(cfg);
		ArgumentNullException.ThrowIfNull(baseDir);
		var atlas = new OverlayImageAtlas(cfg.Images, baseDir);
		return new LayeredOverlayAnimationHost(form, cfg, atlas);
	}

	public string? CurrentState => _animator.CurrentState;

	public void PlayDefault() {
		ObjectDisposedException.ThrowIf(_disposed, this);
		_animator.PlayDefault();
		_stopwatch.Restart();
		_timer.Start();
		PushFrame();
	}

	public void SetState(string stateName, bool restart = true) {
		ObjectDisposedException.ThrowIf(_disposed, this);
		_animator.SetState(stateName, restart);
		_stopwatch.Restart();
		_timer.Start();
		PushFrame();
	}

	public bool TrySetState(string stateName, bool restart = true) {
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (!_animator.TrySetState(stateName, restart))
			return false;
		_stopwatch.Restart();
		_timer.Start();
		PushFrame();
		return true;
	}

	public void Stop() {
		ObjectDisposedException.ThrowIf(_disposed, this);
		_timer.Stop();
	}

	public void ReloadConfig() =>
		throw new NotSupportedException("LayeredOverlayAnimationHost.ReloadConfig is not implemented yet.");

	public void Start() {
		ObjectDisposedException.ThrowIf(_disposed, this);
		_stopwatch.Restart();
		_timer.Start();
		PushFrame();
	}

	public void Dispose() {
		if (_disposed)
			return;
		_disposed = true;
		_timer.Stop();
		_timer.Tick -= OnTimerTick;
		_timer.Dispose();
		_buffer?.Dispose();
		_buffer = null;
		_atlas.Dispose();
	}

	void EnsureBuffer(Size client) {
		if (client.Width < 1 || client.Height < 1)
			return;
		if (_buffer is not null && _buffer.Size == client)
			return;
		_buffer?.Dispose();
		_buffer = new Bitmap(client.Width, client.Height, PixelFormat.Format32bppPArgb);
	}

	void OnTimerTick(object? sender, EventArgs e) {
		if (_disposed || !_form.IsHandleCreated)
			return;
		var client = _form.ClientSize;
		EnsureBuffer(client);
		if (_buffer is null)
			return;

		var elapsedMs = (int)_stopwatch.ElapsedMilliseconds;
		var snapshot = _animator.Sample(elapsedMs);

		using (var g = Graphics.FromImage(_buffer)) {
			g.CompositingMode = CompositingMode.SourceOver;
			OverlayAnimationPainter.Draw(g, _atlas, snapshot);
		}

		if (!LayeredWindowInterop.UpdateFromPremultipliedBitmap(_form.Handle, _buffer))
			Debug.WriteLine($"[LayeredOverlayAnimationHost] UpdateLayeredWindow failed, Win32 error {Marshal.GetLastWin32Error()}");
	}

	void PushFrame() {
		if (_disposed || !_form.IsHandleCreated)
			return;
		OnTimerTick(null, EventArgs.Empty);
	}
}
