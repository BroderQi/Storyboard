# 分镜大师 Storyboard Studio

面向创作者与制作团队的本地分镜工作台：从视频导入、抽帧、AI 分析、图像/视频生成，到批量任务与成片合成，一条链路完成分镜资产管理与输出。

## 特性亮点

- 项目化管理：创建/打开项目，SQLite 持久化，最近项目历史。
- 视频导入与元数据解析：时长/分辨率/帧率自动识别（ffprobe）。
- 抽帧四模式：定数、动态间隔、等时、关键帧检测。
- 分镜编辑：镜头字段全量编辑，拖拽排序，时间线视图。
- AI 镜头解析：首尾帧特征 → 结构化镜头描述（覆盖/追加/放弃）。
- 文本生成分镜：自然语言描述自动拆分多镜头。
- 图像/视频生成：首帧、尾帧、成片多次生成保留历史，用户显式绑定。
- 配置管理：多 Provider 多模型组合，文本/图片/视频各自独立配置，支持本地模型/本地合成与云端服务切换。
- 批量任务与任务管理：解析/生成/合成批处理，不互相影响。
- 导出：分镜 JSON 与合成成片输出。

## 技术栈

- 框架：.NET 8 + Avalonia
- 架构：MVVM + 分层（Domain / Application / Infrastructure / App）
- 数据：SQLite + EF Core
- AI：Semantic Kernel + 多 Provider 适配
- 媒体：FFmpeg / FFprobe

## 目录结构

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

## 快速开始

1. 安装 .NET 8 SDK
2. 在项目根目录执行：

```bash
dotnet restore
dotnet build
dotnet run
```

也可在 Visual Studio 2022 打开 `Storyboard.sln` 直接运行。

## 配置说明

配置入口：
- 应用内「提供商设置」界面（推荐）
- 或直接编辑 `appsettings.json`

关键配置模块：
- `AIServices`: 文本理解 Provider（Qwen / Zhipu / Wenxin / Volcengine / OpenAI / Azure OpenAI）
- `Image`: 图片生成 Provider（本地渲染 / OpenAI）
- `Video`: 视频生成 Provider（本地 FFmpeg 合成）

配置管理能力：
- 支持多 Provider、多模型并存，界面选择默认 Provider。
- 本地模型/本地渲染与云端模型可并行配置，按任务选择与切换。

## 数据与输出

- 数据库位置：`%LOCALAPPDATA%/StoryboardStudio/storyboard.db`
- 输出目录：`output/projects/<ProjectId>/images`、`output/projects/<ProjectId>/videos`

## FFmpeg 依赖

项目已内置 `Tools/ffmpeg`，视频导入、抽帧与本地视频合成都会自动使用。

## 路线图（不在本期）

- TTS 配音
- 自动剪辑优化
- 自动风格迁移
- 社交发布

## 许可证

待补充。
