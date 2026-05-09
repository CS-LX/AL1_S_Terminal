using System.IO;
using System.Text.Json;
using AL1_S_Terminal.OverlayAnimations.Model;

namespace AL1_S_Terminal.OverlayAnimations.Config;

public static class OverlayAnimationConfigLoader
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	/// <summary>
	/// Deserializes overlay animation config from JSON text.
	/// Throws <see cref="JsonException"/> when the text is invalid JSON or deserialization produces a null root value.
	/// </summary>
	public static OverlayAnimationConfig FromJson(string json)
	{
		var cfg = JsonSerializer.Deserialize<OverlayAnimationConfig>(json, Options);
		if (cfg is null)
			throw new JsonException("Deserialized config is null.");
		NormalizeOverlayDimensions(cfg);
		return cfg;
	}

	/// <summary>System.Text.Json leaves missing numeric members as 0; enforce sane overlay size.</summary>
	public static void NormalizeOverlayDimensions(OverlayAnimationConfig cfg)
	{
		if (cfg.Width < 16 || cfg.Width > 8192)
			cfg.Width = 200;
		if (cfg.Height < 16 || cfg.Height > 8192)
			cfg.Height = 200;
	}

	public static string ToJson(OverlayAnimationConfig cfg) =>
		JsonSerializer.Serialize(cfg ?? throw new ArgumentNullException(nameof(cfg)), Options);

	/// <summary>
	/// Reads a UTF-8 text file and deserializes it as <see cref="OverlayAnimationConfig"/>.
	/// Throws <see cref="JsonException"/> when the file contents are invalid JSON or deserialization produces a null root value.
	/// </summary>
	public static OverlayAnimationConfig LoadFromFile(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return FromJson(File.ReadAllText(path));
	}

	public static void SaveToFile(string path, OverlayAnimationConfig cfg)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(path, ToJson(cfg));
	}
}
