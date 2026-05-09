using System.IO;
using System.IO.Compression;
using System.Text;
using AL1_S_Terminal.OverlayAnimations.Model;

namespace AL1_S_Terminal.OverlayAnimations.Config;

/// <summary>
/// Overlay animation package: a ZIP file with extension <c>.alice</c>, containing <c>key.json</c> and an <c>assets/</c> tree.
/// Image paths in <see cref="OverlayAnimationConfig.Images"/> are relative to the package root (e.g. <c>assets/logo.png</c>).
/// </summary>
public static class OverlayAlicePackage {
	public const string KeyJsonEntryName = "key.json";
	public const string AssetsFolderName = "assets";

	/// <summary>
	/// Extracts the archive to a new temporary directory, reads <c>key.json</c>, and returns config + base directory for <see cref="OverlayImageAtlas"/>.
	/// Dispose to delete the temp directory.
	/// </summary>
	public static OverlayAliceExtractSession LoadExtracted(string alicePath) {
		ArgumentException.ThrowIfNullOrWhiteSpace(alicePath);
		if (!File.Exists(alicePath))
			throw new FileNotFoundException("Alice package not found.", alicePath);

		var ext = Path.GetExtension(alicePath);
		if (!ext.Equals(".alice", StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException($"Expected .alice file, got: '{ext}'.");

		var root = Path.Combine(Path.GetTempPath(), "AL1_S_Terminal_alice_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try {
			ExtractArchiveToDirectory(alicePath, root);
			var keyPath = Path.Combine(root, KeyJsonEntryName);
			if (!File.Exists(keyPath))
				throw new InvalidDataException($"Archive is missing '{KeyJsonEntryName}' at the root.");

			var cfg = OverlayAnimationConfigLoader.LoadFromFile(keyPath);
			return new OverlayAliceExtractSession(cfg, root, deleteRootOnDispose: true);
		}
		catch {
			TryDeleteDirectory(root);
			throw;
		}
	}

	/// <summary>
	/// Reads <c>key.json</c> from an existing directory layout (extracted package or editor workspace).
	/// </summary>
	public static OverlayAnimationConfig LoadKeyFromDirectory(string packageRoot) {
		ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
		var keyPath = Path.Combine(packageRoot, KeyJsonEntryName);
		if (!File.Exists(keyPath))
			throw new FileNotFoundException($"Missing '{KeyJsonEntryName}'.", keyPath);
		return OverlayAnimationConfigLoader.LoadFromFile(keyPath);
	}

	/// <summary>
	/// Writes <paramref name="cfg"/> to <c>key.json</c> under <paramref name="packageRoot"/> (UTF-8).
	/// </summary>
	public static void WriteKeyToDirectory(string packageRoot, OverlayAnimationConfig cfg) {
		ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
		ArgumentNullException.ThrowIfNull(cfg);
		Directory.CreateDirectory(packageRoot);
		var keyPath = Path.Combine(packageRoot, KeyJsonEntryName);
		OverlayAnimationConfigLoader.SaveToFile(keyPath, cfg);
	}

	/// <summary>
	/// Creates or overwrites an <c>.alice</c> zip from a directory that contains <c>key.json</c> and <c>assets/</c>.
	/// </summary>
	public static void PackDirectoryToAlice(string packageRoot, string alicePath) {
		ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
		ArgumentException.ThrowIfNullOrWhiteSpace(alicePath);
		var keyPath = Path.Combine(packageRoot, KeyJsonEntryName);
		if (!File.Exists(keyPath))
			throw new FileNotFoundException($"Workspace must contain '{KeyJsonEntryName}'.", keyPath);

		var assetsDir = Path.Combine(packageRoot, AssetsFolderName);
		var aliceDir = Path.GetDirectoryName(Path.GetFullPath(alicePath));
		if (!string.IsNullOrEmpty(aliceDir))
			Directory.CreateDirectory(aliceDir);

		var fullAlice = Path.GetFullPath(alicePath);
		if (fullAlice.StartsWith(Path.GetFullPath(packageRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
		    || string.Equals(fullAlice, Path.GetFullPath(packageRoot), StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Output .alice path must not be inside the workspace directory.");

		if (File.Exists(alicePath))
			File.Delete(alicePath);

		using var fs = File.Create(alicePath);
		using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8);

		AddFile(zip, keyPath, KeyJsonEntryName);

		if (Directory.Exists(assetsDir)) {
			var assetsPrefix = Path.GetFullPath(assetsDir);
			foreach (var file in Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories)) {
				var full = Path.GetFullPath(file);
				if (!full.StartsWith(assetsPrefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
				    && !string.Equals(full, assetsPrefix, StringComparison.OrdinalIgnoreCase))
					continue;
				var rel = Path.GetRelativePath(assetsPrefix, full)
					.Replace(Path.DirectorySeparatorChar, '/')
					.Replace(Path.AltDirectorySeparatorChar, '/');
				var entryName = $"{AssetsFolderName}/{rel}";
				AddFile(zip, file, entryName);
			}
		}
	}

	static void AddFile(ZipArchive zip, string absolutePath, string entryName) {
		var e = zip.CreateEntry(entryName, CompressionLevel.Optimal);
		using var input = File.OpenRead(absolutePath);
		using var output = e.Open();
		input.CopyTo(output);
	}

	/// <summary>Extracts all entries from an <c>.alice</c> zip to <paramref name="destDir"/> (must exist or be creatable).</summary>
	public static void ExtractArchiveToDirectory(string zipPath, string destDir) {
		Directory.CreateDirectory(destDir);
		using var archive = ZipFile.OpenRead(zipPath);
		var destFull = Path.GetFullPath(destDir);
		foreach (var entry in archive.Entries) {
			var fullName = entry.FullName.Replace('\\', '/');
			if (string.IsNullOrEmpty(fullName))
				continue;
			if (fullName.IndexOf("..", StringComparison.Ordinal) >= 0)
				throw new InvalidDataException($"Unsafe path in archive: '{entry.FullName}'.");

			var isDir = fullName.EndsWith('/');
			var relative = isDir ? fullName.TrimEnd('/') : fullName;
			if (string.IsNullOrEmpty(relative))
				continue;

			var target = Path.GetFullPath(Path.Combine(destDir, relative.Replace('/', Path.DirectorySeparatorChar)));
			if (!target.StartsWith(destFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			    && !string.Equals(target, destFull, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"Entry escapes target directory: '{entry.FullName}'.");

			if (isDir || string.IsNullOrEmpty(entry.Name)) {
				Directory.CreateDirectory(target);
				continue;
			}

			var parent = Path.GetDirectoryName(target);
			if (!string.IsNullOrEmpty(parent))
				Directory.CreateDirectory(parent);
			entry.ExtractToFile(target, overwrite: true);
		}
	}

	internal static void TryDeleteDirectory(string path) {
		try {
			if (Directory.Exists(path))
				Directory.Delete(path, recursive: true);
		}
		catch {
			// best effort (temp cleanup)
		}
	}
}

/// <summary>
/// Holds a loaded config and extracted root directory; disposing deletes the directory when this session owns it.
/// </summary>
public sealed class OverlayAliceExtractSession : IDisposable {
	public OverlayAnimationConfig Config { get; }
	public string BaseDirectory { get; }
	readonly string? _rootToDelete;
	bool _disposed;

	public OverlayAliceExtractSession(OverlayAnimationConfig config, string baseDirectory, bool deleteRootOnDispose) {
		Config = config;
		BaseDirectory = baseDirectory;
		_rootToDelete = deleteRootOnDispose ? baseDirectory : null;
	}

	public void Dispose() {
		if (_disposed)
			return;
		_disposed = true;
		if (_rootToDelete is not null)
			OverlayAlicePackage.TryDeleteDirectory(_rootToDelete);
	}
}
