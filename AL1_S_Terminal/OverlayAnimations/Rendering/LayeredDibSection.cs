using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace AL1_S_Terminal.OverlayAnimations.Rendering;

[StructLayout(LayoutKind.Sequential)]
struct DibBitmapInfoHeader {
	public uint biSize;
	public int biWidth;
	public int biHeight;
	public ushort biPlanes;
	public ushort biBitCount;
	public uint biCompression;
	public uint biSizeImage;
	public int biXPelsPerMeter;
	public int biYPelsPerMeter;
	public uint biClrUsed;
	public uint biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
struct DibBitmapInfo {
	public DibBitmapInfoHeader bmiHeader;
}

/// <summary>
/// 32bpp top-down DIB + memory DC for <see cref="LayeredWindowInterop.UpdateLayeredWindowFromSourceDc"/>.
/// </summary>
internal sealed class LayeredDibSection : IDisposable {
	const uint BI_RGB = 0;
	const uint DIB_RGB_COLORS = 0;

	readonly int _width;
	readonly int _height;
	readonly int _dibStride;
	readonly IntPtr _screenDc;
	readonly IntPtr _memDc;
	readonly IntPtr _dibBitmap;
	readonly IntPtr _bits;
	readonly IntPtr _oldObj;
	byte[]? _rowScratch;
	bool _disposed;

	public LayeredDibSection(int width, int height) {
		if (width < 1 || height < 1)
			throw new ArgumentOutOfRangeException(nameof(width));
		_width = width;
		_height = height;
		_dibStride = ((width * 32 + 31) / 32) * 4;

		_screenDc = GdiNative.GetDC(IntPtr.Zero);
		if (_screenDc == IntPtr.Zero)
			throw new InvalidOperationException("GetDC failed.");

		_memDc = GdiNative.CreateCompatibleDC(_screenDc);
		if (_memDc == IntPtr.Zero) {
			GdiNative.ReleaseDC(IntPtr.Zero, _screenDc);
			throw new InvalidOperationException("CreateCompatibleDC failed.");
		}

		var hdr = new DibBitmapInfoHeader {
			biSize = (uint)Marshal.SizeOf<DibBitmapInfoHeader>(),
			biWidth = width,
			biHeight = -height,
			biPlanes = 1,
			biBitCount = 32,
			biCompression = BI_RGB,
			biSizeImage = 0,
			biXPelsPerMeter = 0,
			biYPelsPerMeter = 0,
			biClrUsed = 0,
			biClrImportant = 0
		};
		var bi = new DibBitmapInfo { bmiHeader = hdr };

		_dibBitmap = GdiNative.CreateDIBSection(_screenDc, ref bi, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
		if (_dibBitmap == IntPtr.Zero || _bits == IntPtr.Zero) {
			GdiNative.DeleteDC(_memDc);
			GdiNative.ReleaseDC(IntPtr.Zero, _screenDc);
			throw new InvalidOperationException($"CreateDIBSection failed ({Marshal.GetLastWin32Error()}).");
		}

		_oldObj = GdiNative.SelectObject(_memDc, _dibBitmap);
	}

	public bool CopyFromAndUpdateLayeredWindow(IntPtr hwnd, Bitmap premultipliedArgb) {
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (premultipliedArgb.Width != _width || premultipliedArgb.Height != _height)
			throw new ArgumentException("Bitmap size must match DIB.", nameof(premultipliedArgb));
		if (premultipliedArgb.PixelFormat != PixelFormat.Format32bppPArgb)
			throw new ArgumentException("Expected Format32bppPArgb.", nameof(premultipliedArgb));

		var rect = new Rectangle(0, 0, _width, _height);
		var data = premultipliedArgb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
		try {
			var srcStride = data.Stride;
			_rowScratch ??= new byte[_dibStride];
			for (var y = 0; y < _height; y++) {
				if (_dibStride > _width * 4)
					Array.Clear(_rowScratch, _width * 4, _dibStride - _width * 4);
				Marshal.Copy(IntPtr.Add(data.Scan0, y * srcStride), _rowScratch, 0, _width * 4);
				Marshal.Copy(_rowScratch, 0, IntPtr.Add(_bits, y * _dibStride), _dibStride);
			}
		}
		finally {
			premultipliedArgb.UnlockBits(data);
		}

		return LayeredWindowInterop.UpdateLayeredWindowFromSourceDc(hwnd, _memDc, _width, _height);
	}

	public void Dispose() {
		if (_disposed)
			return;
		_disposed = true;
		if (_memDc != IntPtr.Zero && _oldObj != IntPtr.Zero)
			GdiNative.SelectObject(_memDc, _oldObj);
		if (_dibBitmap != IntPtr.Zero)
			GdiNative.DeleteObject(_dibBitmap);
		if (_memDc != IntPtr.Zero)
			GdiNative.DeleteDC(_memDc);
		if (_screenDc != IntPtr.Zero)
			GdiNative.ReleaseDC(IntPtr.Zero, _screenDc);
	}

	static class GdiNative {
		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr GetDC(IntPtr hwnd);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern bool DeleteObject(IntPtr hObject);

		[DllImport("gdi32.dll", SetLastError = true)]
		public static extern IntPtr CreateDIBSection(
			IntPtr hdc,
			[In] ref DibBitmapInfo pbmi,
			uint usage,
			out IntPtr ppvBits,
			IntPtr hSection,
			uint dwOffset);
	}
}
