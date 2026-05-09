namespace AL1_S_Terminal.OverlayAnimations.Model;

public sealed class OverlayAnimationConfig
{
	public int Version { get; set; }

	/// <summary>Overlay client width in pixels (default 200 when missing or invalid in JSON).</summary>
	public int Width { get; set; } = 200;

	/// <summary>Overlay client height in pixels (default 200 when missing or invalid in JSON).</summary>
	public int Height { get; set; } = 200;

	public string DefaultState { get; set; } = string.Empty;

	public Dictionary<string, string> Images { get; set; } = new();

	public Dictionary<string, OverlayAnimationStateConfig> States { get; set; } = new();

	public Dictionary<string, OverlayAnimationClipConfig> Clips { get; set; } = new();
}

public sealed class OverlayAnimationStateConfig
{
	public string Clip { get; set; } = string.Empty;

	public bool Loop { get; set; }
}

public sealed class OverlayAnimationClipConfig
{
	public int DurationMs { get; set; }

	public Dictionary<string, OverlayAnimationLayerConfig> Layers { get; set; } = new();
}

public sealed class OverlayAnimationLayerConfig
{
	public string ImageKey { get; set; } = string.Empty;

	public List<OverlayAnimationKeyframe> Frames { get; set; } = new();
}

public sealed class OverlayAnimationKeyframe
{
	public int T { get; set; }

	public int X { get; set; }

	public int Y { get; set; }

	public double Opacity { get; set; }

	public double Scale { get; set; }
}
