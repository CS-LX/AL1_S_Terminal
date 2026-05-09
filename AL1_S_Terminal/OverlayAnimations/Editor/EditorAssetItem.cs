using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AL1_S_Terminal.OverlayAnimations.Editor;

/// <summary>One row in the editor Assets grid (logical key → package-relative path).</summary>
public sealed class EditorAssetItem {
    public string Key { get; }
    public string RelativePath { get; }
    public string FullPath { get; }
    public ImageSource? Preview { get; }

    public EditorAssetItem(string key, string relativePath, string fullPath) {
        Key = key;
        RelativePath = relativePath;
        FullPath = fullPath;
        Preview = TryLoadThumbnail(fullPath);
    }

    static ImageSource? TryLoadThumbnail(string fullPath) {
        try {
            if (!File.Exists(fullPath))
                return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(Path.GetFullPath(fullPath));
            bi.DecodePixelWidth = 80;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch {
            return null;
        }
    }

    public static string SanitizeKeyStem(string fileNameWithoutExtension) {
        var sb = new StringBuilder();
        foreach (var c in fileNameWithoutExtension.Trim()) {
            if (char.IsLetterOrDigit(c) || c is '_' or '-')
                sb.Append(c);
        }
        var s = sb.ToString();
        return string.IsNullOrEmpty(s) ? "asset" : s;
    }

    public static string UniqueFileNameInDirectory(string assetsDir, string desiredFileName) {
        var name = Path.GetFileName(desiredFileName);
        if (string.IsNullOrEmpty(name))
            name = "image.png";
        var dest = Path.Combine(assetsDir, name);
        if (!File.Exists(dest))
            return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 2; ; i++) {
            var candidate = $"{stem}_{i}{ext}";
            if (!File.Exists(Path.Combine(assetsDir, candidate)))
                return candidate;
        }
    }

    public static string UniqueImageKey(IDictionary<string, string> images, string baseKey) {
        var key = baseKey;
        for (var i = 2; images.ContainsKey(key); i++)
            key = $"{baseKey}_{i}";
        return key;
    }
}
