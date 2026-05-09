using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AL1_S_Terminal.OverlayAnimations.Assets;
using AL1_S_Terminal.OverlayAnimations.Runtime;

namespace AL1_S_Terminal.OverlayAnimations.Rendering;

public sealed class OverlayAnimationControl : Control {
	readonly OverlayImageAtlas _atlas;
	OverlayRenderSnapshot? _snapshot;

	public OverlayAnimationControl(OverlayImageAtlas atlas) {
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

	public void SetSnapshot(OverlayRenderSnapshot snapshot) {
		_snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e) {
		base.OnPaint(e);
		OverlayAnimationPainter.Draw(e.Graphics, _atlas, _snapshot);
	}
}
