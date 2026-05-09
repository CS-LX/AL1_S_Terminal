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

	/// <summary>Updates layered window from a memory DC that has a 32bpp premultiplied DIB selected (size <paramref name="width"/>×<paramref name="height"/>).</summary>
	public static bool UpdateLayeredWindowFromSourceDc(IntPtr hwnd, IntPtr hdcSrc, int width, int height) {
		var ptSrc = new POINT { X = 0, Y = 0 };
		var size = new SIZE { Width = width, Height = height };
		var blend = new BLENDFUNCTION {
			BlendOp = AC_SRC_OVER,
			BlendFlags = 0,
			SourceConstantAlpha = 255,
			AlphaFormat = AC_SRC_ALPHA
		};
		return UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, hdcSrc, ref ptSrc, 0, ref blend, ULW_ALPHA);
	}
}
