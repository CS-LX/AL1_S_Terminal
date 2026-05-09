using System.Diagnostics;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Model;
using AL1_S_Terminal.OverlayAnimations.Rendering;

namespace AL1_S_Terminal.OverlayAnimations.Runtime;

public sealed class OverlayAnimationHost : IOverlayAnimator, IDisposable
{
	private readonly OverlayAnimator _animator;
	private readonly OverlayImageAtlas _atlas;
	private readonly OverlayAnimationControl _control;
	private readonly System.Windows.Forms.Timer _timer;
	private readonly Stopwatch _stopwatch;
	private readonly bool _ownsAtlas;
	private bool _disposed;

	private OverlayAnimationHost(
		OverlayAnimationConfig config,
		OverlayImageAtlas atlas,
		OverlayAnimationControl control,
		bool ownsAtlas)
	{
		_animator = new OverlayAnimator(config);
		_atlas = atlas;
		_control = control;
		_ownsAtlas = ownsAtlas;
		_stopwatch = new Stopwatch();
		_timer = new System.Windows.Forms.Timer { Interval = 16 };
		_timer.Tick += OnTimerTick;
	}

	public string? CurrentState => _animator.CurrentState;

	/// <summary>
	/// Creates atlas and control, docks the control on <paramref name="form"/>, constructs the host, starts default playback, and runs the update timer.
	/// </summary>
	public static OverlayAnimationHost CreateAndAttach(Form form, OverlayAnimationConfig cfg, string baseDir)
	{
		ArgumentNullException.ThrowIfNull(form);
		ArgumentNullException.ThrowIfNull(cfg);
		ArgumentNullException.ThrowIfNull(baseDir);

		var atlas = new OverlayImageAtlas(cfg.Images, baseDir);
		var control = new OverlayAnimationControl(atlas) { Dock = DockStyle.Fill };
		form.Controls.Add(control);

		var host = new OverlayAnimationHost(cfg, atlas, control, ownsAtlas: true);
		host.PlayDefault();
		return host;
	}

	public void PlayDefault()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_animator.PlayDefault();
		_stopwatch.Restart();
		_timer.Start();
		PushCurrentFrame();
	}

	public void SetState(string stateName, bool restart = true)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_animator.SetState(stateName, restart);
		_stopwatch.Restart();
		_timer.Start();
		PushCurrentFrame();
	}

	public bool TrySetState(string stateName, bool restart = true)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (!_animator.TrySetState(stateName, restart))
			return false;

		_stopwatch.Restart();
		_timer.Start();
		PushCurrentFrame();
		return true;
	}

	public void Stop()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_timer.Stop();
	}

	/// <inheritdoc cref="IOverlayAnimator.ReloadConfig"/>
	/// <exception cref="NotSupportedException">Live config reload is not implemented yet.</exception>
	public void ReloadConfig() =>
		throw new NotSupportedException("OverlayAnimationHost.ReloadConfig is not implemented yet.");

	public void Start()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		_stopwatch.Restart();
		_timer.Start();
		PushCurrentFrame();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_timer.Stop();
		_timer.Tick -= OnTimerTick;
		_timer.Dispose();
		if (_ownsAtlas)
			_atlas.Dispose();
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		if (_disposed)
			return;

		var elapsedMs = _stopwatch.ElapsedMilliseconds;
		var snapshot = _animator.Sample((int)elapsedMs);
		_control.SetSnapshot(snapshot);
	}

	private void PushCurrentFrame()
	{
		var elapsedMs = _stopwatch.ElapsedMilliseconds;
		_control.SetSnapshot(_animator.Sample((int)elapsedMs));
	}
}
