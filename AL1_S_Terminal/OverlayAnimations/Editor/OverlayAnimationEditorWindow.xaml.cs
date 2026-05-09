using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AL1_S_Terminal.OverlayAnimations.Config;

namespace AL1_S_Terminal.OverlayAnimations.Editor;

public partial class OverlayAnimationEditorWindow : Window {
    readonly EditorPreviewController _preview;
    readonly DispatcherTimer _debounce;
    EditorDocument _document = EditorDocument.CreateMinimalForPreview();
    string? _preferredPreviewState;
    LayerEditNode? _selectedLayer;

    public OverlayAnimationEditorWindow() {
        InitializeComponent();
        _preview = new EditorPreviewController(PreviewHost);
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Tick += (_, _) => {
            _debounce.Stop();
            RefreshPreview();
        };
        Loaded += Window_Loaded;
    }

    void Window_Loaded(object sender, RoutedEventArgs e) {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "overlay_animations", "default.json");
        try {
            if (File.Exists(path))
                LoadFromPath(path, showErrors: false);
            else {
                _document = EditorDocument.CreateMinimalForPreview();
                RebuildTree();
                Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
                RefreshPreview();
            }
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"加载默认配置失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            RebuildTree();
            Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
            RefreshPreview();
        }
    }

    protected override void OnClosed(EventArgs e) {
        UnsubscribeFrames();
        _debounce.Stop();
        _preview.Dispose();
        base.OnClosed(e);
    }

    void OpenButton_Click(object sender, RoutedEventArgs e) {
        var dlg = new Microsoft.Win32.OpenFileDialog {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = GetAnimationsInitialDirectory()
        };
        if (dlg.ShowDialog(this) != true)
            return;
        LoadFromPath(dlg.FileName, showErrors: true);
    }

    void SaveButton_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrWhiteSpace(_document.FilePath)) {
            SaveAsInternal();
            return;
        }

        try {
            var cfg = _document.ToConfig();
            OverlayAnimationConfigLoader.SaveToFile(_document.FilePath, cfg);
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"保存失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void SaveAsButton_Click(object sender, RoutedEventArgs e) => SaveAsInternal();

    void PlayButton_Click(object sender, RoutedEventArgs e) => _preview.Play();

    void PauseButton_Click(object sender, RoutedEventArgs e) => _preview.Pause();

    void RestartButton_Click(object sender, RoutedEventArgs e) => _preview.Restart();

    void StructureTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
        if (e.NewValue is TreeViewItem { Tag: LayerEditNode layer }) {
            UnsubscribeFrames();
            _selectedLayer = layer;
            layer.Frames.CollectionChanged += OnFramesCollectionChanged;
            KeyframesGrid.ItemsSource = layer.Frames;
            SchedulePreviewRefresh();
            return;
        }

        if (e.NewValue is TreeViewItem { Tag: StateEditNode state }) {
            UnsubscribeFrames();
            KeyframesGrid.ItemsSource = null;
            _selectedLayer = null;
            _preferredPreviewState = state.Name;
            SchedulePreviewRefresh();
            return;
        }

        UnsubscribeFrames();
        KeyframesGrid.ItemsSource = null;
        _selectedLayer = null;
    }

    void KeyframesGrid_OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e) =>
        SchedulePreviewRefresh();

    void KeyframesGrid_OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e) =>
        SchedulePreviewRefresh();

    void OnFramesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SchedulePreviewRefresh();

    void UnsubscribeFrames() {
        if (_selectedLayer is null)
            return;
        _selectedLayer.Frames.CollectionChanged -= OnFramesCollectionChanged;
        _selectedLayer = null;
    }

    void SchedulePreviewRefresh() {
        _debounce.Stop();
        _debounce.Start();
    }

    void RefreshPreview() {
        try {
            var cfg = _document.ToConfig();
            var play = _preferredPreviewState;
            if (string.IsNullOrEmpty(play) || !cfg.States.ContainsKey(play))
                play = cfg.DefaultState;
            _preview.LoadConfig(cfg, AppContext.BaseDirectory, play);
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"预览失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void LoadFromPath(string path, bool showErrors) {
        UnsubscribeFrames();
        try {
            var cfg = OverlayAnimationConfigLoader.LoadFromFile(path);
            _document = EditorDocument.FromConfig(cfg);
            _document.FilePath = path;
            _preferredPreviewState = cfg.DefaultState;
            RebuildTree();
            Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
            RefreshPreview();
        }
        catch (Exception ex) {
            if (showErrors)
                System.Windows.MessageBox.Show(this, $"打开失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                throw;
        }
    }

    void SaveAsInternal() {
        var dlg = new Microsoft.Win32.SaveFileDialog {
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = GetAnimationsInitialDirectory(),
            FileName = Path.GetFileName(_document.FilePath ?? "default.json")
        };
        if (dlg.ShowDialog(this) != true)
            return;
        try {
            var cfg = _document.ToConfig();
            OverlayAnimationConfigLoader.SaveToFile(dlg.FileName, cfg);
            _document.FilePath = dlg.FileName;
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"保存失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static string GetAnimationsInitialDirectory() {
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "overlay_animations");
        return Directory.Exists(dir) ? dir : AppContext.BaseDirectory;
    }

    void RebuildTree() {
        StructureTree.Items.Clear();
        var statesRoot = new TreeViewItem { Header = "States", IsExpanded = true };
        foreach (var s in _document.States) {
            var ti = new TreeViewItem {
                Header = $"{s.Name} → {s.ClipName} (loop: {s.Loop})",
                Tag = s
            };
            statesRoot.Items.Add(ti);
        }
        StructureTree.Items.Add(statesRoot);

        var clipsRoot = new TreeViewItem { Header = "Clips", IsExpanded = true };
        foreach (var c in _document.Clips) {
            var clipTi = new TreeViewItem { Header = $"{c.Name} ({c.DurationMs} ms)", IsExpanded = true };
            foreach (var layer in c.Layers) {
                var layerTi = new TreeViewItem {
                    Header = $"{layer.LayerKey} [{layer.ImageKey}]",
                    Tag = layer
                };
                clipTi.Items.Add(layerTi);
            }
            clipsRoot.Items.Add(clipTi);
        }
        StructureTree.Items.Add(clipsRoot);
    }

    void SelectFirstLayer() {
        foreach (var root in StructureTree.Items.OfType<TreeViewItem>()) {
            if (root.Header as string != "Clips")
                continue;
            foreach (var clipTi in root.Items.OfType<TreeViewItem>()) {
                foreach (var layerTi in clipTi.Items.OfType<TreeViewItem>()) {
                    if (layerTi.Tag is LayerEditNode) {
                        layerTi.IsSelected = true;
                        return;
                    }
                }
            }
        }
    }
}
