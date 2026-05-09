# Overlay 动画编辑器（最小版）Design

**日期:** 2026-05-09  
**范围:** Task 8（编辑器入口 / 最小编辑器），在已完成的动画运行时（Animator/Atlas/RenderControl/Host/Overlay 接入）之上新增一个“可视化关键帧编辑器”。  

## Goal

提供一个最小但可用的动画编辑器，支持：
- 打开/另存为任意动画 JSON 文件（默认定位 `Assets/overlay_animations/`）
- 可视化编辑关键帧（Tree 选择 State/Clip/Layer + Grid 编辑关键帧表格）
- **实时预览**：边改边播，预览渲染复用现有 WinForms 控件 `OverlayAnimationControl`

## Non-Goals（v1 不做）

- 不做拖拽画布编辑（拖拽位置/缩放生成关键帧）  
- 不做复杂状态机图（Unity Animator 图）  
- 不做与主 Overlay 的热联动（保存后自动让 overlay 立即重载）——v1 可提示“需要手动重启/未来支持热重载”  
- 不做高性能极限优化（预览侧允许适度分配；后续 Task9/增量可优化）

## 约束与设计选择

用户确认：
- **编辑范围**：可视化关键帧编辑（Tree + DataGrid）
- **预览模式**：实时预览（Live）
- **文件行为**：打开/另存为任意 JSON（默认指向 Assets 目录）
- **预览渲染**：复用现有 WinForms 渲染控件（WPF 内用 `WindowsFormsHost` 承载）

## 高层架构（推荐方案 A）

### 组件

1) **WPF 编辑器窗口** `OverlayAnimationEditorWindow`  
- 左侧：TreeView（State / Clip / Layer）
- 中间：DataGrid（关键帧列表：t/x/y/opacity/scale）
- 右侧：预览面板（`WindowsFormsHost` + `OverlayAnimationControl`）
- 顶部工具栏：Open / Save / Save As / Play / Pause / Restart

2) **预览控制器**（editor 内部模块）  
职责：
- 从编辑器当前内存模型构建 `OverlayAnimationConfig`
- 创建/销毁预览运行时：`OverlayAnimator` + `OverlayImageAtlas` + `OverlayAnimationControl` + `Timer/Stopwatch`
- 接收编辑变更事件，做 debounce（例如 200ms）重建/刷新预览并重播

3) **序列化/反序列化**
- 复用现有 `OverlayAnimationConfigLoader`（允许注释与尾逗号）

### 数据流

- Open：JSON 文件 → `OverlayAnimationConfigLoader.LoadFromFile` → UI 模型（树/表格绑定）
- 编辑：UI 模型变更 → debounce → 生成 `OverlayAnimationConfig`（内存）→ 预览控制器刷新
- Save/Save As：UI 模型 → `OverlayAnimationConfigLoader.SaveToFile`

## UI 与交互（v1）

### Tree（选择上下文）

- 根节点：`States`、`Clips`（可折叠）
- `States/<StateName>`：显示 clip 名、loop
- `Clips/<ClipName>/Layers/<LayerName>`：选择后在 DataGrid 展示该 Layer 的关键帧

v1 简化规则：
- DataGrid 只编辑 **选中 Layer** 的 `Frames`
- 其他字段（`defaultState`、state→clip 映射、clip.durationMs、layer.imageKey）可先只读展示或在右侧属性面板简单编辑（v1 至少要能“看到”，是否可编辑由实现时决定）

### DataGrid（关键帧编辑）

列：
- `t`（int，毫秒）
- `x`（float 或 int，v1 先按现有模型字段类型）
- `y`
- `opacity`（0..1）
- `scale`（>0）

操作：
- 新增帧、删除帧
- 自动按 `t` 排序（在保存时排序；预览时也可排序以避免异常）

### 预览（Live）

行为：
- 用户修改表格任何单元格 → 200ms debounce → 预览重启播放
- 预览默认播放：`defaultState` 或当前选中的 State（优先：若用户选中 State，则播放该 State）
- Play/Pause/Restart：
  - Pause：停止预览 timer（保留最后一帧）
  - Restart：`PlayDefault` 或 `SetState(选中 state)` 并重置计时

### 错误处理

- JSON 不可解析：弹窗提示错误 + 保持窗口可用
- 图片路径不可加载：预览中该 layer 绘制跳过（atlas `TryGet` 返回 false / 或 loader 抛异常时提示一次）
- 保存失败（IO）：弹窗提示错误

## 线程与生命周期

- 编辑器运行在 WPF UI 线程。
- `WindowsFormsHost` 内的 `OverlayAnimationControl` 在同一线程创建与绘制。
- 预览 timer 选择：
  - 优先用 `DispatcherTimer`（WPF）驱动 `OverlayAnimator.Sample` 并调用 WinForms 控件 `SetSnapshot`（仍在 UI 线程）
  - 或沿用 WinForms `Timer`（但要确保线程一致）。v1 推荐 `DispatcherTimer`，减少跨 UI 框架的线程困惑。
- 关闭窗口时必须 `Dispose` atlas 与 WinForms 控件，停止计时器，避免 `Image` 句柄泄漏。

## 文件与路径约定

- Open/Save 默认目录：`<AppContext.BaseDirectory>/Assets/overlay_animations/`（运行目录下）
- 允许用户打开任意路径的 JSON；图片路径仍以 JSON `images` 字典中的相对路径 + baseDir 组合（使用 atlas 的路径约束策略）

## 测试策略（v1）

编辑器 UI 本身不做 UI 自动化测试（成本高）。
用单元测试覆盖：
- JSON round-trip 已在 Task3 覆盖
- animator 插值/loop 已在 Task2 覆盖
- atlas 缓存与路径安全已在 Task4 覆盖

手动验证清单（实现后）：
- 打开 `default.json` → Tree/表格显示正确 → 修改某帧 x/y/opacity → 预览立即变化
- Save As 到新文件 → 重新打开 → 内容一致
- 图片缺失时预览不崩溃

## 规格自检（占位/一致性/范围）

- 无 TBD/TODO 占位
- v1 范围明确：Tree + Grid + Live Preview + Open/Save
- 明确复用 WinForms 渲染控件，并在 WPF 中使用 `WindowsFormsHost`

