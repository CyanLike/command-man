# Command Man

Command Man 是一个 PowerToys Run 插件，用于快速查询并复制 Windows、Linux 和 Git 常用命令。插件内置 68 条中文精选命令，并打包了 6,900+ 条 TLDR Pages 离线命令数据，无需在检索时联网。

![Command Man 图标](assets/icon.light.svg)

## 功能

- 搜索 Windows CMD、PowerShell、Linux 和 Git 命令
- 输入参数后精确筛选对应示例，并自动进入完整模式
- `simple` / `full` 两种显示模式，也可使用 `常用` / `完整`
- 使用 `windows`、`linux`、`git` 过滤命令类别
- 按 `Enter` 直接复制选中的命令
- 按 `Ctrl+Enter` 在常用和完整用法间切换
- 在 PowerToys 插件设置中配置默认显示模式
- 支持 x64 和 ARM64

## 使用

按 `Alt+Space` 打开 PowerToys Run，输入默认激活关键字 `cmd`：

```text
cmd git commit
cmd full git rebase
cmd simple linux curl
cmd windows 端口
cmd 完整 powershell 进程
cmd ll
cmd ll -a
cmd git commit --amend
```

查询语法为：

```text
cmd [simple|full] [windows|linux|git] [命令或关键词]
```

模式和平台可以省略；两种筛选词的先后不限，但应放在搜索词之前。PowerToys 设置中的插件管理器允许修改 `cmd` 激活关键字。

当查询不含参数时，默认显示少量常用示例；出现 `-a`、`--amend`、`/all` 等参数时，会自动使用完整模式，并仅显示匹配该参数（包括短参数组合）的示例。例如 `ll -a` 会把 `ll` 解析为 `ls` 的别名，并优先显示 `ll -a` 的完整说明。

查询 `adb`、`docker` 等父命令时，精确命中的主页面排在最前，并继续列出以空格分隔的层级子命令，例如 `adb shell` 和 `adb devices`；不会把 `ls` 错误扩展到 `lsof`。使用 `full adb` 可显示主页面的全部示例以及相关子命令用法。

检索器会在进程内共享一次构建的离线索引：命令名与别名使用精确索引，前缀查询使用排序索引和二分定位，命令参数在加载时预解析，最近 128 次查询使用有界缓存。只有无法匹配命令名或别名前缀的说明关键词（例如中文语义查询）才会回退到全文检索。

选中结果后：

- `Enter`：复制命令并关闭 PowerToys Run
- `Ctrl+C`：通过上下文操作复制命令
- `Ctrl+Enter`：切换该命令的常用/完整用法

## 构建

要求：

- Windows 10 22H2 或 Windows 11
- .NET 10 SDK
- PowerShell 7 或 Windows PowerShell 5.1

构建 x64 插件：

```powershell
.\scripts\build.ps1 -Architecture x64
```

构建 ARM64 插件：

```powershell
.\scripts\build.ps1 -Architecture ARM64
```

脚本会还原依赖、运行测试，并生成：

```text
artifacts/CommandMan-x64.zip
artifacts/x64/CommandMan/
```

项目按照 PowerToys 当前插件清单目标使用 `net10.0-windows10.0.22621.0`。`Wox.Plugin` 等宿主引用来自 `Community.PowerToys.Run.Plugin.Dependencies`；发布目录不会携带宿主自己的 DLL。

运行检索微基准：

```powershell
dotnet run --project .\tools\benchmark_search\CommandMan.SearchBenchmark.csproj -c Release
```

## 安装

先完全退出 PowerToys，再运行：

```powershell
.\scripts\install.ps1 -Architecture x64
```

插件会安装到：

```text
%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\CommandMan
```

重新启动 PowerToys 后，在“PowerToys Run → 插件”中确认 Command Man 已启用。若尚未安装 PowerToys，可先通过 `winget install Microsoft.PowerToys` 安装。

## 扩充命令库

命令数据位于：

- `src/CommandMan.Core/Data/windows.json`
- `src/CommandMan.Core/Data/linux.json`
- `src/CommandMan.Core/Data/git.json`
- `src/CommandMan.Core/Data/tldr.json`（由官方 TLDR Pages 数据生成）

每个命令必须包含唯一 `id`、名称、平台、说明，以及至少一条 `simple` 和 `full` 示例。`CommandCatalog` 在加载时会校验这些约束，测试也会验证全部平台和用法是否齐全。

更新本地 TLDR 数据：

```powershell
.\scripts\update-tldr.ps1 -Python python
```

脚本会在 `.cache/tldr` 维护一个可复用的浅克隆，只检出 tldr-pages 官方仓库最新 `main` 分支中的英文全集 `pages` 与简体中文翻译 `pages.zh`；后续更新只需获取新增提交。英文页面提供完整覆盖，中文页面按命令和示例覆盖可用的说明，之后生成适合插件快速加载的紧凑 JSON 索引。精确上游提交及页面数量记录在 `third_party/tldr/SOURCE.json`；许可证位于 `third_party/tldr/LICENSE.md` 和 `THIRD_PARTY_NOTICES.md`。

网络不可用但已有缓存时，可以运行 `./scripts/update-tldr.ps1 -Offline`，从缓存的精确提交重新生成索引；默认模式仍会先获取官方仓库的最新提交。

## 项目结构

```text
src/CommandMan.Core/                         搜索、解析和离线命令库
src/Community.PowerToys.Run.Plugin.CommandMan/ PowerToys Run 适配、复制和设置
tests/CommandMan.Tests/                      核心与插件测试
scripts/                                    构建、打包和安装脚本
```

## 兼容性说明

PowerToys Run 的插件 API 随 PowerToys 更新。若升级 PowerToys 后插件无法加载，请把项目中的 `Community.PowerToys.Run.Plugin.Dependencies` 更新到与已安装 PowerToys 相匹配的版本，然后重新构建。
