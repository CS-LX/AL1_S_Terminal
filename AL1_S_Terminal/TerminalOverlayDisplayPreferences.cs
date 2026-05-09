using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AL1_S_Terminal.Win32;

namespace AL1_S_Terminal;

/// <summary>持久化 overlay 在终端窗口内的角落（左下/右下）；右下时内容水平镜像。</summary>
public static class TerminalOverlayDisplayPreferences {
    static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AL1_S_Terminal", "overlay_display.json");

    static TerminalOverlayScreenCorner _corner = TerminalOverlayScreenCorner.LeftBottom;
    static bool _loaded;

    public static TerminalOverlayScreenCorner Corner {
        get {
            EnsureLoaded();
            return _corner;
        }
        set {
            EnsureLoaded();
            if (_corner == value)
                return;
            _corner = value;
            Save();
        }
    }

    public static bool MirrorOverlayContent =>
        Corner == TerminalOverlayScreenCorner.RightBottom;

    static void EnsureLoaded() {
        if (_loaded)
            return;
        _loaded = true;
        try {
            var path = FilePath;
            if (!File.Exists(path))
                return;
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions);
            if (dto?.Corner is TerminalOverlayScreenCorner c)
                _corner = c;
        }
        catch {
            // keep default
        }
    }

    static void Save() {
        try {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var dto = new Dto { Corner = _corner };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, JsonOptions));
        }
        catch {
            // ignore disk errors
        }
    }

    sealed class Dto {
        public TerminalOverlayScreenCorner Corner { get; set; }
    }
}
