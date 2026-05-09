namespace AL1_S_Terminal.OverlayAnimations.Runtime;

public interface IOverlayAnimator
{
	string? CurrentState { get; }
	void PlayDefault();
	void SetState(string stateName, bool restart = true);
	bool TrySetState(string stateName, bool restart = true);
	void Stop();

	/// <summary>
	/// Reloads overlay animation configuration (e.g. from disk).
	/// </summary>
	/// <remarks>
	/// Not implemented in <see cref="OverlayAnimationHost"/> yet: that implementation throws <see cref="NotSupportedException"/>. A future version may load config without recreating the host.
	/// </remarks>
	void ReloadConfig();
}
