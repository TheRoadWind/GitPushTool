# GitPushTool

<img src="docs/app.png" width="120" alt="应用图标" />

> 由 GitPushTool 自动生成

---

## 📖 简介

用于项目快速推送至Git仓库

## ✨ 特性

- 支持显示用户已创建仓库
- 支持仓库管理
- 版本号自动读取（支持 VB/C# 工程、bin\Release / bin\Debug 编译产物 exe/dll、apk/aab、Node、Maven、Gradle、Flutter、Rust、Python 等，只读不可修改）
- 打 tag 时保留 Git 仓库原有 tag
- 推送时自动更新 README 里的版本号、目录结构与运行时
- 自动导入程序图标到 README（GitHub 上直接可见，ico 自动转 PNG）
- 内置 FastGithub 加速（源码已并入 `FastGithubSrc/`，项目引用 + 进程内调用，**启动时自动启用**）
- 启动时自动打开原项目的加速窗口（流量曲线 / UDP 日志 / 问答），可实时查看加速状态

## 🛠️ 环境要求

- 操作系统：Windows 10 / 11
- 运行时：.NET 10.0（Windows）
- 权限：**需要管理员**（清单 `requireAdministrator`），因为内置加速涉及 WinDivert 驱动、安装自签 CA、修改 hosts / 代理设置

## 🚀 快速开始

```bash
git clone https://github.com/TheRoadWind/GitPushTool.git
cd GitPushTool
```

## 📁 目录结构

```
GitPushTool/
├── docs/
│   └── app.png
├── FastGithubSrc/
│   ├── @dnscrypt-proxy/
│   │   ├── linux-arm64/
│   │   ├── linux-x64/
│   │   ├── osx-arm64/
│   │   ├── osx-x64/
│   │   ├── win-x64/
│   │   ├── dnscrypt-proxy.toml
│   │   └── LICENSE
│   ├── FastGithub/
│   │   ├── appsettings/
│   │   ├── Properties/
│   │   ├── app.manifest
│   │   ├── AppHostedService.cs
│   │   ├── AppOptions.cs
│   │   ├── appsettings.json
│   │   ├── ConsoleUtil.cs
│   │   ├── FastGithub.csproj
│   │   ├── FastGithubRunner.cs
│   │   ├── ProductionVersion.cs
│   │   ├── Program.cs
│   │   ├── ServiceExtensions.cs
│   │   └── …（其余 1 项未列出）
│   ├── FastGithub.Configuration/
│   │   ├── DomainConfig.cs
│   │   ├── DomainPattern.cs
│   │   ├── FastGithub.Configuration.csproj
│   │   ├── FastGithubConfig.cs
│   │   ├── FastGithubException.cs
│   │   ├── FastGithubOptions.cs
│   │   ├── GlobalListener.cs
│   │   ├── LoggerExtensions.cs
│   │   ├── ResponseConfig.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── TlsSniPattern.cs
│   │   └── TypeConverterBinder.cs
│   ├── FastGithub.DomainResolve/
│   │   ├── DnsClient.cs
│   │   ├── DnscryptProxy.cs
│   │   ├── DomainResolveHostedService.cs
│   │   ├── DomainResolver.cs
│   │   ├── FastGithub.DomainResolve.csproj
│   │   ├── IDomainResolver.cs
│   │   ├── IPAddressService.cs
│   │   ├── PersistenceService.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   ├── ServiceInstallUtil.cs
│   │   └── TomlUtil.cs
│   ├── FastGithub.FlowAnalyze/
│   │   ├── DelegatingDuplexPipe.cs
│   │   ├── DelegatingStream.cs
│   │   ├── DuplexPipeStreamExtensions.cs
│   │   ├── FastGithub.FlowAnalyze.csproj
│   │   ├── FlowAnalyzeDuplexPipe.cs
│   │   ├── FlowAnalyzer.cs
│   │   ├── FlowAnalyzeStream.cs
│   │   ├── FlowStatistics.cs
│   │   ├── FlowStatisticsContext.cs
│   │   ├── FlowType.cs
│   │   ├── IFlowAnalyzer.cs
│   │   ├── ListenOptionsExtensions.cs
│   │   └── …（其余 2 项未列出）
│   ├── FastGithub.Http/
│   │   ├── FastGithub.Http.csproj
│   │   ├── HttpClient.cs
│   │   ├── HttpClientFactory.cs
│   │   ├── HttpClientHandler.cs
│   │   ├── HttpConnectTimeoutException.cs
│   │   ├── IHttpClientFactory.cs
│   │   ├── LifetimeHttpHandler.cs
│   │   ├── LifetimeHttpHandlerCleaner.cs
│   │   ├── LifeTimeKey.cs
│   │   ├── RequestContext.cs
│   │   ├── RequestContextExtensions.cs
│   │   └── ServiceCollectionExtensions.cs
│   ├── FastGithub.HttpServer/
│   │   ├── Certs/
│   │   ├── HttpMiddlewares/
│   │   ├── TcpMiddlewares/
│   │   ├── TlsMiddlewares/
│   │   ├── ApplicationBuilderExtensions.cs
│   │   ├── FastGithub.HttpServer.csproj
│   │   ├── KestrelServerExtensions.cs
│   │   └── ServiceCollectionExtensions.cs
│   ├── FastGithub.PacketIntercept/
│   │   ├── Dns/
│   │   ├── Tcp/
│   │   ├── DnsInterceptHostedService.cs
│   │   ├── FastGithub.PacketIntercept.csproj
│   │   ├── IDnsConflictSolver.cs
│   │   ├── IDnsInterceptor.cs
│   │   ├── ITcpInterceptor.cs
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── TcpInterceptHostedService.cs
│   ├── FastGithub.UI/
│   │   ├── Prebuilt/
│   │   ├── Resource/
│   │   ├── app.ico
│   │   ├── app.manifest
│   │   ├── AssemblyInfo.cs
│   │   ├── FastGithub.UI.csproj
│   │   ├── FlowChart.xaml
│   │   ├── FlowChart.xaml.cs
│   │   ├── FlowStatistics.cs
│   │   ├── IssuesWebbrowser.xaml
│   │   ├── IssuesWebbrowser.xaml.cs
│   │   ├── LogLevel.cs
│   │   └── …（其余 7 项未列出）
│   ├── Resources/
│   │   └── MacOSXConfig/
│   ├── .gitattributes
│   ├── .gitignore
│   └── …（其余 10 项未列出）
├── My Project/
│   ├── PublishProfiles/
│   │   ├── FolderProfile.pubxml
│   │   └── FolderProfile.pubxml.user
│   ├── Application.Designer.vb
│   └── Application.myapp
├── Resources/
│   ├── app.ico
│   └── icon_source.png
├── .gitignore
├── app.manifest
├── ApplicationEvents.vb
├── GitPushTool.slnx
├── GitPushTool.vbproj
├── GitPushTool.vbproj.user
├── MainForm.Designer.vb
├── MainForm.vb
└── …（其余 1 项未列出）
```

