![NVIDIA Profile Inspector 主窗口](npi_screenshot.png)

# NVIDIA Profile Inspector（中文汉化版）

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)](#系统要求)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4.svg)](#从源码构建)

NVIDIA Profile Inspector 是一款 Windows 工具，用于通过 NVIDIA 驱动设置 API 编辑
NVIDIA 驱动配置文件。它可以查看 NVIDIA 控制面板所使用的驱动配置文件数据库，开放大量
隐藏或未公开的配置项，并支持按应用程序创建、调整、导出、导入和还原配置文件。

本工具面向需要精确控制游戏和应用程序配置文件的高级用户，可用于调整兼容性标志、
帧率限制器、同步选项、纹理过滤、SLI 设置、Optimus 行为、DLSS 覆盖以及其他在
标准控制面板中不总是可用的驱动级开关。

## 主要功能

- 查看和编辑全局及按应用程序的 NVIDIA 驱动配置文件。
- 添加或移除配置文件所关联的应用程序。
- 为驱动数据库中缺失的游戏或应用程序创建自定义配置文件。
- 快速搜索和筛选配置文件设置。
- 在通用、已知和扩展驱动设置之间切换。
- 将常用设置标记为收藏。
- 使用内置的位编辑器查看和编辑位掩码型设置。
- 导出和导入 `.nip` 配置文件备份。
- 导出和导入 NVIDIA 文本格式的配置文件转储。
- 还原单个设置、整个配置文件或驱动默认值。
- 在已安装的 NVIDIA 驱动支持的情况下，使用 DLSS、DLSS 光线重建和 DLSS 帧生成覆盖项。
- 提供多种界面主题，可选 Windows 11 背景效果。
- 在应用内检查 GitHub 发布版本。

## 下载

请从 [GitHub 发布页面](https://github.com/Orbmu2k/nvidiaProfileInspector/releases)
下载最新的官方构建。发布实验性构建时，同一页面也会提供预发布版本：
[GitHub 预发布](https://github.com/Orbmu2k/nvidiaProfileInspector/releases?q=prerelease%3Atrue)。

发布压缩包中包含可执行文件及其运行所需的文件。将压缩包解压到可写的文件夹，
然后启动 `nvidiaProfileInspector.exe`。

## 系统要求

- 已安装兼容 NVIDIA 显卡驱动程序的 Windows 系统。
- 已安装驱动程序所支持的 NVIDIA GPU。
- .NET Framework 4.8。
- 管理员权限。NVIDIA Profile Inspector 必须以提升权限启动，才能写入 NVIDIA
  驱动配置文件数据库。

## 快速上手

1. 启动 `nvidiaProfileInspector.exe`。
2. 在配置文件框中选择一个配置文件，或输入游戏/应用程序的配置文件名称并按回车，
   查找对应的配置文件。
3. 使用搜索框或设置模式选择器找到要编辑的设置。
4. 在设置列表中修改数值。
5. 点击**应用更改**，将配置文件更改写入 NVIDIA 驱动数据库。

在尝试高级设置之前，建议先导出当前配置文件或所有自定义配置文件，
以便之后恢复到已知良好的状态。

## 配置文件备份

NVIDIA Profile Inspector 可以将配置文件更改保存为 `.nip` 文件。这适用于分享单个
游戏配置文件、保存个人预设，或在更新驱动之前备份已修改的驱动配置文件。

推荐的备份流程：

1. 选择要保留的配置文件。
2. 对单个配置文件使用**导出**，或在导出菜单中导出所有自定义配置文件。
3. 将生成的 `.nip` 文件保存在驱动安装文件夹之外的位置。
4. 需要时使用**导入**来还原或合并配置文件。

## 高级设置须知

NVIDIA Profile Inspector 暴露的许多条目是隐藏的、实验性的、已弃用的、
依赖特定驱动版本的，或 NVIDIA 未公开文档的。对某个游戏、驱动版本或 GPU
架构有效的数值，在其他环境下可能无效甚至引发问题。

请尽量使用配置文件级别的修改、保留备份，并在重置整个驱动数据库之前，
优先尝试还原单个设置或单个配置文件。

## 命令行

NVIDIA Profile Inspector 支持少量用于导入、导出和维护任务的启动参数。

```powershell
nvidiaProfileInspector.exe [选项] [profile1.nip] [profile2.nip ...]
```

| 选项 | 说明 |
| --- | --- |
| `<文件>.nip` | 导入一个或多个 `.nip` 配置文件。如果程序已在运行，文件会被转发给现有实例。 |
| `-silentImport` | 导入 `.nip` 文件时不显示常规的成功对话框。 |
| `-silent` | `-silentImport` 的别名。 |
| `-exportCustomized` | 将所有自定义配置文件导出到可执行文件旁带时间戳的 `.nip` 文件，然后退出。 |
| `-createCSN` | 将内嵌的 `CustomSettingNames.xml` 写入当前工作目录，然后退出。 |
| `-showOnlyCSN` | 启动界面时仅显示自定义设置。 |
| `-disableScan` | 启动界面时不执行初始配置文件扫描。 |

示例：

```powershell
nvidiaProfileInspector.exe ".\MyProfile.nip"
nvidiaProfileInspector.exe -silentImport ".\ProfileA.nip" ".\ProfileB.nip"
nvidiaProfileInspector.exe -exportCustomized
```

## 从源码构建

本项目是面向 .NET Framework 4.8 的 WPF 桌面应用程序。

```powershell
git clone https://github.com/Orbmu2k/nvidiaProfileInspector.git
cd nvidiaProfileInspector
dotnet build .\nvidiaProfileInspector\nvidiaProfileInspector.sln -c Release
```

编译输出的应用程序位于：

```text
nvidiaProfileInspector\bin\Release\net48\
```

也可以直接用 Visual Studio 打开 `nvidiaProfileInspector\nvidiaProfileInspector.sln`。

## 项目结构

```text
nvidiaProfileInspector/
+-- Common/                 驱动配置文件服务、元数据、导入/导出逻辑
+-- Native/                 NVAPI 与 Win32 互操作
+-- Services/               应用程序服务（主题、更新检查等）
+-- UI/                     WPF 视图、视图模型、控件、样式与主题
+-- CustomSettingNames.xml  已知设置的用户友好名称与描述
`-- Reference.xml           复制到输出文件夹的参考设置元数据
```

## 相关资源

- [GitHub 发布](https://github.com/Orbmu2k/nvidiaProfileInspector/releases)
- [问题与功能建议](https://github.com/Orbmu2k/nvidiaProfileInspector/issues)
- [STEP Project NVIDIA Inspector 指南](https://wiki.step-project.com/Guide:NVIDIA_Inspector)
- [PCGamingWiki: Nvidia Profile Inspector](https://www.pcgamingwiki.com/wiki/Nvidia_Profile_Inspector)

## 参与贡献

欢迎提交缺陷报告、设置更新、兼容性说明和拉取请求。报告问题时，请附上
NVIDIA 驱动版本、Windows 版本、GPU 型号、受影响的配置文件或可执行文件，
以及复现步骤。

## 许可证

NVIDIA Profile Inspector 以 [MIT 许可证](LICENSE) 发布。

## 汉化说明

本版本为社区汉化版，在原版基础上对全部界面、设置名称、取值名称和描述进行了
中文本地化，详见 [汉化说明.md](汉化说明.md)。原项目版权归原作者所有，
按 MIT 许可证发布。
