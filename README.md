<h1 align="center">🎬 分镜大师 Storyboard Studio</h1>

<p align="center"><b>本地分镜工作台：视频导入 → 抽帧 → AI 解析 → 图片/视频生成 → 批量任务 → 成片合成</b></p>
<p align="center"><b>Local storyboard workbench: video import → frame extraction → AI analysis → image/video generation → batch jobs → final render</b></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/Avalonia-11.x-3A7CF0" alt="Avalonia">
  <img src="https://img.shields.io/badge/SQLite-Embedded-003B57" alt="SQLite">
  <img src="https://img.shields.io/badge/FFmpeg-Bundled-4CBB17" alt="FFmpeg">
  <img src="https://img.shields.io/badge/AI-Multi%20Provider-FF6B6B" alt="AI Providers">
</p>

> 只需导入视频或输入文本，即可快速生成完整分镜与素材资产。支持多 Provider、多模型配置，兼顾本地渲染/合成与云端模型调用。
> Import a video or text to quickly generate complete storyboards and assets. Supports multiple providers/models, with both local rendering/compositing and cloud calls.



## ✨ 功能亮点 / Highlights

- ✅ 项目化管理：创建/打开项目，SQLite 持久化，最近项目历史。/ Project management: create/open projects, SQLite persistence, recent history.
- ✅ 视频导入与元数据解析：时长/分辨率/帧率自动识别（ffprobe）。/ Video import & metadata parsing: auto-detect duration/resolution/fps (ffprobe).
- ✅ 抽帧四模式：定数、动态间隔、等时、关键帧检测。/ Four extraction modes: fixed count, dynamic interval, equal time, keyframe detection.
- ✅ 分镜编辑：镜头字段全量编辑，拖拽排序，时间线视图。/ Shot editing: full field editing, drag ordering, timeline view.
- ✅ AI 镜头解析：首尾帧特征 → 结构化镜头描述（覆盖/追加/放弃）。/ AI shot parsing: head/tail features → structured descriptions (overwrite/append/skip).
- ✅ 文本生成分镜：自然语言描述自动拆分多镜头。/ Text-to-storyboard: split natural language into multiple shots.
- ✅ 图片/视频生成：首帧、尾帧、成片多次生成保留历史，用户显式绑定。/ Image/video generation: multi-run history for first/last/full, explicit binding by user.
- ✅ 配置管理：多 Provider、多模型组合，文本/图片/视频各自独立配置。/ Config management: multi-provider/multi-model, separate text/image/video configs.
- ✅ 本地能力：本地渲染图片、本地 FFmpeg 合成视频，可与云端模型并存。/ Local capability: local image render + FFmpeg video, coexists with cloud models.
- ✅ 批量任务与任务管理：解析/生成/合成批处理，不互相影响。/ Batch jobs & task management: parse/generate/compose without interference.
- ✅ 导出：分镜 JSON 与合成成片输出。/ Export: storyboard JSON and final video output.



## 🖼️ 界面预览 / UI Preview

打开软件，首页创建项目：
resources\home.png

创建项目成功，进入主页面
resources\main.png

主页面左列导入视频，进行分镜
resources\storyboard.png

可以批量生成
resources\batch.png

任务管理
resources\taskmanage.png

导出成品
resources\export.png

配置AI模型
resources\AiProder.png


## 🌐 Web 演示 / Web Demo

地址：http://47.100.163.84/（只包含 UI，没有后端实现）
Web demo: http://47.100.163.84/ (UI only, no backend implementation)

## 🧭 工作流 / Workflow

**视频导入** → **抽帧/分镜** → **AI 解析/文本生成** → **图片生成** → **视频生成** → **整片合成**
**Video import** → **Frame extraction/storyboard** → **AI analysis/text-to-storyboard** → **Image generation** → **Video generation** → **Final render**

每个环节都可独立执行，支持手动编辑与批量任务。
Each stage can run independently, supporting manual edits and batch jobs.

## 🚀 快速开始 / Quick Start

1. 安装 .NET 8 SDK / Install .NET 8 SDK
2. 在项目根目录执行：/ Run in the project root:

```bash
dotnet restore
dotnet build
dotnet run
```

也可在 Visual Studio 2022 打开 `Storyboard.sln` 直接运行。
You can also open `Storyboard.sln` in Visual Studio 2022 and run directly.

## ⚙️ 配置管理（多模型 / 本地模型） / Configuration (Multi-model / Local)

配置入口：
- 应用内「提供商设置」界面（推荐） / In-app "Provider Settings" page (recommended)
- 或直接编辑 `appsettings.json` / Or edit `appsettings.json` directly

关键配置模块：
- `AIServices`: 文本理解 Provider（Qwen / Zhipu / Wenxin / Volcengine / OpenAI / Azure OpenAI） / Text understanding providers (Qwen / Zhipu / Wenxin / Volcengine / OpenAI / Azure OpenAI)
- `Image`: 图片生成 Provider（本地渲染 / OpenAI） / Image generation providers (local renderer / OpenAI)
- `Video`: 视频生成 Provider（本地 FFmpeg 合成） / Video generation providers (local FFmpeg)

配置管理能力：
- 多 Provider、多模型并存，界面可选择默认 Provider。/ Multiple providers/models coexist; UI lets you choose defaults.
- 本地渲染/本地合成与云端模型可并行配置，按任务选择与切换。/ Local render/compose and cloud models can be configured in parallel, switchable per task.

## 🗂️ 目录结构 / Project Structure

```
分镜大师/
├─ App/                     # Avalonia UI
├─ Application/             # 应用层 / Application layer
├─ Domain/                  # 领域模型 / Domain models
├─ Infrastructure/          # 基础设施（持久化/AI/媒体服务） / Infrastructure (persistence/AI/media)
├─ Shared/                  # 跨层模型与 DTO / Shared models & DTOs
├─ Tools/ffmpeg/            # 内置 ffmpeg/ffprobe / Bundled ffmpeg/ffprobe
├─ appsettings.json
└─ Storyboard.sln
```

## 📦 数据与输出 / Data & Output

- 数据库位置：`%LOCALAPPDATA%/StoryboardStudio/storyboard.db` / Database location: `%LOCALAPPDATA%/StoryboardStudio/storyboard.db`
- 输出目录：`output/projects/<ProjectId>/images`、`output/projects/<ProjectId>/videos` / Output paths: `output/projects/<ProjectId>/images`, `output/projects/<ProjectId>/videos`

## 🧰 FFmpeg 依赖 / FFmpeg Dependency

项目已内置 `Tools/ffmpeg`，视频导入、抽帧与本地视频合成会自动使用。
`Tools/ffmpeg` is bundled; video import, frame extraction, and local composition use it automatically.

## 🧪 技术栈 / Tech Stack

- 框架：.NET 8 + Avalonia / Framework: .NET 8 + Avalonia
- 架构：MVVM + 分层（Domain / Application / Infrastructure / App） / Architecture: MVVM + layers (Domain / Application / Infrastructure / App)
- 数据：SQLite + EF Core / Data: SQLite + EF Core
- AI：Semantic Kernel + 多 Provider 适配 / AI: Semantic Kernel + multi-provider adapters
- 媒体：FFmpeg / FFprobe / Media: FFmpeg / FFprobe

## 🗺️ 路线图 / Roadmap

- TTS 配音 / TTS voiceover
- 自动剪辑优化 / Auto-editing optimization
- 自动风格迁移 / Automatic style transfer
- 社交发布 / Social publishing
