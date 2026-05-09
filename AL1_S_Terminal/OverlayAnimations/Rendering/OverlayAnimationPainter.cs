using System.Drawing;
using System.Drawing.Drawing2D;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Runtime;

namespace AL1_S_Terminal.OverlayAnimations.Rendering;

/// <summary>
/// Shared drawing path for overlay frames (WinForms control and layered-window updates).
/// </summary>
public static class OverlayAnimationPainter {
	public static void Draw(Graphics g, OverlayImageAtlas atlas, OverlayRenderSnapshot? snapshot) {
		g.Clear(Color.Transparent);

		if (snapshot is null)
			return;

		g.PixelOffsetMode = PixelOffsetMode.Half;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.SmoothingMode = SmoothingMode.HighQuality;

		foreach (var item in snapshot.Items) {
			if (!atlas.TryGet(item.ImageKey, out var img))
				continue;

			var opacity = (float)Math.Clamp(item.Opacity, 0.0, 1.0);
			var scale = (float)item.Scale;
			if (opacity <= 0f || scale <= 0f)
				continue;

			var w = img.Width * scale;
			var h = img.Height * scale;
			var dest = new RectangleF(item.X, item.Y, w, h);

			using var attrs = new ImageAttributes();
			var matrix = new ColorMatrix { Matrix33 = opacity };
			attrs.SetColorMatrix(matrix);

			g.DrawImage(
				img,
				Rectangle.Round(dest),
				0,
				0,
				img.Width,
				img.Height,
				GraphicsUnit.Pixel,
				attrs);
		}
	}
}
