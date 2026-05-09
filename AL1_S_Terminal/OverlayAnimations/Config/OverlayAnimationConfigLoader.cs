using System.IO;
using System.Text.Json;
using AL1_S_Terminal.OverlayAnimations.Model;

namespace AL1_S_Terminal.OverlayAnimations.Config;

public static class OverlayAnimationConfigLoader
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	public static OverlayAnimationConfig FromJson(string json)
	{
		var cfg = JsonSerializer.Deserialize<OverlayAnimationConfig>(json, Options);
		if (cfg is null)
			throw new JsonException("Deserialized config is null.");
		return cfg;
	}

	public static string ToJson(OverlayAnimationConfig cfg) =>
		JsonSerializer.Serialize(cfg ?? throw new ArgumentNullException(nameof(cfg)), Options);

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
