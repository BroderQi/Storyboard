<h1 align="center">🎬 分镜大师 Storyboard Studio</h1>

<p align="center"><b>本地分镜工作台：视频导入 → 抽帧 → AI 解析 → 图片/视频生成 → 批量任务 → 成片合成</b></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/Avalonia-11.x-3A7CF0" alt="Avalonia">
  <img src="https://img.shields.io/badge/SQLite-Embedded-003B57" alt="SQLite">
  <img src="https://img.shields.io/badge/FFmpeg-Bundled-4CBB17" alt="FFmpeg">
  <img src="https://img.shields.io/badge/AI-Multi%20Provider-FF6B6B" alt="AI Providers">
</p>

> 只需导入视频或输入文本，即可快速生成完整分镜与素材资产。支持多 Provider、多模型配置，兼顾本地渲染/合成与云端模型调用。

## ✨ 功能亮点

- ✅ 项目化管理：创建/打开项目，SQLite 持久化，最近项目历史。
- ✅ 视频导入与元数据解析：时长/分辨率/帧率自动识别（ffprobe）。
- ✅ 抽帧四模式：定数、动态间隔、等时、关键帧检测。
- ✅ 分镜编辑：镜头字段全量编辑，拖拽排序，时间线视图。
- ✅ AI 镜头解析：首尾帧特征 → 结构化镜头描述（覆盖/追加/放弃）。
- ✅ 文本生成分镜：自然语言描述自动拆分多镜头。
- ✅ 图片/视频生成：首帧、尾帧、成片多次生成保留历史，用户显式绑定。
- ✅ 配置管理：多 Provider、多模型组合，文本/图片/视频各自独立配置。
- ✅ 本地能力：本地渲染图片、本地 FFmpeg 合成视频，可与云端模型并存。
- ✅ 批量任务与任务管理：解析/生成/合成批处理，不互相影响。
- ✅ 导出：分镜 JSON 与合成成片输出。

## 🖼️ 界面预览

（建议放一张截图，例如 `docs/preview.png`）

## 🧭 工作流

**视频导入** → **抽帧/分镜** → **AI 解析/文本生成** → **图片生成** → **视频生成** → **整片合成**

每个环节都可独立执行，支持手动编辑与批量任务。

## 🚀 快速开始

1. 安装 .NET 8 SDK
2. 在项目根目录执行：

```bash
dotnet restore
dotnet build
dotnet run
```

也可在 Visual Studio 2022 打开 `Storyboard.sln` 直接运行。

## ⚙️ 配置管理（多模型 / 本地模型）

配置入口：
- 应用内「提供商设置」界面（推荐）
- 或直接编辑 `appsettings.json`

关键配置模块：
- `AIServices`: 文本理解 Provider（Qwen / Zhipu / Wenxin / Volcengine / OpenAI / Azure OpenAI）
- `Image`: 图片生成 Provider（本地渲染 / OpenAI）
- `Video`: 视频生成 Provider（本地 FFmpeg 合成）

配置管理能力：
- 多 Provider、多模型并存，界面可选择默认 Provider。
- 本地渲染/本地合成与云端模型可并行配置，按任务选择与切换。

## 🗂️ 目录结构

```
分镜大师/
├─ App/                     # Avalonia UI
├─ Application/             # 应用层
├─ Domain/                  # 领域模型
├─ Infrastructure/          # 基础设施（持久化/AI/媒体服务）
├─ Shared/                  # 跨层模型与 DTO
├─ Tools/ffmpeg/            # 内置 ffmpeg/ffprobe
├─ appsettings.json
└─ Storyboard.sln
```

## 📦 数据与输出

- 数据库位置：`%LOCALAPPDATA%/StoryboardStudio/storyboard.db`
- 输出目录：`output/projects/<ProjectId>/images`、`output/projects/<ProjectId>/videos`

## 🧰 FFmpeg 依赖

项目已内置 `Tools/ffmpeg`，视频导入、抽帧与本地视频合成会自动使用。

## 🧩 技术栈

- 框架：.NET 8 + Avalonia
- 架构：MVVM + 分层（Domain / Application / Infrastructure / App）
- 数据：SQLite + EF Core
- AI：Semantic Kernel + 多 Provider 适配
- 媒体：FFmpeg / FFprobe

## 🗺️ 路线图（不在本期）

- TTS 配音
- 自动剪辑优化
- 自动风格迁移
- 社交发布

## 📄 许可证

待补充。
