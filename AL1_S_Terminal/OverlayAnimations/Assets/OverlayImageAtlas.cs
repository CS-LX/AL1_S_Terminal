using System.Drawing;
using System.IO;

namespace AL1_S_Terminal.OverlayAnimations.Assets;

public sealed class OverlayImageAtlas : IDisposable
{
	public delegate Image ImageLoader(string absolutePath);

	private readonly IReadOnlyDictionary<string, string> _images;
	private readonly string _baseDir;
	private readonly ImageLoader _loader;
	private readonly Dictionary<string, Image> _cache = new();

	public OverlayImageAtlas(IReadOnlyDictionary<string, string> images, string baseDir, ImageLoader? loader = null)
	{
		_images = images;
		_baseDir = baseDir;
		_loader = loader ?? (path => Image.FromFile(path));
	}

	public bool TryGet(string key, out Image image)
	{
		if (!_images.TryGetValue(key, out var relativePath))
		{
			image = null!;
			return false;
		}

		if (_cache.TryGetValue(key, out var cached))
		{
			image = cached;
			return true;
		}

		var absolutePath = Path.Combine(_baseDir, relativePath);
		cached = _loader(absolutePath);
		_cache[key] = cached;
		image = cached;
		return true;
	}

	public void Dispose()
	{
		foreach (var img in _cache.Values)
			img.Dispose();
		_cache.Clear();
	}
}
