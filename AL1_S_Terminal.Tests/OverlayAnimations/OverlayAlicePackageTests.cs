using AL1_S_Terminal.OverlayAnimations.Config;
using Xunit;

namespace AL1_S_Terminal.Tests.OverlayAnimations;

public sealed class OverlayAlicePackageTests
{
	[Fact]
	public void PackAndLoad_RoundTrip_PreservesImagePaths()
	{
		var work = Path.Combine(Path.GetTempPath(), "alice_rt_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(work, "assets"));
		try {
			var cfg = TestConfigs.IdleLogoClipForJsonRoundTrip();
			cfg.Images["logo"] = "assets/logo.png";
			File.WriteAllBytes(Path.Combine(work, "assets", "logo.png"), MinimalPng);
			OverlayAlicePackage.WriteKeyToDirectory(work, cfg);

			var alice = Path.Combine(Path.GetTempPath(), "alice_out_" + Guid.NewGuid().ToString("N") + ".alice");
			try {
				OverlayAlicePackage.PackDirectoryToAlice(work, alice);
				using var session = OverlayAlicePackage.LoadExtracted(alice);
				Assert.Equal("Idle", session.Config.DefaultState);
				Assert.True(session.Config.Images.TryGetValue("logo", out var p));
				Assert.Equal("assets/logo.png", p);
			}
			finally {
				if (File.Exists(alice))
					File.Delete(alice);
			}
		}
		finally {
			if (Directory.Exists(work))
				Directory.Delete(work, recursive: true);
		}
	}

	/// <summary>1×1 PNG, no external files.</summary>
	static readonly byte[] MinimalPng =
	[
		0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
		0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
		0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
		0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
		0x44, 0xAE, 0x42, 0x60, 0x82
	];
}
