using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AL1_S_Terminal.OverlayAnimations.Config;

namespace AL1_S_Terminal.OverlayAnimations.Editor;

public partial class OverlayAnimationEditorWindow : Window {
    const string AliceFilter = "Alice 动画包 (*.alice)|*.alice|All files (*.*)|*.*";

    readonly EditorPreviewController _preview;
    readonly DispatcherTimer _debounce;
    EditorDocument _document = EditorDocument.CreateMinimalForPreview();
    string? _workspaceRoot;
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
        var defaultAlice = Path.Combine(AppContext.BaseDirectory, "Assets", "overlay_animations", "Default.alice");
        try {
            if (File.Exists(defaultAlice))
                LoadAlicePackage(defaultAlice);
            else
                CreateNewWorkspaceInternal();
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"加载默认包失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            try {
                CreateNewWorkspaceInternal();
            }
            catch (Exception ex2) {
                System.Windows.MessageBox.Show(this, $"新建工作区失败：{ex2.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
                _document = EditorDocument.CreateMinimalForPreview();
                RebuildTree();
                Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
                RefreshPreview();
            }
        }
    }

    protected override void OnClosed(EventArgs e) {
        UnsubscribeFrames();
        _debounce.Stop();
        _preview.Dispose();
        TearDownWorkspace();
        base.OnClosed(e);
    }

    void NewButton_Click(object sender, RoutedEventArgs e) {
        try {
            CreateNewWorkspaceInternal();
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"新建失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ImportButton_Click(object sender, RoutedEventArgs e) {
        var dlg = new Microsoft.Win32.OpenFileDialog {
            Filter = AliceFilter,
            InitialDirectory = GetAnimationsInitialDirectory()
        };
        if (dlg.ShowDialog(this) != true)
            return;
        try {
            LoadAlicePackage(dlg.FileName);
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"导入失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ExportButton_Click(object sender, RoutedEventArgs e) {
        if (string.IsNullOrWhiteSpace(_document.FilePath)) {
            ExportAsInternal();
            return;
        }

        try {
            SaveAliceToPath(_document.FilePath);
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"导出失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ExportAsButton_Click(object sender, RoutedEventArgs e) => ExportAsInternal();

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
            var baseDir = _workspaceRoot ?? AppContext.BaseDirectory;
            _preview.LoadConfig(cfg, baseDir, play);
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"预览失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void LoadAlicePackage(string alicePath) {
        UnsubscribeFrames();
        TearDownWorkspace();
        var root = Path.Combine(Path.GetTempPath(), "AL1_editor_ws_" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            OverlayAlicePackage.ExtractArchiveToDirectory(alicePath, root);
            var cfg = OverlayAlicePackage.LoadKeyFromDirectory(root);
            _document = EditorDocument.FromConfig(cfg);
            _document.FilePath = alicePath;
            _workspaceRoot = root;
            _preferredPreviewState = cfg.DefaultState;
            RebuildTree();
            Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
            RefreshPreview();
        }
        catch {
            OverlayAlicePackage.TryDeleteDirectory(root);
            _workspaceRoot = null;
            throw;
        }
    }

    void CreateNewWorkspaceInternal() {
        UnsubscribeFrames();
        TearDownWorkspace();
        var root = Path.Combine(Path.GetTempPath(), "AL1_editor_ws_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var assetsDir = Path.Combine(root, OverlayAlicePackage.AssetsFolderName);
        Directory.CreateDirectory(assetsDir);
        var logoPath = Path.Combine(assetsDir, "logo.png");
        WriteMinimalPlaceholderPng(logoPath);

        _document = EditorDocument.CreateMinimalForPreview();
        _document.FilePath = null;
        _workspaceRoot = root;
        _preferredPreviewState = _document.ToConfig().DefaultState;
        OverlayAlicePackage.WriteKeyToDirectory(root, _document.ToConfig());
        RebuildTree();
        Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
        RefreshPreview();
    }

    static void WriteMinimalPlaceholderPng(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var b = new Bitmap(1, 1);
        b.SetPixel(0, 0, Color.FromArgb(255, 200, 0, 200));
        b.Save(path, ImageFormat.Png);
    }

    void SaveAliceToPath(string alicePath) {
        if (_workspaceRoot is null)
            throw new InvalidOperationException("工作区未初始化。");
        var cfg = _document.ToConfig();
        OverlayAlicePackage.WriteKeyToDirectory(_workspaceRoot, cfg);
        OverlayAlicePackage.PackDirectoryToAlice(_workspaceRoot, alicePath);
        _document.FilePath = alicePath;
    }

    void ExportAsInternal() {
        if (_workspaceRoot is null) {
            System.Windows.MessageBox.Show(this, "工作区未就绪。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog {
            Filter = AliceFilter,
            InitialDirectory = GetAnimationsInitialDirectory(),
            FileName = Path.GetFileName(_document.FilePath ?? "Untitled.alice"),
            DefaultExt = ".alice"
        };
        if (dlg.ShowDialog(this) != true)
            return;
        try {
            SaveAliceToPath(dlg.FileName);
        }
        catch (Exception ex) {
            System.Windows.MessageBox.Show(this, $"导出失败：{ex.Message}", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    static string GetAnimationsInitialDirectory() {
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "overlay_animations");
        return Directory.Exists(dir) ? dir : AppContext.BaseDirectory;
    }

    void TearDownWorkspace() {
        if (_workspaceRoot is null)
            return;
        OverlayAlicePackage.TryDeleteDirectory(_workspaceRoot);
        _workspaceRoot = null;
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
