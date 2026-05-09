# Overlay 动画编辑器（最小版）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 增加一个 WPF 动画编辑器：可打开/另存为动画 JSON，Tree+DataGrid 编辑关键帧，右侧用 `WindowsFormsHost` 复用 `OverlayAnimationControl` 实时预览播放。

**Architecture:** 采用 WPF 窗口作为编辑器 UI，预览复用 WinForms 渲染控件，通过一个 `EditorPreviewController`（或等价类）在 UI 线程用 `DispatcherTimer` 驱动 `OverlayAnimator.Sample` 并推送 snapshot；编辑区的变更以 debounce 触发“重建 config + 重启预览”。序列化复用 `OverlayAnimationConfigLoader`。

**Tech Stack:** WPF (`Window`/`TreeView`/`DataGrid`/`DispatcherTimer`), WinForms (`OverlayAnimationControl` via `WindowsFormsHost`), `System.Text.Json`, 现有动画运行时模块。

---

## 先决条件（已具备）

- 运行时：`OverlayAnimator`（插值/loop）+ `OverlayImageAtlas`（缓存/路径约束）+ `OverlayAnimationControl`（绘制）+ `OverlayAnimationConfigLoader`
- Overlay 侧：`TerminalOverlayForm` 已可播放默认动画并暴露 `Animator`
- 托盘：已有 “动画：Idle/Pulse” 切换项

---

## 文件结构（本计划将创建/修改）

### 新增（编辑器 UI + 预览控制）
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/OverlayAnimationEditorWindow.xaml`
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/OverlayAnimationEditorWindow.xaml.cs`
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/EditorModels.cs`
  - WPF 绑定模型（State/Clip/Layer/Keyframe 的 ObservableCollection）
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/EditorPreviewController.cs`
  - 预览运行时：atlas+control+animator+timer，提供 `Attach(host)` / `LoadConfig(cfg, baseDir)` / `Play(state)` / `Pause()` / `Dispose()`

### 修改（托盘入口）
- Modify: `AL1_S_Terminal/App.xaml.cs`
  - 增加菜单项 “动画编辑器…” 打开窗口

---

## Task 1: 建立编辑器窗口骨架 + 托盘入口

**Files:**
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/OverlayAnimationEditorWindow.xaml`
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/OverlayAnimationEditorWindow.xaml.cs`
- Modify: `AL1_S_Terminal/App.xaml.cs`

- [ ] **Step 1: 创建窗口 XAML（Tree/DataGrid/预览占位）**

XAML 要点：
- `DockPanel` 顶部 `ToolBar`（Open/Save/SaveAs/Play/Pause/Restart）
- 中间 `Grid` 三列：
  - 左：`TreeView`（绑定 `States`/`Clips`）
  - 中：`DataGrid`（绑定选中 Layer 的 `Frames`）
  - 右：`WindowsFormsHost`（承载 WinForms 预览控件）

- [ ] **Step 2: code-behind 加载窗口并初始化控件树**

在 `.xaml.cs` 中：
- 创建 `EditorPreviewController` 并把它 attach 到 `WindowsFormsHost`
- 先提供一个 “空项目” 的默认模型（避免 null）

- [ ] **Step 3: 托盘入口**

在 `App.xaml.cs` 的托盘菜单增加 `ToolStripMenuItem("动画编辑器…")`：
- 点击后 `Dispatcher.Invoke` 打开/激活编辑器窗口（单例：如果已开则 `Activate()`）

- [ ] **Step 4: build 验证**

Run: `dotnet build .\AL1_S_Terminal.sln -c Debug`
Expected: SUCCESS

- [ ] **Step 5: Commit**

```bash
git add AL1_S_Terminal/App.xaml.cs AL1_S_Terminal/OverlayAnimations/Editor
git commit -m "feat: add overlay animation editor window shell"
```

---

## Task 2: EditorModels（绑定模型）+ 从 Config 映射到 UI

**Files:**
- Create: `AL1_S_Terminal/OverlayAnimations/Editor/EditorModels.cs`
- Modify: `AL1_S_Terminal/OverlayAnimations/Editor/OverlayAnimationEditorWindow.xaml.cs`

- [ ] **Step 1: 定义绑定模型**

最小集合（示意）：

```csharp
public sealed class EditorDocument {
  public string? FilePath { get; set; }
  public ObservableCollection<StateNode> States { get; } = new();
  public ObservableCollection<ClipNode> Clips { get; } = new();
  public string DefaultState { get; set; } = "";
}

public sealed class LayerNode {
  public string Name { get; set; } = "";
  public string ImageKey { get; set; } = "";
  public ObservableCollection<KeyframeRow> Frames { get; } = new();
}

public sealed class KeyframeRow {
  public int T { get; set; }
  public int X { get; set; }
  public int Y { get; set; }
  public double Opacity { get; set; }
  public double Scale { get; set; }
}
```

