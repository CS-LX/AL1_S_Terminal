using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace AL1_S_Terminal.OverlayAnimations.Assets;

public sealed class OverlayImageAtlas : IDisposable
{
	public delegate Image ImageLoader(string absolutePath);

	private readonly IReadOnlyDictionary<string, string> _images;
	private readonly string _baseDir;
	private readonly ImageLoader _loader;
	private readonly Dictionary<string, Image> _cache = new();
	private bool _disposed;

	public OverlayImageAtlas(IReadOnlyDictionary<string, string> images, string baseDir, ImageLoader? loader = null)
	{
		_images = images;
		_baseDir = baseDir;
		_loader = loader ?? (path => Image.FromFile(path));
	}

	public bool TryGet(string key, out Image image)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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

		var absolutePath = ResolveAndValidatePath(relativePath, key);

		cached = _loader(absolutePath);
		_cache[key] = cached;
		image = cached;
		return true;
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		List<Exception>? disposeErrors = null;
		foreach (var img in _cache.Values)
		{
			try
			{
				img.Dispose();
			}
			catch (Exception ex)
			{
				disposeErrors ??= [];
				disposeErrors.Add(ex);
			}
		}

		_cache.Clear();
		_disposed = true;

		if (disposeErrors is { Count: > 0 })
			throw new AggregateException("One or more images failed to dispose.", disposeErrors);
	}

	private string ResolveAndValidatePath(string relativePath, string key)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			throw new ArgumentException($"Image path for key '{key}' is null or whitespace.", nameof(relativePath));

		var baseFull = Path.GetFullPath(_baseDir);
		var basePrefix = Path.TrimEndingDirectorySeparator(baseFull);
		var combinedFull = Path.GetFullPath(Path.Combine(baseFull, relativePath));

		if (!IsUnderBaseDirectory(basePrefix, combinedFull))
		{
			throw new InvalidOperationException(
				$"Image path for key '{key}' is not under base directory: '{relativePath}'.");
		}

		return combinedFull;
	}

	private static bool IsUnderBaseDirectory(string basePrefix, string combinedFull)
	{
		var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;

		if (combinedFull.Length < basePrefix.Length)
			return false;
		if (!combinedFull.StartsWith(basePrefix, comparison))
			return false;
		if (combinedFull.Length == basePrefix.Length)
			return true;

		var boundary = combinedFull[basePrefix.Length];
		return boundary == Path.DirectorySeparatorChar || boundary == Path.AltDirectorySeparatorChar;
	}
}
