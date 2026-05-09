using AL1_S_Terminal.OverlayAnimations.Model;
using AL1_S_Terminal.OverlayAnimations.Runtime;
using Xunit;

namespace AL1_S_Terminal.Tests.OverlayAnimations;

internal static class TestConfigs
{
	public static OverlayAnimationConfig OneLayerTwoFrames()
	{
		return new OverlayAnimationConfig
		{
			Version = 1,
			DefaultState = "S",
			Images = new Dictionary<string, string> { ["img"] = "x.png" },
			States = new Dictionary<string, OverlayAnimationStateConfig>
			{
				["S"] = new OverlayAnimationStateConfig { Clip = "c", Loop = false }
			},
			Clips = new Dictionary<string, OverlayAnimationClipConfig>
			{
				["c"] = new OverlayAnimationClipConfig
				{
					DurationMs = 1000,
					Layers = new Dictionary<string, OverlayAnimationLayerConfig>
					{
						["L"] = new OverlayAnimationLayerConfig
						{
							ImageKey = "img",
							Frames = new List<OverlayAnimationKeyframe>
							{
								new() { T = 0, X = 0, Y = 0, Opacity = 0, Scale = 1 },
								new() { T = 1000, X = 10, Y = 0, Opacity = 1, Scale = 1 }
							}
						}
					}
				}
			}
		};
	}

	public static OverlayAnimationConfig OneLayerTwoFramesLooping()
	{
		var cfg = OneLayerTwoFrames();
		cfg.States["S"].Loop = true;
		return cfg;
	}
}

public sealed class OverlayAnimatorTests
{
	[Fact]
	public void LoadsDefaultStateAndSamples()
	{
		var cfg = new OverlayAnimationConfig
		{
			Version = 1,
			DefaultState = "Idle",
			Images = new Dictionary<string, string> { ["logo"] = "logo.png" },
			States = new Dictionary<string, OverlayAnimationStateConfig>
			{
				["Idle"] = new OverlayAnimationStateConfig { Clip = "idle", Loop = true }
			},
			Clips = new Dictionary<string, OverlayAnimationClipConfig>
			{
				["idle"] = new OverlayAnimationClipConfig
				{
					DurationMs = 1000,
					Layers = new Dictionary<string, OverlayAnimationLayerConfig>
					{
						["logoLayer"] = new OverlayAnimationLayerConfig
						{
							ImageKey = "logo",
							Frames = new List<OverlayAnimationKeyframe>
							{
								new() { T = 0, X = 10, Y = 20, Opacity = 1, Scale = 1 }
							}
						}
					}
				}
			}
		};

		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();

		var snapshot = animator.Sample(ms: 0);
		Assert.Equal("Idle", animator.CurrentState);
		Assert.Single(snapshot.Items);
		Assert.Equal("logo", snapshot.Items[0].ImageKey);
		Assert.Equal(10, snapshot.Items[0].X);
		Assert.Equal(20, snapshot.Items[0].Y);
	}

	[Fact]
	public void InterpolatesBetweenKeyframes()
	{
		var cfg = TestConfigs.OneLayerTwoFrames();
		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();

		var snapshot = animator.Sample(500);
		Assert.Single(snapshot.Items);
		Assert.Equal(5, snapshot.Items[0].X);
		Assert.Equal(0.5, snapshot.Items[0].Opacity, precision: 5);
	}

	[Fact]
	public void LoopWrapsTimeSoSampleAtOneAndHalfDurationMatchesMidpoint()
	{
		var cfg = TestConfigs.OneLayerTwoFramesLooping();
		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();

		var a = animator.Sample(500);
		var b = animator.Sample(1500);
		Assert.Equal(a.Items[0].X, b.Items[0].X);
		Assert.Equal(a.Items[0].Opacity, b.Items[0].Opacity, precision: 10);
	}
}
