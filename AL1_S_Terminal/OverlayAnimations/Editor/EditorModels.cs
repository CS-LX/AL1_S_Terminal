using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AL1_S_Terminal.OverlayAnimations.Model;

namespace AL1_S_Terminal.OverlayAnimations.Editor;

public sealed class KeyframeRow : INotifyPropertyChanged {
    public KeyframeRow() { }

    int _t;
    int _x;
    int _y;
    double _opacity = 1;
    double _scale = 1;

    public int T {
        get => _t;
        set => SetField(ref _t, value);
    }

    public int X {
        get => _x;
        set => SetField(ref _x, value);
    }

    public int Y {
        get => _y;
        set => SetField(ref _y, value);
    }

    public double Opacity {
        get => _opacity;
        set => SetField(ref _opacity, value);
    }

    public double Scale {
        get => _scale;
        set => SetField(ref _scale, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static KeyframeRow FromKeyframe(OverlayAnimationKeyframe k) =>
        new() {
            T = k.T,
            X = k.X,
            Y = k.Y,
            Opacity = k.Opacity,
            Scale = k.Scale
        };

    public OverlayAnimationKeyframe ToKeyframe() =>
        new() {
            T = T,
            X = X,
            Y = Y,
            Opacity = Opacity,
            Scale = Scale
        };

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        where T : struct {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public sealed class StateEditNode {
    public string Name { get; set; } = string.Empty;

    public string ClipName { get; set; } = string.Empty;

    public bool Loop { get; set; }
}

public sealed class LayerEditNode {
    public string LayerKey { get; set; } = string.Empty;

    public string ParentClipName { get; set; } = string.Empty;

    public string ImageKey { get; set; } = string.Empty;

    public ObservableCollection<KeyframeRow> Frames { get; } = new();
}

public sealed class ClipEditNode {
    public string Name { get; set; } = string.Empty;

    public int DurationMs { get; set; }

    public ObservableCollection<LayerEditNode> Layers { get; } = new();
}

public sealed class EditorDocument {
    public int Version { get; set; } = 1;

    public string? FilePath { get; set; }

    public string DefaultState { get; set; } = string.Empty;

    public Dictionary<string, string> Images { get; } = new();

    public ObservableCollection<StateEditNode> States { get; } = new();

    public ObservableCollection<ClipEditNode> Clips { get; } = new();

    /// <summary>Minimal valid config so preview can run before the user opens a file.</summary>
    public static EditorDocument CreateMinimalForPreview() {
        var cfg = new OverlayAnimationConfig {
            Version = 1,
            DefaultState = "Idle"
        };
        cfg.Images["logo"] = "assets/logo.png";
        cfg.States["Idle"] = new OverlayAnimationStateConfig { Clip = "idle", Loop = true };
        var clip = new OverlayAnimationClipConfig { DurationMs = 1000 };
        clip.Layers["L"] = new OverlayAnimationLayerConfig {
            ImageKey = "logo",
            Frames = { new OverlayAnimationKeyframe { T = 0, X = 0, Y = 0, Opacity = 1, Scale = 1 } }
        };
        cfg.Clips["idle"] = clip;
        return FromConfig(cfg);
    }

    public static EditorDocument FromConfig(OverlayAnimationConfig cfg) {
        var doc = new EditorDocument {
            Version = cfg.Version,
            DefaultState = cfg.DefaultState
        };
        foreach (var (k, v) in cfg.Images)
            doc.Images[k] = v;

        foreach (var (name, s) in cfg.States)
            doc.States.Add(new StateEditNode { Name = name, ClipName = s.Clip, Loop = s.Loop });

        foreach (var (clipName, clip) in cfg.Clips) {
            var ce = new ClipEditNode { Name = clipName, DurationMs = clip.DurationMs };
            foreach (var (layerKey, layer) in clip.Layers) {
                var le = new LayerEditNode {
                    LayerKey = layerKey,
                    ParentClipName = clipName,
                    ImageKey = layer.ImageKey
                };
                foreach (var f in layer.Frames.OrderBy(x => x.T))
                    le.Frames.Add(KeyframeRow.FromKeyframe(f));
                ce.Layers.Add(le);
            }
            doc.Clips.Add(ce);
        }

        return doc;
    }

    public OverlayAnimationConfig ToConfig() {
        var cfg = new OverlayAnimationConfig {
            Version = Version,
            DefaultState = DefaultState,
            Images = new Dictionary<string, string>(Images)
        };

        foreach (var s in States)
            cfg.States[s.Name] = new OverlayAnimationStateConfig { Clip = s.ClipName, Loop = s.Loop };

        foreach (var c in Clips) {
            var clip = new OverlayAnimationClipConfig { DurationMs = c.DurationMs };
            foreach (var layer in c.Layers) {
                var lc = new OverlayAnimationLayerConfig { ImageKey = layer.ImageKey };
                foreach (var row in layer.Frames.OrderBy(r => r.T))
                    lc.Frames.Add(row.ToKeyframe());
                clip.Layers[layer.LayerKey] = lc;
            }
            cfg.Clips[c.Name] = clip;
        }

        return cfg;
    }
}
