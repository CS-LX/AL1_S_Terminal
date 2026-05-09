using System.Drawing;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Config;
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

	public static OverlayAnimationConfig IdleLogoClipForJsonRoundTrip()
	{
		return new OverlayAnimationConfig
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
								new() { T = 0, X = 0, Y = 0, Opacity = 0, Scale = 1 },
								new() { T = 1000, X = 10, Y = 0, Opacity = 1, Scale = 1 }
							}
						}
					}
				}
			}
		};
	}

	public static OverlayAnimationConfig TwoStatesDifferentX()
	{
		return new OverlayAnimationConfig
		{
			Version = 1,
			DefaultState = "Idle",
			Images = new Dictionary<string, string> { ["img"] = "x.png" },
			States = new Dictionary<string, OverlayAnimationStateConfig>
			{
				["Idle"] = new OverlayAnimationStateConfig { Clip = "idle", Loop = false },
				["Pulse"] = new OverlayAnimationStateConfig { Clip = "pulse", Loop = false }
			},
			Clips = new Dictionary<string, OverlayAnimationClipConfig>
			{
				["idle"] = new OverlayAnimationClipConfig
				{
					DurationMs = 1000,
					Layers = new Dictionary<string, OverlayAnimationLayerConfig>
					{
						["L"] = new OverlayAnimationLayerConfig
						{
							ImageKey = "img",
							Frames = new List<OverlayAnimationKeyframe>
							{
								new() { T = 0, X = 0, Y = 0, Opacity = 1, Scale = 1 }
							}
						}
					}
				},
				["pulse"] = new OverlayAnimationClipConfig
				{
					DurationMs = 1000,
					Layers = new Dictionary<string, OverlayAnimationLayerConfig>
					{
						["L"] = new OverlayAnimationLayerConfig
						{
							ImageKey = "img",
							Frames = new List<OverlayAnimationKeyframe>
							{
								new() { T = 0, X = 100, Y = 0, Opacity = 1, Scale = 1 }
							}
						}
					}
				}
			}
		};
	}

	public static OverlayAnimationConfig LoopingClipDuration(int durationMs)
	{
		return new OverlayAnimationConfig
		{
			Version = 1,
			DefaultState = "S",
			Images = new Dictionary<string, string> { ["img"] = "x.png" },
			States = new Dictionary<string, OverlayAnimationStateConfig>
			{
				["S"] = new OverlayAnimationStateConfig { Clip = "c", Loop = true }
			},
			Clips = new Dictionary<string, OverlayAnimationClipConfig>
			{
				["c"] = new OverlayAnimationClipConfig
				{
					DurationMs = durationMs,
					Layers = new Dictionary<string, OverlayAnimationLayerConfig>
					{
						["L"] = new OverlayAnimationLayerConfig
						{
							ImageKey = "img",
							Frames = new List<OverlayAnimationKeyframe>
							{
								new() { T = 0, X = 0, Y = 0, Opacity = 0, Scale = 1 }
							}
						}
					}
				}
			}
		};
	}
}

public sealed class OverlayAnimatorTests
{
	[Fact]
	public void TwoStatesDifferentX()
	{
		var cfg = TestConfigs.TwoStatesDifferentX();
		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();
		var idleX = animator.Sample(0).Items[0].X;

		animator.SetState("Pulse");
		var pulseX = animator.Sample(0).Items[0].X;

		Assert.NotEqual(idleX, pulseX);
	}

