# AL1_S_Terminal

在 **Windows 终端（Windows Terminal）** 窗口一角显示透明分层动画的**终端桌宠**。程序在后台运行，通过托盘图标管理；检测到已打开的 Windows Terminal 后，会在终端内容区域左下或右下叠加桌宠窗口，并根据前台焦点与键盘输入在 **Idle（空闲）** 与 **Typing（输入中）** 等动画状态间切换。

## 运行环境

- **操作系统**：Windows 10 / 11（x64）
- **运行时**：与工程目标框架一致（当前为 **.NET 10**，Windows 桌面）
- **终端**：需安装并运行 **Windows Terminal**（`WindowsTerminal.exe`）。本程序通过窗口类名等方式查找终端主窗口，**不**支持将桌宠挂到传统 `conhost.exe` 控制台窗口。

## 获取与运行

### 从源码构建

在仓库根目录（包含 `AL1_S_Terminal.sln`）执行：

```powershell
dotnet build .\AL1_S_Terminal.sln -c Release
```

生成的可执行文件位于：

`AL1_S_Terminal\bin\Release\net10.0-windows\AL1_S_Terminal.exe`

（Debug 构建则对应 `bin\Debug\...`。）

### 日常使用

1. 启动 **AL1_S_Terminal.exe**（可放入开机启动项，按需自行配置）。
2. 打开 **Windows Terminal** 并保持至少一个可见终端窗口。
3. 桌宠会出现在该终端窗口的 **左下角或右下角**（见下文「显示位置」）。
4. 当 **Windows Terminal 处于前台** 且检测到你在相关上下文中输入时，会尝试切换到 **Typing** 状态；否则为 **Idle**（具体状态名由动画包 `key.json` 定义，名称需与内置策略一致，见「动画与桌宠包」）。

主窗口在设计上会隐藏，**请通过任务栏通知区域的托盘图标**使用本程序。

## 托盘菜单说明

| 菜单项 | 说明 |
|--------|------|
| **动画编辑器…** | 打开内置编辑器，用于编辑 `.alice` 动画包（预览、导出等）。 |
| **Overlay 位置** | 选择桌宠在终端窗口上的 **左下角** 或 **右下角**。右下角时，分层内容会 **水平镜像**，使角色朝向与左侧一致。 |
| **退出** | 结束进程。 |

（Debug 构建下可能额外提供与开发调试相关的菜单项，正式使用可忽略。）

## 配置说明

### 1. 显示位置（持久化）

通过托盘 **「Overlay 位置」** 切换后，设置会写入本机用户目录下的 JSON 文件：

`%LocalAppData%\AL1_S_Terminal\overlay_display.json`

内容为 `corner` 字段，对应枚举 **`0` = 左下角**、**`1` = 右下角**（JSON 数字）。若需备份或迁移偏好，可复制该文件；删除后重启程序将恢复默认（左下角）。

### 2. 桌宠外观与动画包（`.alice`）

运行时默认加载与 exe 同目录下的：

`Assets\overlay_animations\Default.alice`

- **`.alice` 文件**本质为 **ZIP** 压缩包，根目录需包含 **`key.json`**（动画配置）及 **`assets/`** 等资源目录（图片路径在 `key.json` 中相对包根填写，例如 `assets/idle.png`）。
- 替换桌宠：在 **程序退出** 后，用你自己的包覆盖上述路径下的 **`Default.alice`**（保持文件名不变），或自行修改工程中的复制规则与 `TerminalOverlayForm` 中的加载路径（高级用法）。
- 工程文件中已将 `Assets\overlay_animations\**/*.alice` 设为 **复制到输出目录**，因此也可在源码树 `AL1_S_Terminal\Assets\overlay_animations\` 下放置 `Default.alice` 后重新构建，便于分发。

### 3. 动画状态与自动切换逻辑

程序根据「终端是否在前台」和「近期是否有符合条件的按键」决定目标逻辑状态，并调用动画控制器切换到配置中的 **状态名**：

- **`Idle`**：终端非前台，或前台但未处于「正在输入」判定窗口内。
- **`Typing`**：终端在前台且检测到近期键盘活动。

因此 **`key.json` 中的 `states` 应至少包含名为 `Idle` 与 `Typing` 的状态**（或与代码中常量一致的状态名），否则自动切换可能无法正确显示对应动画。

使用 **「动画编辑器…」** 可编辑并导出符合上述结构的包，再替换 `Default.alice` 即可。

### 4. 高 DPI

程序在启动时会将 WinForms 设为 **PerMonitorV2** 高 DPI 模式，以便桌宠尺寸与终端缩放一致、避免叠加窗口被错误缩放。

## 隐私与权限说明

- 为检测「在终端中输入」，程序可能注册 **底层键盘钩子**（`WH_KEYBOARD_LL`），仅在判定与 **当前附加的 Windows Terminal 窗口子树** 相关时记录活动时间，用于 **Typing** 状态；不用于记录具体按键内容。
- 若钩子创建失败（权限或环境限制），**Typing** 相关行为可能不可用，桌宠仍可能显示 **Idle** 等状态。

## 解决方案结构（简要）

- **`AL1_S_Terminal`**：WPF 壳层、托盘、主循环同步终端与叠加窗、动画协调与编辑器 UI。
- **`AL1_S_Terminal.Win32`**：查找 Windows Terminal、计算叠加位置等互操作逻辑。

## 许可证

本项目以 [MIT 许可证](LICENSE) 发布。
