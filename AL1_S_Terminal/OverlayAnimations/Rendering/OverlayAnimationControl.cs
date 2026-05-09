using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Runtime;

namespace AL1_S_Terminal.OverlayAnimations.Rendering;

public sealed class OverlayAnimationControl : Control
{
	private readonly OverlayImageAtlas _atlas;
	private OverlayRenderSnapshot? _snapshot;

	public OverlayAnimationControl(OverlayImageAtlas atlas)
	{
		_atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));

		SetStyle(
			ControlStyles.AllPaintingInWmPaint
			| ControlStyles.UserPaint
			| ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.SupportsTransparentBackColor,
			true);

		DoubleBuffered = true;
		BackColor = Color.Transparent;
	}

	public void SetSnapshot(OverlayRenderSnapshot snapshot)
	{
		_snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);

		var g = e.Graphics;
		g.Clear(Color.Transparent);

		if (_snapshot is null)
			return;

		g.PixelOffsetMode = PixelOffsetMode.Half;
		g.InterpolationMode = InterpolationMode.HighQualityBicubic;
		g.SmoothingMode = SmoothingMode.HighQuality;

		foreach (var item in _snapshot.Items)
		{
			if (!_atlas.TryGet(item.ImageKey, out var img))
				continue;

			float opacity = (float)Math.Clamp(item.Opacity, 0.0, 1.0);
			float scale = (float)item.Scale;
			if (opacity <= 0f || scale <= 0f)
				continue;

			float w = img.Width * scale;
			float h = img.Height * scale;
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