	[Fact]
	public void CanSwitchStateExternally()
	{
		var cfg = TestConfigs.TwoStatesDifferentX();
		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();
		Assert.Equal(0, animator.Sample(0).Items[0].X);

		animator.SetState("Pulse");
		Assert.Equal(100, animator.Sample(0).Items[0].X);
	}

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
	public void JsonRoundTripPreservesCoreFields()
	{
		var cfg = TestConfigs.IdleLogoClipForJsonRoundTrip();
		var json = OverlayAnimationConfigLoader.ToJson(cfg);
		var back = OverlayAnimationConfigLoader.FromJson(json);

		Assert.Equal("Idle", back.DefaultState);
		Assert.True(back.Images.TryGetValue("logo", out var logoPath));
		Assert.False(string.IsNullOrEmpty(logoPath));
		Assert.True(back.States.TryGetValue("Idle", out var idleState));
		Assert.True(idleState.Loop);
		Assert.True(back.Clips.TryGetValue("idle", out var idleClip));
		Assert.Equal(1000, idleClip.DurationMs);
		Assert.NotNull(idleClip.Layers);
		Assert.True(idleClip.Layers.Count >= 1);
		Assert.Contains(idleClip.Layers, p => p.Value.ImageKey == "logo");

		var animator = new OverlayAnimator(back);
		animator.PlayDefault();
		var s = animator.Sample(ms: 500);
		Assert.Equal(5, s.Items[0].X);
	}

