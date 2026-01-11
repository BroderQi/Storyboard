# TODO（分镜大师）

> 目标：对齐/吸收 Pixelle-Video 的“可插拔原子能力 + 任务化 + 模板化 + 可预览可校验”的工程与产品思路。
> 当前状态：WPF 交互与数据表已基本成型，但视频分析/图像生成/视频生成仍为模拟实现；缺少任务队列/历史记录/整片合成/模板体系。

---

## P0（本迭代必须完成）

### 1) 移除演示数据：启动不再自动加载测试分镜
- 背景：`MainViewModel` 构造函数里会 `LoadTestData()`，会干扰真实工作流。
- 验收：启动后 `Shots` 为空；用户上传视频后才填充。
- 涉及：`ViewModels/MainViewModel.cs`

### 2) 真实“配置闭环”：密钥配置页绑定到 `appsettings.json` + Provider 选择/验证
- 需求：
  - UI 可编辑/保存 `AIServices` 配置项（至少：Qwen/Wenxin/Zhipu/Volcengine 的 ApiKey、模型、启用开关）。
  - 提供“测试连接/验证配置”按钮并展示结果。
  - 提供“设置默认 Provider”能力（写回 `AIServices:DefaultProvider`）。
- 验收：
  - 用户在 UI 修改后重启仍生效；
  - `AIServiceManager.GetAvailableProviders()` 能随配置变化；
  - 验证按钮能返回每个 Provider 成功/失败。
- 涉及：`Views/Pages/ApiKeyPage.xaml`、`Views/Pages/ApiKeyPage.xaml.cs`、`App.xaml.cs`（配置写回策略）、`AI/AIServiceManager.cs`、`AI/Core/AIServiceConfig.cs`

### 3) 任务队列（Job Queue）与历史记录（最小可用）
- 需求：将“生成首帧/尾帧/视频”从直接调用改为进入队列，可并发限制、可重试、可取消。
- 建议最小设计：
  - `GenerationJob { Id, Type(ImageFirst/ImageLast/Video/FullRender), ShotId, Status, Progress, Error, CreatedAt }`
  - `JobQueueService`：限制并发（例如 2-4），失败重试次数（例如 1-2），提供取消 token。
  - `JobHistory`：内存 +（可选）落盘 JSON。
- 验收：
  - 批量生成时 UI 不冻结；
  - 可看到队列中/执行中/完成/失败；
  - 应用重启后能看到最近历史（若实现落盘）。
- 涉及：新增 `Services/JobQueueService.cs`（或 `Services/GenerationJobService.cs`）、更新 `ViewModels/MainViewModel.cs`、更新 `Views/Pages/StoryboardPage.xaml`（显示队列/按钮绑定）。

---

## P1（下一迭代：核心能力补齐）

### 4) 视频分析落地：从“模拟数据”到“真实分析 + LLM 结构化输出”
- 目标：替换 `VideoAnalysisService` 的示例数据。
- 实现路径（建议）：
  1. 本地解析基础元数据：时长、帧率、分辨率、关键帧/切点（可用 ffmpeg/ffprobe 或媒体库）。
  2. 将切点摘要 + 抽帧描述输入 LLM，要求返回严格 JSON（分镜列表）。
  3. 将 JSON 解析为 `ShotItem`，并做异常容错（字段缺失/类型不匹配）。
- 验收：同一视频每次生成分镜数量/字段稳定；解析失败有明确错误提示。
- 涉及：`Services/VideoAnalysisService.cs`、`Models/ShotItem.cs`（可能新增 start/end timecode）、`AI/Prompts/PromptManagementService.cs`（模板与参数）。

### 5) 批量生成图片/一键合成按钮：真正绑定命令 + 复用任务队列
- 现状：`StoryboardPage.xaml` 有按钮“批量生成图片/一键合成视频”但未看到绑定 Command。
- 需求：
  - 批量生成图片：为每个分镜排入首帧/尾帧任务。
  - 一键合成：排入“整片合成”任务（见 P1-6）。
- 验收：点击按钮后队列出现对应任务；可观察进度。
- 涉及：`Views/Pages/StoryboardPage.xaml`、`ViewModels/MainViewModel.cs`

### 6) 整片合成（Full Render）：按分镜拼接生成最终视频
- 需求（最小）：
  - 将每个分镜的 `GeneratedVideoPath` 按顺序拼接；
  - 若某分镜缺视频则提示并阻止合成；
  - 输出到统一 `output/` 目录并可打开。
- 备注：可先做“无转场、无字幕、无配音”的最小版本，再逐步增强。
- 验收：生成一个最终视频文件；时长符合各分镜之和（允许编码误差）。
- 涉及：新增 `Services/FinalRenderService.cs`（内部调用 ffmpeg 或其它库），更新 UI。

---

## P2（增强体验：对齐 Pixelle-Video 的模板/预览理念）

### 7) 模板体系（画面布局/风格）：分辨率 + 模板预览 + 自定义模板导入
- 目标：像 Pixelle-Video 的 `static_/image_/video_` 模板一样，让“换风格”可配置。
- 方案建议（WPF 友好）：
  - 模板定义 JSON：字体/字号/边距/字幕区域/背景（纯色/图片/视频）。
  - 预览：选中模板后用示例分镜渲染预览图。
- 验收：用户可切换模板预设；预览与最终输出一致。

### 8) 生成前预检与预览：连通性测试、提示词预览、（可选）TTS 预听
- 目标：将失败尽量前置（像 Pixelle 的“测试连接/预览模板/预览语音”）。
- 验收：生成前一键检查：Provider 可用、输出目录可写、依赖可用。

---

## P3（可选：产品化/集成）

### 9) 项目保存/加载（Storyboard Project）
- 需求：保存 `SelectedVideoPath`（或拷贝/引用）、分镜表、生成结果路径、使用的模型/模板等。
- 验收：保存为 `.storyboard.json`；可加载恢复编辑状态。

### 10) CLI / 本地 API（自动化集成）
- 需求：提供命令行或本地 HTTP API，支持：导入视频→生成分镜→批量生成→导出。
- 验收：无 UI 场景可跑通完整流程。

---

## 备注（对照 Pixelle-Video 的关键启发点）
- “能力可插拔”：你们已完成 LLM Provider 层（`AI/Providers` + `AIServiceManager`）；下一步是把“图像生成/视频生成/整片合成”也做成 Provider/Adapter。
- “任务化/历史记录”：Pixelle 有批量任务/历史/并发；你们建议优先落地 JobQueue。
- “模板化”：Pixelle 的模板命名规范和按分辨率组织方式很清晰；你们可用 JSON/XAML 模板实现同等效果。