## 📝 更新日志

### v2.7.1.0 (2026-11-12)

- 修复推送被拒 `fetch first / non-fast-forward`：推送前自动 `git fetch`，远端领先时先 `git rebase` 合并，再打 tag / 推送
- 刻意不使用 `-X ours/theirs`：实测会静默丢掉远端对同一文件的其它改动，普通 rebase 才能完整保留两边提交
- 合并冲突时自动 `rebase --abort` 干净回退，并弹窗让用户选择「以本地为准强制覆盖」或「中止手动处理」，绝不静默丢数据
- 提前还原 origin 地址，避免中止路径把 PAT 残留进 `.git/config`
- push 退出码 1 的错误提示补充 non-fast-forward / PAT 权限 / 分支保护排查

### v2.7.0.0 (2026-11-11)

- 加速改为**软件启动时同步启用**（默认勾选），不再是推送时才启动
- 新增就绪预热：等待 dnscrypt-proxy、探测 `/flowStatistics`、缓冲 IP 测速，推送前 Await 完成，缓解首次推送 502
- 启动时自动打开原项目 `FastGithub.UI` 窗口（流量 / 日志 / 问答）；UDP 日志端口与内置加速统一为 38457
- 原窗口产物嵌入单文件 exe，运行时释放；取消勾选或关闭本工具时一并停止/关闭
- 加速开关支持随时启停并持久化到 config.ini

### v2.6.0.0 (2026-11-11)