- [ ] **Step 2: 从 `OverlayAnimationConfig` 导入到 `EditorDocument`**

实现 `EditorDocument FromConfig(OverlayAnimationConfig cfg)`：
- 将 states/clips/layers/frames 映射到 ObservableCollection
- 关键帧按 `T` 排序

- [ ] **Step 3: 绑定 Tree/DataGrid**

在窗口中：
- Tree 的 SelectedItem 变化时，将中间 DataGrid 的 ItemsSource 指向该 Layer 的 Frames

- [ ] **Step 4: Commit**

```bash
git add AL1_S_Terminal/OverlayAnimations/Editor
git commit -m "feat: add editor binding models for overlay animation"
```

---

## Task 3: Open/Save/SaveAs（JSON）+ 默认目录

**Files:**
- Modify: `OverlayAnimationEditorWindow.xaml.cs`
- (Optional Modify): `EditorModels.cs`（增加 `ToConfig()`）

- [ ] **Step 1: Open（文件对话框）**

使用 `Microsoft.Win32.OpenFileDialog`：
- InitialDirectory：`Path.Combine(AppContext.BaseDirectory,"Assets","overlay_animations")`（若存在）
- Filter：`*.json`
- 打开后：`LoadFromFile` → `EditorDocument.FromConfig` → 重新绑定 UI → 调用预览刷新

- [ ] **Step 2: Save/SaveAs**

实现 `EditorDocument.ToConfig()`：
- 从 ObservableCollection 还原 `OverlayAnimationConfig`
- 在保存前对 frames 按 `T` 排序

保存：
- Save：若 `FilePath` 为空则转 SaveAs
- SaveAs：`SaveFileDialog`（默认目录同上）
- 写入：`OverlayAnimationConfigLoader.SaveToFile`

- [ ] **Step 3: 错误提示**

对 JSON/IO 错误用 `MessageBox.Show` 显示异常 Message（不显示堆栈）

- [ ] **Step 4: Commit**

```bash
git add AL1_S_Terminal/OverlayAnimations/Editor
git commit -m "feat: add open/save/save-as for overlay animation editor"
```

---

## Task 4: 实时预览（DispatcherTimer + debounce 重载）

**Files:**
- Create: `EditorPreviewController.cs`
- Modify: `OverlayAnimationEditorWindow.xaml.cs`

- [ ] **Step 1: 预览控制器实现**

职责：
- 创建 `OverlayImageAtlas(cfg.Images, baseDir)` 与 `OverlayAnimationControl(atlas)`
- 使用 `DispatcherTimer(16ms)` + `Stopwatch`：tick → `_animator.Sample(elapsedMs)` → `control.SetSnapshot(snapshot)`
- `LoadConfig(cfg, baseDir)`：dispose 旧 atlas/animator/control；重建；默认 `Play(cfg.DefaultState)`

- [ ] **Step 2: debounce**

在编辑器中：
- 监听 DataGrid 的 `CellEditEnding` / CollectionChanged 等事件
- 用 `DispatcherTimer`（200ms，单次）合并变更；触发时 `document.ToConfig()` → `preview.LoadConfig(...)`

- [ ] **Step 3: Play/Pause/Restart**

- Play：如果选中 state，调用 `preview.Play(stateName)`；否则默认 state
- Pause：停止预览 timer
- Restart：重置 stopwatch 并继续

- [ ] **Step 4: 手动验证**

Run: `dotnet run --project .\AL1_S_Terminal\AL1_S_Terminal.csproj -c Debug`
Expected: 托盘菜单出现 “动画编辑器…”；打开 `default.json`，修改关键帧后预览变化。

- [ ] **Step 5: Commit**

```bash
git add AL1_S_Terminal/OverlayAnimations/Editor
git commit -m "feat: add live preview for overlay animation editor"
```

---

## Self-Review

- Spec coverage：Open/SaveAs + Tree+Grid + Live preview + WinFormsHost 复用均有任务覆盖
- Placeholder scan：无 TBD/TODO
- 一致性：JSON 模型仍使用 `OverlayAnimationConfig`；预览复用 `OverlayAnimator`/`OverlayAnimationControl`

---

## Execution

该计划将用 Subagent-Driven Development 逐 Task 实现、每步两轮 review，直到编辑器 v1 可用。