	[Fact]
	public void LoadBundledDefaultAlice_FromTestOutput_HasExpectedStructure()
	{
		var path = Path.Combine(AppContext.BaseDirectory, "Assets", "overlay_animations", "Default.alice");
		Assert.True(File.Exists(path), $"Expected default overlay package at: {path}");
		using var session = OverlayAlicePackage.LoadExtracted(path);
		var cfg = session.Config;
		Assert.Equal("Idle", cfg.DefaultState);
		foreach (var key in new[] { "spark", "ring", "logo" })
			Assert.True(cfg.Images.ContainsKey(key), $"Missing image key: {key}");
		foreach (var state in new[] { "Idle", "Pulse" })
			Assert.True(cfg.States.ContainsKey(state), $"Missing state: {state}");
		foreach (var clip in new[] { "idle", "pulse" })
			Assert.True(cfg.Clips.ContainsKey(clip), $"Missing clip: {clip}");
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

	[Fact]
	public void DuplicateT_SelectsEarliestStableKeyframeAtBoundary()
	{
		var cfg = TestConfigs.OneLayerTwoFrames();
		var layerFrames = cfg.Clips["c"].Layers["L"].Frames;
		layerFrames.Clear();
		layerFrames.Add(new OverlayAnimationKeyframe { T = 0, X = 7, Y = 0, Opacity = 0, Scale = 1 });
		layerFrames.Add(new OverlayAnimationKeyframe { T = 0, X = 99, Y = 0, Opacity = 1, Scale = 1 });
		layerFrames.Add(new OverlayAnimationKeyframe { T = 1000, X = 10, Y = 0, Opacity = 1, Scale = 1 });

		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();
		Assert.Equal(7, animator.Sample(0).Items[0].X);
	}

	[Fact]
	public void LoopWithNonPositiveDurationThrowsDescribingDurationMs()
	{
		foreach (var d in new[] { 0, -1 })
		{
			var animator = new OverlayAnimator(TestConfigs.LoopingClipDuration(d));
			animator.PlayDefault();
			var ex = Assert.Throws<InvalidOperationException>(() => animator.Sample(0));
			Assert.Contains("DurationMs", ex.Message);
			Assert.Contains(d.ToString(), ex.Message);
		}
	}

	[Fact]
	public void SampleWithoutActiveStateThrows()
	{
		var animator = new OverlayAnimator(TestConfigs.OneLayerTwoFrames());
		Assert.Throws<InvalidOperationException>(() => animator.Sample(0));
	}

	[Fact]
	public void NonLoop_NegativeMs_ClampsToZero()
	{
		var animator = new OverlayAnimator(TestConfigs.OneLayerTwoFrames());
		animator.PlayDefault();
		var zero = animator.Sample(0);
		var neg = animator.Sample(-999);
		Assert.Equal(zero.Items[0].X, neg.Items[0].X);
		Assert.Equal(zero.Items[0].Opacity, neg.Items[0].Opacity, precision: 10);
	}

	[Fact]
	public void Loop_NegativeMs_WrapsLikePositiveMod()
	{
		var animator = new OverlayAnimator(TestConfigs.OneLayerTwoFramesLooping());
		animator.PlayDefault();
		var wrapped = animator.Sample(-500);
		var upright = animator.Sample(500);
		Assert.Equal(upright.Items[0].X, wrapped.Items[0].X);
		Assert.Equal(upright.Items[0].Opacity, wrapped.Items[0].Opacity, precision: 10);
	}

	[Fact]
	public void UnsortedKeyframesYieldSameInterpolationAsTimeOrder()
	{
		var cfg = TestConfigs.OneLayerTwoFrames();
		var frames = cfg.Clips["c"].Layers["L"].Frames;
		frames.Clear();
		frames.Add(new OverlayAnimationKeyframe { T = 1000, X = 100, Y = 0, Opacity = 1, Scale = 1 });
		frames.Add(new OverlayAnimationKeyframe { T = 0, X = 0, Y = 0, Opacity = 0, Scale = 1 });
		frames.Add(new OverlayAnimationKeyframe { T = 500, X = 50, Y = 0, Opacity = 0.5, Scale = 1 });

		var animator = new OverlayAnimator(cfg);
		animator.PlayDefault();
		var snapshot = animator.Sample(250);

		Assert.Single(snapshot.Items);
		Assert.Equal(25, snapshot.Items[0].X);
		Assert.Equal(0.25, snapshot.Items[0].Opacity, precision: 10);
	}
}

public sealed class OverlayImageAtlasTests
{
	[Theory]
	[InlineData(@"C:\x.png")]
	[InlineData(@"..\x.png")]
	public void AtlasRejectsAbsoluteOrTraversalPaths(string badRelativePath)
	{
		var baseDir = Path.Combine(Path.GetTempPath(), "atlas_path_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(baseDir);

		var loadCount = 0;
		var images = new Dictionary<string, string> { ["k"] = badRelativePath };
		using var atlas = new OverlayImageAtlas(images, baseDir, _ =>
		{
			loadCount++;
			return new Bitmap(1, 1);
		});

		Assert.Throws<InvalidOperationException>(() => atlas.TryGet("k", out _));
		Assert.Equal(0, loadCount);
	}

	[Fact]
	public void AtlasDoesNotCacheOnLoaderException()
	{
		var images = new Dictionary<string, string> { ["k"] = "a.png" };
		var loadCount = 0;
		using var atlas = new OverlayImageAtlas(images, @"C:\base", _ =>
		{
			loadCount++;
			if (loadCount == 1)
				throw new IOException("simulated load failure");
			return new Bitmap(1, 1);
		});

		Assert.Throws<IOException>(() => atlas.TryGet("k", out _));
		Assert.True(atlas.TryGet("k", out var img));
		Assert.NotNull(img);
		Assert.Equal(2, loadCount);
	}

	[Fact]
	public void TryGetAfterDisposeThrows()
	{
		var images = new Dictionary<string, string> { ["k"] = "f.png" };
		var atlas = new OverlayImageAtlas(images, @"C:\base", _ => new Bitmap(1, 1));
		atlas.TryGet("k", out _);
		atlas.Dispose();

		Assert.Throws<ObjectDisposedException>(() => atlas.TryGet("k", out _));
	}

	[Fact]
	public void AtlasCachesImagesByKey()
	{
		var images = new Dictionary<string, string> { ["a"] = "x.png" };
		using var atlas = new OverlayImageAtlas(images, @"C:\no\such\base", _ => new Bitmap(1, 1));

		Assert.False(atlas.TryGet("missing", out _));
		Assert.True(atlas.TryGet("a", out var image));
		Assert.NotNull(image);
	}

	[Fact]
	public void AtlasLoadsOncePerKey()
	{
		var images = new Dictionary<string, string> { ["k"] = "f.png" };
		var loadCount = 0;
		using var atlas = new OverlayImageAtlas(images, @"C:\base", _ =>
		{
			loadCount++;
			return new Bitmap(1, 1);
		});

		Assert.True(atlas.TryGet("k", out var img1));
		Assert.True(atlas.TryGet("k", out var img2));
		Assert.Same(img1, img2);
		Assert.Equal(1, loadCount);
	}
}
