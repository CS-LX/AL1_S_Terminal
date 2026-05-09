using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AL1_S_Terminal.OverlayAnimations.Rendering;

/// <summary>Minimal P/Invoke for per-pixel alpha layered top-level windows.</summary>
internal static class LayeredWindowInterop {
	const uint ULW_ALPHA = 0x02;

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT {
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct SIZE {
		public int Width;
		public int Height;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct BLENDFUNCTION {
		public byte BlendOp;
		public byte BlendFlags;
		public byte SourceConstantAlpha;
		public byte AlphaFormat;
	}

	public const byte AC_SRC_OVER = 0x00;
	public const byte AC_SRC_ALPHA = 0x01;

	[DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
	static extern bool UpdateLayeredWindow(
		IntPtr hwnd,
		IntPtr hdcDst,
		IntPtr pptDst,
		ref SIZE psize,
		IntPtr hdcSrc,
		ref POINT pptSrc,
		int crKey,
		ref BLENDFUNCTION pblend,
		uint dwFlags);

	/// <summary>
	/// Updates the layered window bits from a 32bpp bitmap with premultiplied alpha (<see cref="PixelFormat.Format32bppPArgb"/>).
	/// </summary>
	public static bool UpdateFromPremultipliedBitmap(IntPtr hwnd, Bitmap bitmap) {
		if (bitmap.PixelFormat != PixelFormat.Format32bppPArgb)
			throw new ArgumentException("Bitmap must be Format32bppPArgb for layered alpha.", nameof(bitmap));

		using var g = Graphics.FromImage(bitmap);
		var hdcSrc = g.GetHdc();
		try {
			var ptSrc = new POINT { X = 0, Y = 0 };
			var size = new SIZE { Width = bitmap.Width, Height = bitmap.Height };
			var blend = new BLENDFUNCTION {
				BlendOp = AC_SRC_OVER,
				BlendFlags = 0,
				SourceConstantAlpha = 255,
				AlphaFormat = AC_SRC_ALPHA
			};
			return UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, hdcSrc, ref ptSrc, 0, ref blend, ULW_ALPHA);
		}
		finally {
			g.ReleaseHdc(hdcSrc);
		}
	}
}
