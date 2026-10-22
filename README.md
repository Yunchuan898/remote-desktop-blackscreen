# 远程黑屏助手 · Remote Desktop Black Screen

> 远程控制电脑时，**本机显示器自动变黑**，站在你电脑前的人看不到你在远程操作什么；而远程端的你，画面完全不受影响。

[![Platform](https://img.shields.io/badge/platform-Windows%2010%202004%2B%20%7C%2011-0078D6?logo=windows&logoColor=white)](#系统要求)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.x-512BD4?logo=dotnet&logoColor=white)](#系统要求)
[![Single File](https://img.shields.io/badge/single%20exe-no%20install-2ea44f)](#快速开始)
[![License: MIT](https://img.shields.io/badge/license-MIT-2ea44f)](LICENSE)

---

## 它解决什么问题

用向日葵、ToDesk、RustDesk 等工具远程控制电脑时，**本机屏幕会把远端操作的一切完整显示出来**——办公室、宿舍、家里有人的场景下，屏幕内容等于直接暴露。

这个工具在远控连接期间把本机屏幕黑掉（或直接关掉显示器），远控软件抓到的画面仍然正常，所以你该怎么操作还怎么操作。

> ⚠️ **注意方向：黑的是「你面前这台机器的显示器」，不是远端画面。**

```
本机显示器        →  ██████ 全屏黑幕 ██████   （只有本机看到）
远程端看到的画面  →  🖥️ 正常桌面                （抓屏排除生效）
```

## 功能

- **自动检测远控连接**：按进程或窗口标题/类名判断，连上自动黑屏，断开自动恢复
- **两种黑屏方式**：全屏黑幕（推荐）/ 关闭显示器，按远控工具兼容性任选
- **远程端可见的控制面板**：远控端能看见当前状态并一键恢复，不会把自己锁死
- **全局热键**：默认 `Ctrl+Alt+B`，黑屏状态下依然有效
- **学习模式（窗口侦探）**：不知道目标窗口标题时，自动把远控进程的窗口信息记录到日志
- **托盘常驻 / 开机自启 / 单实例 / 免安装**

## 快速开始

### 编译（Windows 10/11 自带 .NET Framework 4.x，无需安装任何 SDK）

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

几秒后生成 `BlackScreenHelper.exe`，双击运行即可。首次运行会在 exe 同目录生成 `BlackScreenHelper.config`，并在屏幕右上角显示控制面板、托盘出现图标。

> 建议把 exe 放在非受保护目录（如 `D:\Tools\`），避免放在 `C:\Program Files` 下导致配置文件无法写入。

### 系统要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 **2004 (build 19041)** 及以上 / Windows 11 |
| 运行时 | .NET Framework 4.x（系统自带） |
| 权限 | 普通用户即可（开机自启写 HKCU） |

> 低于 Windows 10 2004 的系统不支持「抓屏排除」，覆盖层方式会让远端也看到黑屏，只能改用「关闭显示器」方式。

## 使用方式

启动后有三处入口：

1. **控制面板**（右上角，置顶）：显示状态，一个按钮切换黑屏/恢复。这个窗口**不参与抓屏排除**，所以远控端能看见它。
2. **托盘图标**：双击 = 切换黑屏；右键菜单可切换检测模式、黑屏方式、学习模式、开机自启、打开配置、退出。
3. **全局热键**：默认 `Ctrl+Alt+B`，可在配置文件中修改。

黑屏后恢复的四种方式：控制面板按钮 → 热键 → 托盘双击 → 托盘菜单「黑屏 / 恢复」。

## 检测模式

| 模式 | 判定依据 | 适用场景 |
| --- | --- | --- |
| `Manual` 手动 | 不自动检测，只能手动切换 | 只需要一个热键开关 |
| `Process` 进程 | 监视列表中的进程是否存在 | 最稳定。默认已包含 `SunloginClient.exe`、`ToDesk.exe` |
| `Window` 窗口 | 匹配可见窗口的**标题**或**类名**关键字 | 进程常驻但只在连接时弹窗的远控工具 |

- 进程名匹配**忽略 `.exe` 后缀和大小写**（`SunloginClient.exe` 与 `sunloginclient` 等价）。
- 窗口关键字是**子串匹配、不区分大小写**；标题关键字和类名关键字命中任意一个即触发。
- 检测每 **1.5 秒**轮询一次。
- **手动接管**：一旦你手动切换过黑屏状态，自动检测会暂停，托盘提示 `(手动接管)`；在托盘菜单里**重新选择一次检测模式**即可恢复自动检测。

## 黑屏方式

### Overlay 覆盖层（默认，推荐）

创建一个覆盖整个虚拟屏幕、置顶、鼠标穿透、不抢焦点的黑色窗口，并调用
`SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` 把它标记为「不参与屏幕捕获」。

- 本机显示器：纯黑
- 远控软件抓屏：过滤掉这一层，看到的是正常桌面

程序每秒会把覆盖层重新置顶一次，防止被其他窗口压住；无法被 `Alt+F4` 关闭。

### MonitorOff 关闭显示器

向所有窗口广播 `WM_SYSCOMMAND / SC_MONITORPOWER`，并每秒重复一次保持关闭。

- 更省电，物理上真的关掉背光
- 对使用**自带显示驱动**的远控工具更可靠
- 部分显示器/显卡组合唤醒时会有 1–2 秒闪烁

### 兼容性

常见远控工具（向日葵、ToDesk、RustDesk、mstsc 等）通常基于 Desktop Duplication API 抓屏，覆盖层一般能被正确排除。少数自带采集驱动的远控可能仍然把黑幕抓走——具体以你机器上的实际测试为准，遇到这种情况换用 `MonitorOff` 方式。

## 配置文件

`BlackScreenHelper.config` 与 exe 同目录，程序退出后修改，重启生效（托盘菜单里的开关会自动写回这个文件）。

```ini
# 检测模式：Manual / Process / Window
Mode=Process

# 黑屏方式：Overlay(覆盖层) / MonitorOff(关闭显示器)
BlackScreenMethod=Overlay

# 监视的进程名，可写多行
Process=SunloginClient.exe
Process=ToDesk.exe
Process=RustDesk.exe

# 窗口标题关键字，可写多行（Window 模式用）
WindowKeyword=远程控制

# 窗口类名关键字，可写多行（Window 模式用）
WindowClass=

# 开机自启：1 / 0
AutoStart=0

# 学习模式（窗口侦探）：1 / 0
LearnMode=0

# 全局热键
Hotkey=Control+Alt+B
```

| 键名 | 取值 | 说明 |
| --- | --- | --- |
| `Mode` | `Manual` / `Process` / `Window` | 检测模式，不区分大小写 |
| `BlackScreenMethod` | `Overlay` / `MonitorOff` | 黑屏方式 |
| `Process` | 进程名 | 可多行；忽略 `.exe` 与大小写 |
| `WindowKeyword` | 文本 | 可多行；窗口标题子串匹配 |
| `WindowClass` | 文本 | 可多行；窗口类名子串匹配 |
| `AutoStart` | `1` / `0` | 写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| `LearnMode` | `1` / `0` | 见下文 |
| `Hotkey` | 见下 | 修饰键 `Control`/`Ctrl`、`Alt`、`Shift`、`Win`，主键为字母、数字或 `F1`–`F24`，用 `+` 连接 |

以 `#` 或 `;` 开头的行是注释；空行忽略。**注意：通过托盘菜单修改设置时，程序会重写整个配置文件，你手写的注释会丢失**，建议手改配置时先退出程序。

## 学习模式（窗口侦探）

有些远控工具的窗口标题会变，或者进程常驻但窗口类名不好猜。用学习模式自动找出来：

1. 在配置里把 `Process=` 填上目标远控的进程名
2. 托盘菜单勾选「学习模式」
3. 正常发起一次远程连接，让远控窗口出现
4. 查看 exe 同目录下的 `BlackScreenHelper.log`，会记录类似：

   ```
   2026-10-22 14:03:11  HWND=0x00120A3C Title="向日葵远程控制" Class="SunloginWindowClass"
   ```

5. 把 `Title` 里的稳定片段填进 `WindowKeyword=`，或把 `Class` 填进 `WindowClass=`
6. 改成 `Mode=Window`，关掉学习模式

学习模式只记录**属于 `Process=` 列表内进程**的可见窗口，日志按会话去重。

## 常见问题

**Q：黑屏后，我自己（远端）也看不见了？**

说明你的远控工具没有走标准抓屏接口，覆盖层被一起抓走了。解决：把 `BlackScreenMethod` 改成 `MonitorOff`；或确认系统是 Windows 10 2004 及以上。

**Q：黑屏了怎么恢复？会不会锁死？**

不会。控制面板在远端可见且可点击；热键全局有效；托盘双击也可以。即使面板被误关，托盘菜单里还有「显示控制面板」。

**Q：杀毒软件报毒？**

程序会注册全局热键、写注册表自启项、调用 `SetWindowDisplayAffinity`、全屏置顶，这些行为容易触发启发式误报。仓库提供完整源码和 `build.ps1`，可自行编译核对。

**Q：热键没反应？**

大概率热键被其他软件占用。换一个组合（如 `Control+Alt+F9`），或先退出程序重开。

**Q：控制面板找不到了？**

托盘右键 →「显示控制面板」。面板固定显示在主屏右上角，若中途改了分辨率，重启程序即可回到可见位置。

**Q：提示「已经在运行」？**

程序是单实例的，去托盘找已经运行的实例。要重启请先右键退出。

**Q：怎么彻底删除？**

托盘退出 → 删除 exe 和配置文件 → 如果开过自启，删掉注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `BlackScreenHelper` 值（或先关掉自启再退出）。

## 已知限制

- 只影响**本机显示器**，无法阻止远端看到画面，也不是防偷拍/防录屏的安全产品
- 覆盖层方式依赖 `WDA_EXCLUDEFROMCAPTURE`，需要 Windows 10 2004+
- 多显示器：覆盖层覆盖整个虚拟屏幕（所有显示器一起黑）；`MonitorOff` 同样作用于所有显示器
- 不支持远程会话（RDP 的独立 session）中区分本地/远程，判断依据是进程或窗口是否存在
- 少数使用私有采集驱动的远控软件无法被覆盖层绕过

## 项目结构

```
BlackScreenHelper.cs       单文件主程序（WinForms + P/Invoke，无第三方依赖）
BlackScreenHelper.config   配置文件（首次运行自动生成）
build.ps1                  一键编译脚本（调用系统自带 csc.exe）
```

## 工作原理

- 单文件 C# WinForms，`/target:winexe` 编译，无外部依赖
- 1.5 秒轮询进程列表或枚举可见窗口，判定远控是否在线
- 覆盖层：`WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE`
- 抓屏排除：`SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`
- 控制面板使用 `WS_EX_NOACTIVATE`，点击不会激活窗口、不会被覆盖层挡住
- 热键：`RegisterHotKey` + 隐藏消息窗口
- 控制面板/覆盖层的关闭按钮被拦截，只能由程序自己隐藏

## Keywords

remote desktop black screen · privacy screen · hide local screen during remote session ·
remote control privacy · screen capture exclusion · WDA_EXCLUDEFROMCAPTURE ·
Sunlogin / ToDesk / RustDesk · Windows tray app · C# WinForms ·
远程桌面黑屏 · 远程控制防偷窥 · 向日葵黑屏 · ToDesk 黑屏 · 远程时屏幕变黑

## 许可证

[MIT](LICENSE) © 2026 yccx030827

---

本项目为个人效率工具，欢迎 issue / PR。
