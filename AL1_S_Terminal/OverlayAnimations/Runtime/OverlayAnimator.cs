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
		if (CurrentState is null)
			throw new InvalidOperationException("No active state; call PlayDefault or SetState first.");

		var state = _config.States[CurrentState];
		if (!_config.Clips.TryGetValue(state.Clip, out var clip))
			throw new InvalidOperationException($"Unknown clip: {state.Clip}");

		int timeMs = ResolveSampleTimeMs(ms, state.Loop, clip.DurationMs);

		var items = new List<OverlayRenderItem>();
		foreach (var layer in clip.Layers.Values)
		{
			if (layer.Frames.Count == 0)
				continue;

			items.Add(SampleLayer(layer.ImageKey, layer.Frames, timeMs));
		}

		return new OverlayRenderSnapshot(items);
	}

	private static int ResolveSampleTimeMs(int ms, bool loop, int durationMs)
	{
		if (loop)
		{
			if (durationMs <= 0)
				throw new InvalidOperationException($"Loop requires clip DurationMs > 0, but DurationMs is {durationMs}.");

			return PositiveMod(ms, durationMs);
		}

		return ms < 0 ? 0 : ms;
	}

	private static int PositiveMod(int value, int modulus)
	{
		int r = value % modulus;
		return r < 0 ? r + modulus : r;
	}

	private static bool IsNonDecreasingByT(List<OverlayAnimationKeyframe> frames)
	{
		for (int i = 1; i < frames.Count; i++)
		{
			if (frames[i - 1].T > frames[i].T)
				return false;
		}

		return true;
	}

	private static List<OverlayAnimationKeyframe> StableSortFramesByT(List<OverlayAnimationKeyframe> frames)
	{
		var keys = new (OverlayAnimationKeyframe K, int I)[frames.Count];
		for (int i = 0; i < frames.Count; i++)
			keys[i] = (frames[i], i);

		Array.Sort(keys, static (a, b) =>
		{
			int c = a.K.T.CompareTo(b.K.T);
			return c != 0 ? c : a.I.CompareTo(b.I);
		});

		var sorted = new List<OverlayAnimationKeyframe>(frames.Count);
		for (int i = 0; i < keys.Length; i++)
			sorted.Add(keys[i].K);

		return sorted;
	}

	private static OverlayRenderItem SampleLayer(string imageKey, List<OverlayAnimationKeyframe> frames, int timeMs)
	{
		IReadOnlyList<OverlayAnimationKeyframe> ordered = IsNonDecreasingByT(frames)
			? frames
			: StableSortFramesByT(frames);

		if (ordered.Count == 1)
			return KeyframeToItem(imageKey, ordered[0]);

		var first = ordered[0];
		var last = ordered[^1];
		if (timeMs <= first.T)
			return KeyframeToItem(imageKey, first);
		if (timeMs >= last.T)
			return KeyframeToItem(imageKey, last);

		int i = 0;
		while (i < ordered.Count - 1 && !(ordered[i].T < timeMs && timeMs <= ordered[i + 1].T))
			i++;

		if (i >= ordered.Count - 1)
			return KeyframeToItem(imageKey, last);

		var a = ordered[i];
		var b = ordered[i + 1];
		int span = b.T - a.T;
		if (span <= 0)
			return KeyframeToItem(imageKey, b);

		double u = (timeMs - a.T) / (double)span;
		return new OverlayRenderItem
		{
			ImageKey = imageKey,
			X = (int)Math.Round(a.X + (b.X - a.X) * u),
			Y = (int)Math.Round(a.Y + (b.Y - a.Y) * u),
			Opacity = a.Opacity + (b.Opacity - a.Opacity) * u,
			Scale = a.Scale + (b.Scale - a.Scale) * u
		};
	}

	private static OverlayRenderItem KeyframeToItem(string imageKey, OverlayAnimationKeyframe k) =>
		new()
		{
			ImageKey = imageKey,
			X = k.X,
			Y = k.Y,
			Opacity = k.Opacity,
			Scale = k.Scale
		};
}
