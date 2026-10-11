# SC2Uncensor (.NET)

星际争霸2 国服反和谐工具 —— **C# / .NET 8 实现**。

原版是 Python 写的（作者：水本）。本仓库是基于它重写的 C# 版本：沿用原有的机制与 `offsets.dat` 数据格式，代码按职责重新分层。当前仓库只保留这一套实现。

## 它做什么

国服客户端隐藏了「减少暴力表现」选项、并强制和谐，而未和谐的资源就在本地。本工具做三件事：

1. **NOP 三处 CHN 条件跳转** —— 解除选项隐藏与锁定
2. **写存储位与 R 三态** —— 让选项回到「未勾选 = 完整特效」的状态
3. **（自动模式）** 用 Windows 自带 OCR 找到「减少暴力表现」的勾选框并点击，触发游戏真正生效

纯内存读写：不改游戏任何文件、不联网、无遥测。改动只在本次游戏进程内有效，重启游戏需再执行一次。

## 环境要求

- Windows 10 1809+ 或 Windows 11（依赖 `Windows.Media.Ocr`，需安装**简体中文 OCR 语言包**）
- .NET 8 桌面运行时（或自行以 self-contained 方式发布）
- 若游戏以管理员身份运行，本工具也需要管理员权限

## 构建

在仓库根目录：

```powershell
dotnet build -c Release
dotnet test  -c Release
```

产物：

```
src\SC2Uncensor.App\bin\Release\net8.0-windows10.0.19041.0\SC2Uncensor.exe
```

本机（`E:\Proj` 环境约定）使用：

- SDK：`E:\SDK\dotnet-8`
- NuGet 包目录：`E:\Cache\nuget`（`nuget.config` 已固定）

## 快捷键

| 键 | 功能 |
|---|---|
| `F8` | 手动：只做内存部分，之后自己到 选项 > 画面设置 把「减少暴力表现」勾选 → 取消勾选各一次 |
| `F9` | 全自动：内存部分 + OCR 导航 + 自动点击两次 |
| `F11` | 自动切换和谐状态（开画面设置切换一次并提交，再按切回） |
| `Ctrl+Alt+1` | 热键总开关（暂停/恢复其他热键） |
| `Ctrl+Alt+2` | 显示当前状态（NOP / 存储位 / R） |
| `Ctrl+Alt+3` | 关闭反和谐（内存恢复，再手动点一次选项） |
| `Ctrl+Alt+4` | 关闭反和谐（内存恢复 + 自动操作） |

键位可在 `data/config.json` 的 `hotkeys` 段修改（重启工具生效）；被占用的键会自动退到 `Ctrl+Alt+原键`。检测到在对局内时会跳过自动点击，不干扰对局。

## 项目结构

```
SC2Uncensor.sln
Directory.Build.props          编译公共属性
nuget.config                   NuGet 包目录固定到 E:\Cache\nuget
data/
  offsets.dat                  各版本内存偏移（键 = exe 内嵌完整四段版本号）
  config.json                  运行配置
src/SC2Uncensor.Core/          核心库（net8.0-windows10.0.19041.0）
  Memory/                      ProcessMemory / OffsetTable / ExeVersion
  Capture/                     ScreenCapture（BitBlt + PrintWindow，蓝框聚类）
  Ocr/                         WinOcrEngine（进程内直调 Windows.Media.Ocr）
  Input/                       GameWindow / HotkeyLoop / KeyParser
  Navigation/                  DialogNavigator（菜单与画面页导航）
  UncensorService.cs           三条流程的门面
src/SC2Uncensor.App/           WPF 外壳
tests/SC2Uncensor.Core.Tests/  单元测试
```

## offsets.dat

```json
"5.0.15.98370": {
  "display": "5.0.17.98370",
  "nop_sites": [["0x11DA00", "742A"], ["0x11DA09", "7421"], ["0x11DA12", "7518"]],
  "store": "0x3E2E381",
  "r": "0x3E2B5A8"
}
```

- **键 = exe 内嵌完整四段版本号**，不是战网显示的号。已知映射：战网 `5.0.16.97579` ↔ exe `5.0.15.32043`；战网 `5.0.17.98370` ↔ exe `5.0.15.98370`。
- 版本核对是**精确匹配**，不使用 `5.0.15.*` 前缀。同主版本号下 NOP 点也可能平移：`5.0.15.98370` 相对 `5.0.15.32043` 后移了 `0x10`，拿旧偏移去写会把 `call` 打成半截，一点选项就卡死游戏进程。
- 读取版本必须用 `StringFileInfo` 里的 **FileVersion 字符串**。`VS_FIXEDFILEINFO` 的四个字段各只有 16 位，`98370 & 0xFFFF = 32834`，只读它会对不上表。
- 写入 NOP 前会校验该处原始字节；既不是预期原值也不是已 NOP 时**拒绝写入**，避免把代码写坏。

## 与原 Python 版的差异

本版是精简重写，只保留核心链路：内存补丁、WinOCR 自动点击、全局热键和一个小 WPF 窗口。

原 Python 版还有控制台版/图形版双入口、按键转发、Tk 皮肤、PowerShell OCR 回退等，这些没有搬过来。

## 免责与许可

- 仅支持 Windows；修改客户端违反暴雪 EULA，使用风险自负；本工具完全免费，请勿转卖。
- 许可：GPL-3.0（沿用原项目）。原 Python 实现作者：**水本**。