- 将 FastGithub 源码整体并入 `FastGithubSrc/`，通过 `ProjectReference` 引用，改为**进程内调用**（不再是外部进程）
- 新增 `FastGithub.FastGithubRunner` 托管入口，使用 `StartAsync` / `StopAsync` 非阻塞托管 ASP.NET Core 通用主机
- 去除外部路径配置；加速开关仍可记忆，取消勾选或关闭窗口时优雅停止（恢复 ProxyOverride、停 dnscrypt-proxy、释放 WinDivert）
- 配置 / 域名规则 / dnscrypt-proxy 以嵌入资源打进单文件 exe，启动时释放到 `%LOCALAPPDATA%\GitPushTool\fastgithub`
- 新增管理员权限清单，最终仍为单个 `GitPushTool.exe`
- FastGithub 基于 MIT 许可（原作者 老九 / dotnetcore），许可见 `FastGithubSrc/LICENSE`

### v2.5.0.0 (2026-11-11)

- 新增可选 FastGithub 加速：勾选后推送前以独立子进程拉起 fastgithub.exe，系统级透明加速，git 与 GitHub API 自动生效
- 支持在「设置」里指定 fastgithub.exe 路径（可直接指向本机改编版），并自动探测常见目录
- 通过 `ParentProcessId` 让子进程随本程序退出优雅收尾，不做强杀；UAC 取消不影响推送
- 加速开关与路径持久化到 config.ini，默认关闭，发布仍为单文件 exe

### v2.4.7.0 (2026-10-19)

- 自动处理 Git 2.35.2+ 的 safe.directory 安全检查（dubious ownership）：检测到仓库目录所有者与当前用户不一致时，自动加入全局信任并重试，不再中断推送

### v2.4.6.0 (2026-10-18)

- 修复 git config 退出码 128：项目目录没有 .git 时必须先执行 git init（原逻辑未勾选"自动 git init"会跳过）
- git config 改为非关键步骤，失败不再中断推送
- 提交时用 -c 临时传入 user.name / user.email，不再依赖 .git/config 写入成功
- 退出码 128 的错误提示补充常见原因排查

### v2.4.5.0 (2026-10-12)

- 图标查找改为纯按扩展名匹配（ico → png → jpg → svg），不再依赖固定文件名
- 转换生成的 PNG 沿用原图标文件名（xxx.ico → docs/xxx.png），不再固定命名 icon.png

### v2.4.4.0 (2026-10-11)

- 新增应用图标自动导入：查找项目图标（png/jpg/svg/ico），ico 无损提取最大 PNG 帧存入 docs/，并在 README 顶部插图
- 支持 apk 图标提取（aapt 定位图标 + 解压）
- GitHub 不渲染 .ico，已统一转为 PNG
- 检测 .gitignore 是否会忽略图标并给出警告

### v2.4.3.0 (2026-10-11)

- 新增 README"环境要求"运行时自动同步（.NET / Node.js / Python / Java / Android / Flutter / Go / Rust / PHP）
- 可由目标框架推断操作系统（如 net10.0-windows → Windows 10 / 11）
- 修复目录结构写入 README 时每行后残留 "\r" 导致多出空行的问题

### v2.4.2.0 (2026-10-11)

- 修复 README 面板 GitPushTool/TheRoadWind 占位符未替换的问题（改为加载项目时立即替换）
- 新增 README"目录结构"小节自动更新（按项目真实目录生成树状结构，跳过 bin/obj 等构建目录）

### v2.4.1.0 (2026-10-10)

- 修复版本号读取：工程文件里没有版本号时，自动读取 bin\Release / bin\Debug 下编译产物（exe/dll）的文件版本
- 目标框架目录名（如 net10.0-windows）自动适配，不再硬编码
- 全部来源都读不到版本号时按初始版本 1.0.0.0 处理

### v2.4.0.0 (2026-10-10)

- 版本号改为自动读取（支持 exe/apk/工程文件等多种类型），删除“确认升级”按钮并设为只读
- 打 tag 不再删除 Git 仓库原有 tag
- 推送时自动更新 README 里的版本号
- 新增应用程序图标（Git 官方风格）

### v2.3.9.4 (2026-09-29)

- 版本更新

## 📄 License

本项目基于 MIT 协议开源。

---

_Generated by GitPushTool on 2026-11-12 09:57:18_