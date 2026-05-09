using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    bool _suppressFieldEvents;

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
                SyncDocumentFieldsToUi();
                Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
                RefreshPreview();
            }
        }
    }

    protected override void OnClosed(EventArgs e) {
        UnsubscribeFrames();
        _debounce.Stop();
        TearDownWorkspace();
        _preview.Dispose();
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

    void OverlaySizeBox_TextChanged(object sender, TextChangedEventArgs e) {
        if (_suppressFieldEvents)
            return;
        if (!int.TryParse(OverlayWidthBox.Text.Trim(), out var w))
            return;
        if (!int.TryParse(OverlayHeightBox.Text.Trim(), out var h))
            return;
        _document.OverlayWidth = Math.Clamp(w, 16, 8192);
        _document.OverlayHeight = Math.Clamp(h, 16, 8192);
        SchedulePreviewRefresh();
    }

    void DefaultStateCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (_suppressFieldEvents)
            return;
        if (DefaultStateCombo.SelectedItem is string s) {
            _document.DefaultState = s;
            SchedulePreviewRefresh();
        }
    }

    void StructureTree_OnContextMenuOpening(object sender, ContextMenuEventArgs e) {
        var tvi = FindAncestorTreeViewItem(e.OriginalSource as DependencyObject);
        if (tvi is null)
            return;

        var menu = new System.Windows.Controls.ContextMenu();

        if (tvi.Header as string == "States") {
            menu.Items.Add(MenuItemOf("添加状态", (_, _) => AddState()));
        }
        else if (tvi.Header as string == "Clips") {
            menu.Items.Add(MenuItemOf("添加片段", (_, _) => AddClip()));
        }
        else if (tvi.Tag is StateEditNode st) {
            menu.Items.Add(MenuItemOf("重命名状态", (_, _) => RenameState(st)));
            menu.Items.Add(MenuItemOf("绑定片段…", (_, _) => BindStateToClip(st)));
            menu.Items.Add(MenuItemOf("切换循环", (_, _) => ToggleStateLoop(st)));
            menu.Items.Add(MenuItemOf("删除状态", (_, _) => DeleteState(st)));
        }
        else if (tvi.Tag is ClipEditNode clip) {
            menu.Items.Add(MenuItemOf("重命名片段", (_, _) => RenameClip(clip)));
            menu.Items.Add(MenuItemOf("片段时长 (ms)…", (_, _) => EditClipDuration(clip)));
            menu.Items.Add(MenuItemOf("添加图层", (_, _) => AddLayer(clip)));
            menu.Items.Add(MenuItemOf("删除片段", (_, _) => DeleteClip(clip)));
        }
        else if (tvi.Tag is LayerEditNode layer) {
            menu.Items.Add(MenuItemOf("重命名图层", (_, _) => RenameLayer(layer)));
            menu.Items.Add(MenuItemOf("图块键…", (_, _) => ChangeLayerImageKey(layer)));
            menu.Items.Add(MenuItemOf("删除图层", (_, _) => DeleteLayer(layer)));
        }

        if (menu.Items.Count > 0) {
            tvi.ContextMenu = menu;
            menu.Closed += (_, _) => tvi.ContextMenu = null;
        }
    }

    static System.Windows.Controls.MenuItem MenuItemOf(string header, RoutedEventHandler onClick) {
        var mi = new System.Windows.Controls.MenuItem { Header = header };
        mi.Click += onClick;
        return mi;
    }

    void AddState() {
        if (!TryPrompt(this, "新状态名称", "State", out var name))
            return;
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) {
            System.Windows.MessageBox.Show(this, "名称不能为空。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_document.States.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))) {
            System.Windows.MessageBox.Show(this, "已存在同名状态。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var defaultClip = _document.Clips.FirstOrDefault()?.Name ?? "idle";
        _document.States.Add(new StateEditNode { Name = name, ClipName = defaultClip, Loop = true });
        RebuildTree();
        SyncDocumentFieldsToUi();
        SchedulePreviewRefresh();
    }

    void BindStateToClip(StateEditNode st) {
        var names = string.Join(", ", _document.Clips.Select(c => c.Name));
        if (!TryPrompt(this, $"片段名称（可用：{names}）", st.ClipName, out var clipName))
            return;
        clipName = clipName.Trim();
        if (string.IsNullOrEmpty(clipName))
            return;
        if (!_document.Clips.Any(c => string.Equals(c.Name, clipName, StringComparison.Ordinal))) {
            System.Windows.MessageBox.Show(this, "找不到该片段。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        st.ClipName = clipName;
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void ToggleStateLoop(StateEditNode st) {
        st.Loop = !st.Loop;
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void RenameState(StateEditNode st) {
        if (!TryPrompt(this, "重命名状态", st.Name, out var name))
            return;
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || string.Equals(name, st.Name, StringComparison.Ordinal))
            return;
        if (_document.States.Any(s => !ReferenceEquals(s, st) && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))) {
            System.Windows.MessageBox.Show(this, "已存在同名状态。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.Equals(_document.DefaultState, st.Name, StringComparison.Ordinal))
            _document.DefaultState = name;
        st.Name = name;
        RebuildTree();
        SyncDocumentFieldsToUi();
        SchedulePreviewRefresh();
    }

    void DeleteState(StateEditNode st) {
        if (_document.States.Count <= 1) {
            System.Windows.MessageBox.Show(this, "至少保留一个状态。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (System.Windows.MessageBox.Show(this, $"删除状态「{st.Name}」？", "动画编辑器", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (string.Equals(_document.DefaultState, st.Name, StringComparison.Ordinal))
            _document.DefaultState = _document.States.First(s => !ReferenceEquals(s, st)).Name;

        _document.States.Remove(st);
        RebuildTree();
        SyncDocumentFieldsToUi();
        SchedulePreviewRefresh();
    }

    void AddClip() {
        if (!TryPrompt(this, "新片段名称", "clip", out var name))
            return;
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) {
            System.Windows.MessageBox.Show(this, "名称不能为空。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_document.Clips.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) {
            System.Windows.MessageBox.Show(this, "已存在同名片段。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var imgKey = _document.Images.Keys.FirstOrDefault() ?? "logo";
        var clip = new ClipEditNode { Name = name, DurationMs = 1000 };
        clip.Layers.Add(new LayerEditNode {
            LayerKey = "L",
            ParentClipName = name,
            ImageKey = imgKey,
            Frames = { new KeyframeRow { T = 0, X = 0, Y = 0, Opacity = 1, Scale = 1 } }
        });
        _document.Clips.Add(clip);
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void EditClipDuration(ClipEditNode clip) {
        if (!TryPrompt(this, "片段时长 (毫秒)", clip.DurationMs.ToString(), out var txt))
            return;
        if (!int.TryParse(txt.Trim(), out var ms) || ms < 1) {
            System.Windows.MessageBox.Show(this, "请输入正整数毫秒。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        clip.DurationMs = ms;
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void RenameClip(ClipEditNode clip) {
        if (!TryPrompt(this, "重命名片段", clip.Name, out var name))
            return;
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || string.Equals(name, clip.Name, StringComparison.Ordinal))
            return;
        if (_document.Clips.Any(c => !ReferenceEquals(c, clip) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) {
            System.Windows.MessageBox.Show(this, "已存在同名片段。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var old = clip.Name;
        foreach (var s in _document.States.Where(s => string.Equals(s.ClipName, old, StringComparison.Ordinal)))
            s.ClipName = name;
        foreach (var l in clip.Layers)
            l.ParentClipName = name;
        clip.Name = name;
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void DeleteClip(ClipEditNode clip) {
        var used = _document.States.Where(s => string.Equals(s.ClipName, clip.Name, StringComparison.Ordinal)).Select(s => s.Name).ToList();
        if (used.Count > 0) {
            System.Windows.MessageBox.Show(this,
                $"以下状态仍引用该片段，无法删除：{string.Join(", ", used)}",
                "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (System.Windows.MessageBox.Show(this, $"删除片段「{clip.Name}」？", "动画编辑器", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _document.Clips.Remove(clip);
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void AddLayer(ClipEditNode clip) {
        var baseKey = "layer";
        var n = 1;
        var key = baseKey;
        while (clip.Layers.Any(l => string.Equals(l.LayerKey, key, StringComparison.OrdinalIgnoreCase)))
            key = $"{baseKey}{n++}";

        var imgKey = _document.Images.Keys.FirstOrDefault() ?? "logo";
        clip.Layers.Add(new LayerEditNode {
            LayerKey = key,
            ParentClipName = clip.Name,
            ImageKey = imgKey,
            Frames = { new KeyframeRow { T = 0, X = 0, Y = 0, Opacity = 1, Scale = 1 } }
        });
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void RenameLayer(LayerEditNode layer) {
        if (!TryPrompt(this, "重命名图层键", layer.LayerKey, out var key))
            return;
        key = key.Trim();
        if (string.IsNullOrEmpty(key) || string.Equals(key, layer.LayerKey, StringComparison.Ordinal))
            return;
        var clip = _document.Clips.FirstOrDefault(c => c.Name == layer.ParentClipName);
        if (clip is null)
            return;
        if (clip.Layers.Any(l => !ReferenceEquals(l, layer) && string.Equals(l.LayerKey, key, StringComparison.OrdinalIgnoreCase))) {
            System.Windows.MessageBox.Show(this, "该片段下已有同名图层键。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        layer.LayerKey = key;
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void ChangeLayerImageKey(LayerEditNode layer) {
        var keys = string.Join(", ", _document.Images.Keys);
        if (!TryPrompt(this, $"图块键（可用：{keys}）", layer.ImageKey, out var ik))
            return;
        ik = ik.Trim();
        if (string.IsNullOrEmpty(ik))
            return;
        layer.ImageKey = ik;
        RebuildTree();
        SchedulePreviewRefresh();
    }

    void DeleteLayer(LayerEditNode layer) {
        var clip = _document.Clips.FirstOrDefault(c => c.Name == layer.ParentClipName);
        if (clip is null)
            return;
        if (clip.Layers.Count <= 1) {
            System.Windows.MessageBox.Show(this, "片段至少保留一个图层。", "动画编辑器", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (System.Windows.MessageBox.Show(this, $"删除图层「{layer.LayerKey}」？", "动画编辑器", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        clip.Layers.Remove(layer);
        RebuildTree();
        SchedulePreviewRefresh();
    }

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

        if (e.NewValue is TreeViewItem { Tag: ClipEditNode clip }) {
            UnsubscribeFrames();
            KeyframesGrid.ItemsSource = null;
            _selectedLayer = null;
            _preferredPreviewState = _document.States.FirstOrDefault(s => string.Equals(s.ClipName, clip.Name, StringComparison.Ordinal))?.Name
                ?? _document.DefaultState;
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
            PreviewHost.Width = cfg.Width + 8;
            PreviewHost.Height = cfg.Height + 8;
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
            SyncDocumentFieldsToUi();
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
        SyncDocumentFieldsToUi();
        Dispatcher.BeginInvoke(SelectFirstLayer, DispatcherPriority.Loaded);
        RefreshPreview();
    }

    static void WriteMinimalPlaceholderPng(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var b = new Bitmap(1, 1);
        b.SetPixel(0, 0, System.Drawing.Color.FromArgb(255, 200, 0, 200));
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
        _preview.ReleaseWorkspaceFileLocks();
        OverlayAlicePackage.TryDeleteDirectory(_workspaceRoot);
        _workspaceRoot = null;
    }

    void SyncDocumentFieldsToUi() {
        _suppressFieldEvents = true;
        try {
            OverlayWidthBox.Text = _document.OverlayWidth.ToString();
            OverlayHeightBox.Text = _document.OverlayHeight.ToString();
            DefaultStateCombo.Items.Clear();
            foreach (var s in _document.States)
                DefaultStateCombo.Items.Add(s.Name);
            DefaultStateCombo.SelectedItem = _document.States.Any(x => x.Name == _document.DefaultState)
                ? _document.DefaultState
                : _document.States.FirstOrDefault()?.Name;
        }
        finally {
            _suppressFieldEvents = false;
        }
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
            var clipTi = new TreeViewItem {
                Header = $"{c.Name} ({c.DurationMs} ms)",
                Tag = c,
                IsExpanded = true
            };
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

    static TreeViewItem? FindAncestorTreeViewItem(DependencyObject? src) {
        while (src is not null) {
            if (src is TreeViewItem tvi)
                return tvi;
            src = VisualTreeHelper.GetParent(src);
        }
        return null;
    }

    static bool TryPrompt(Window owner, string title, string initial, out string result) {
        var captured = initial;
        var dlg = new Window {
            Title = title,
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false
        };
        var tb = new System.Windows.Controls.TextBox { Text = captured, MinWidth = 280, Margin = new Thickness(12, 12, 12, 0) };
        var row = new StackPanel {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(12, 12, 12, 12)
        };
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 72, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new System.Windows.Controls.Button { Content = "取消", Width = 72, IsCancel = true };
        ok.Click += (_, _) => {
            captured = tb.Text;
            dlg.DialogResult = true;
        };
        cancel.Click += (_, _) => { dlg.DialogResult = false; };
        row.Children.Add(ok);
        row.Children.Add(cancel);
        var sp = new StackPanel();
        sp.Children.Add(tb);
        sp.Children.Add(row);
        dlg.Content = sp;
        dlg.KeyDown += (_, k) => {
            if (k.Key == Key.Escape)
                dlg.DialogResult = false;
        };
        var okResult = dlg.ShowDialog() == true;
        result = captured;
        return okResult;
    }
}
