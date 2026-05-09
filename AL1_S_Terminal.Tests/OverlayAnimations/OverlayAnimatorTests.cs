using AL1_S_Terminal.OverlayAnimations.Model;
using AL1_S_Terminal.OverlayAnimations.Runtime;
using Xunit;

namespace AL1_S_Terminal.Tests.OverlayAnimations;

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
}
