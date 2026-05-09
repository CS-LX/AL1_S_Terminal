using AL1_S_Terminal.OverlayAnimations.Model;

namespace AL1_S_Terminal.OverlayAnimations.Runtime;

public sealed class OverlayRenderSnapshot
{
	public IReadOnlyList<OverlayRenderItem> Items { get; }

	public OverlayRenderSnapshot(IReadOnlyList<OverlayRenderItem> items) => Items = items;
}

public sealed class OverlayRenderItem
{
	public required string ImageKey { get; init; }

	public int X { get; init; }

	public int Y { get; init; }

	public double Opacity { get; init; }

	public double Scale { get; init; }
}

public sealed class OverlayAnimator
{
	private readonly OverlayAnimationConfig _config;

	public OverlayAnimator(OverlayAnimationConfig config) =>
		_config = config ?? throw new ArgumentNullException(nameof(config));

	public string? CurrentState { get; private set; }

	public void PlayDefault() => SetState(_config.DefaultState);

	public void SetState(string stateName)
	{
		if (!_config.States.ContainsKey(stateName))
			throw new ArgumentException($"Unknown state: {stateName}", nameof(stateName));

		CurrentState = stateName;
	}

	public OverlayRenderSnapshot Sample(int ms)
	{
		_ = ms;
		if (CurrentState is null)
			throw new InvalidOperationException("No active state; call PlayDefault or SetState first.");

		var state = _config.States[CurrentState];
		if (!_config.Clips.TryGetValue(state.Clip, out var clip))
			throw new InvalidOperationException($"Unknown clip: {state.Clip}");

		var items = new List<OverlayRenderItem>();
		foreach (var layer in clip.Layers.Values)
		{
			if (layer.Frames.Count == 0)
				continue;

			var kf = layer.Frames[0];
			items.Add(new OverlayRenderItem
			{
				ImageKey = layer.ImageKey,
				X = kf.X,
				Y = kf.Y,
				Opacity = kf.Opacity,
				Scale = kf.Scale
			});
		}

		return new OverlayRenderSnapshot(items);
	}
}
