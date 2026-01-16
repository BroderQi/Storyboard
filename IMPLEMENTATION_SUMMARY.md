# 分镜大师 - 后端逻辑实现总结

## 📋 完成的工作

### 1. ✅ 视频抽帧功能 - 提取素材基本信息

**文件修改：**
- `App/ViewModels/MainViewModel.cs`

**新增功能：**
- `ExtractMaterialInfo()` - 从抽帧图片中提取素材信息
  - 分辨率（使用 SkiaSharp 读取图片尺寸）
  - 文件大小（格式化显示）
  - 文件格式（从扩展名获取）
  - 主色调（分析图片颜色：暖色调/冷色调/中性）
  - 亮度（分析图片亮度：暗/中等/亮）

- `AnalyzeImageColor()` - 分析图片颜色和亮度
  - 采样图片像素计算平均 RGB 值
  - 根据 RGB 值判断色调
  - 根据亮度值分级

- `FormatFileSize()` - 格式化文件大小显示

**流程：**
```
用户导入视频 → 选择抽帧模式 → 执行抽帧
  ↓
ExtractFrames() 调用 FrameExtractionService
  ↓
BuildShotsFromFrames() 为每个帧创建 ShotItem
  ↓
ExtractMaterialInfo() 提取素材信息
  ↓
填充 ShotItem 的素材字段：
  - MaterialResolution
  - MaterialFileSize
  - MaterialFormat
  - MaterialColorTone
  - MaterialBrightness
  - MaterialFilePath
  - MaterialThumbnailPath
```

---

### 2. ✅ AI 解析功能

#### 2.1 数据模型扩展

**文件修改：**
- `Shared/Models/AiShotDescription.cs`

**新增字段：**
```csharp
// 图片专业参数
string? Composition = null,          // 构图
string? LightingType = null,         // 光线类型
string? TimeOfDay = null,            // 时间
string? ColorStyle = null,           // 色调
string? NegativePrompt = null,       // 负面提示词

// 视频参数
string? VideoPrompt = null,          // 视频主提示词
string? SceneDescription = null,     // 场景描述
string? ActionDescription = null,    // 动作描述
string? StyleDescription = null,     // 风格描述
string? CameraMovement = null,       // 运镜方式
string? ShootingStyle = null,        // 拍摄风格
string? VideoEffect = null,          // 视频特效
string? VideoNegativePrompt = null   // 视频负面提示词
```

#### 2.2 AI 解析应用逻辑

**文件修改：**
- `App/ViewModels/MainViewModel.cs` - `ApplyAiShotDescription()`

**功能：**
- 将 AI 返回的所有字段（包括新增的专业参数）应用到 ShotItem
- 支持三种写入模式：
  - `Overwrite` - 覆盖现有内容
  - `Append` - 追加到现有内容
  - `Skip` - 跳过已有内容

**流程：**
```
单个分镜 AI 解析：
  用户点击 "AI 解析" 按钮
    ↓
  OnShotAiParseRequested()
    ↓
  EnqueueAiParseJob()
    ↓
  调用 AiShotService.AnalyzeShotAsync()
    ↓
  ApplyAiShotDescription() 应用结果到 ShotItem
    ↓
  填充所有字段（基本信息 + 专业参数）

批量 AI 解析：
  用户点击 "批量 AI 分析" 按钮
    ↓
  AIAnalyzeAll() 遍历所有 Shots
    ↓
  为每个 Shot 调用 EnqueueAiParseJob()
    ↓
  队列依次处理所有分镜
```

---

### 3. ✅ 图片生成功能 - 入参优化

#### 3.1 新增请求模型

**新文件：**
- `Shared/Models/ImageGenerationRequest.cs`

**包含参数：**
```csharp
string Prompt,                    // 提示词 *必填
string? Model,                    // 模型
string? OutputDirectory,          // 输出目录
string? FilePrefix,               // 文件前缀
// 专业参数
string? ShotType,                 // 景别
string? Composition,              // 构图
string? LightingType,             // 光线
string? TimeOfDay,                // 时间
string? ColorStyle,               // 色调
string? NegativePrompt,           // 负面提示词
string? ImageSize,                // 尺寸
string? AspectRatio               // 比例
```

#### 3.2 服务接口扩展

**文件修改：**
- `Application/Abstractions/IImageGenerationService.cs`

**新增方法：**
```csharp
Task<string> GenerateImageAsync(
    ImageGenerationRequest request,
    CancellationToken cancellationToken = default);
```

#### 3.3 ViewModel 更新

**文件修改：**
- `App/ViewModels/MainViewModel.cs`
  - `EnqueueFirstFrameJob()`
  - `EnqueueLastFrameJob()`

**改进：**
- 使用 `ImageGenerationRequest` 传递所有参数
- 包含用户在 UI 上设置的所有专业参数
- 支持负面提示词、尺寸、比例等高级选项

**流程：**
```
用户填写首帧/尾帧提示词 + 专业参数
  ↓
点击 "生成" 按钮
  ↓
EnqueueFirstFrameJob() / EnqueueLastFrameJob()
  ↓
创建 ImageGenerationRequest 包含所有参数：
  - Prompt (必填)
  - Model
  - ShotType, Composition, LightingType, TimeOfDay, ColorStyle
  - NegativePrompt
  - ImageSize, AspectRatio
  ↓
调用 ImageGenerationService.GenerateImageAsync(request)
  ↓
生成图片并保存到 output/images/
  ↓
AddAssetToShot() 添加到历史记录
```

---

### 4. ✅ 视频生成功能 - 入参检查

#### 4.1 新增请求模型

**新文件：**
- `Shared/Models/VideoGenerationRequest.cs`

**包含参数：**
```csharp
string Prompt,                    // 主提示词 *必填
double DurationSeconds,           // 时长 *必填
string? Model,                    // 模型
string? OutputDirectory,          // 输出目录
string? FilePrefix,               // 文件前缀
// 参考图
string? FirstFrameImagePath,      // 首帧图片路径
string? LastFrameImagePath,       // 尾帧图片路径
bool UseFirstFrameReference,      // 是否使用首帧参考
bool UseLastFrameReference,       // 是否使用尾帧参考
// 专业参数
string? SceneDescription,         // 场景描述
string? ActionDescription,        // 动作描述
string? StyleDescription,         // 风格描述
string? CameraMovement,           // 运镜方式
string? ShootingStyle,            // 拍摄风格
string? VideoEffect,              // 视频特效
string? NegativePrompt,           // 负面提示词
// 技术参数
string? VideoResolution,          // 分辨率
string? VideoRatio,               // 比例
int? VideoFrames,                 // 帧数
int? Seed,                        // 随机种子
bool CameraFixed,                 // 固定摄影机
bool Watermark                    // 水印
```

#### 4.2 当前实现

**文件：**
- `Application/Abstractions/IVideoGenerationService.cs`

**当前接口：**
```csharp
Task<string> GenerateVideoAsync(
    ShotItem shot,  // 直接传递 ShotItem，包含所有字段
    string? outputDirectory = null,
    string? filePrefix = null,
    CancellationToken cancellationToken = default);
```

**说明：**
- 视频生成服务目前接收整个 `ShotItem` 对象
- `ShotItem` 已经包含了所有需要的字段（通过我们的 UI 重构添加）
- 服务实现可以直接访问 `shot.VideoPrompt`, `shot.CameraMovement` 等字段
- **入参已经合理，无需修改**

**流程：**
```
用户填写视频生成参数：
  - 主提示词 (VideoPrompt)
  - 场景/动作/风格描述
  - 专业参数（运镜、拍摄风格等）
  - 生成设置（分辨率、比例、时长、帧数）
  - 参考图选择（首帧/尾帧）
  - 高级选项（Seed、固定摄影机、水印）
  ↓
点击 "生成视频" 按钮
  ↓
EnqueueVideoJob(shot)
  ↓
调用 VideoGenerationService.GenerateVideoAsync(shot, ...)
  ↓
服务从 shot 对象读取所有参数
  ↓
生成视频并保存到 output/videos/
  ↓
创建缩略图
  ↓
AddAssetToShot() 添加到历史记录
```

---

## 🔄 完整工作流程

### 流程 1：从视频导入到 AI 解析

```
1. 用户导入视频
   ↓
2. 系统提取视频元数据（时长、分辨率、帧率）
   ↓
3. 用户选择抽帧模式（定数/动态/等时/关键帧）
   ↓
4. 执行抽帧 → 生成多个 ShotItem
   ↓
5. 每个 ShotItem 自动提取素材信息：
   - 分辨率、文件大小、格式
   - 主色调、亮度
   ↓
6. 用户点击 "批量 AI 分析" 或单个分镜的 "AI 解析"
   ↓
7. AI 分析素材图片，返回：
   - 基本信息（镜头类型、核心画面、动作、场景）
   - 首帧/尾帧提示词
   - 专业参数（构图、光线、色调等）
   - 视频参数（场景描述、运镜方式等）
   ↓
8. 系统应用 AI 结果到 ShotItem
   ↓
9. 用户可以在 UI 上查看和微调所有字段
```

### 流程 2：图片生成

```
1. 用户在 "图片生成" 标签页查看/编辑：
   - 首帧提示词（AI 已填充或手动输入）
   - 专业参数（景别、构图、光线、色调、时间）
   - 负面提示词
   - 生成设置（尺寸、模型）
   ↓
2. 点击 "生成" 或 "重新生成"
   ↓
3. 系统创建 ImageGenerationRequest 包含所有参数
   ↓
4. 调用图片生成服务
   ↓
5. 生成的图片保存到 output/images/
   ↓
6. 添加到历史生成记录
   ↓
7. 用户可以从历史记录中选择使用
```

### 流程 3：视频生成

```
1. 用户在 "视频生成" 标签页查看/编辑：
   - 主提示词（AI 已填充或手动输入）
   - 场景/动作/风格描述（可组合到主提示词）
   - 专业参数（运镜、拍摄风格、摄影机运动、特效）
   - 负面提示词
   - 生成设置（分辨率、比例、时长、帧数、参考图）
   - 高级选项（Seed、固定摄影机、水印）
   ↓
2. 点击 "生成视频"
   ↓
3. 系统从 ShotItem 读取所有参数
   ↓
4. 调用视频生成服务
   ↓
5. 生成的视频保存到 output/videos/
   ↓
6. 自动创建缩略图
   ↓
7. 添加到生成记录
   ↓
8. 在视频预览器中播放
```

---

## 🎯 关键设计决策

### 1. 避免 AI 返回值与枚举不匹配

**问题：** 如果使用下拉框（ComboBox），AI 返回的值可能与枚举选项不匹配。

**解决方案：**
- ✅ 使用 **自由文本输入框（TextBox）** + **Watermark 提示**
- ✅ AI 可以返回任意文本，用户也可以手动输入任意值
- ✅ Watermark 提供常见选项作为参考（如 "特写/近景/中景/全景/远景"）

### 2. 参数传递方式

**图片生成：**
- 使用 `ImageGenerationRequest` 结构化传递参数
- 清晰、类型安全、易于扩展

**视频生成：**
- 直接传递 `ShotItem` 对象
- 简化调用，避免参数过多
- `ShotItem` 已包含所有需要的字段

### 3. 可折叠 UI 设计

**目的：** 避免界面拥挤，同时保留专业功能

**实现：**
- 默认折叠：专业参数、负面提示词、高级选项
- 最重要的"提示词"放在最上面，最大最显眼
- 用户可以按需展开调整

---

## 📝 待实现的服务层逻辑

虽然 ViewModel 层已经完成，但以下服务实现需要更新以支持新参数：

### 1. ImageGenerationService 实现

**文件：** `Infrastructure/Services/ImageGenerationService.cs`

**需要实现：**
```csharp
public async Task<string> GenerateImageAsync(
    ImageGenerationRequest request,
    CancellationToken cancellationToken = default)
{
    // 1. 构建完整的提示词
    var fullPrompt = BuildFullPrompt(request);

    // 2. 调用图片生成 API（火山引擎/通义千问等）
    //    传递：prompt, negative_prompt, size, aspect_ratio 等

    // 3. 保存生成的图片

    // 4. 返回图片路径
}

private string BuildFullPrompt(ImageGenerationRequest request)
{
    var parts = new List<string> { request.Prompt };

    // 添加专业参数到提示词
    if (!string.IsNullOrWhiteSpace(request.ShotType))
        parts.Add($"shot type: {request.ShotType}");
    if (!string.IsNullOrWhiteSpace(request.Composition))
        parts.Add($"composition: {request.Composition}");
    if (!string.IsNullOrWhiteSpace(request.LightingType))
        parts.Add($"lighting: {request.LightingType}");
    // ... 其他参数

    return string.Join(", ", parts);
}
```

### 2. VideoGenerationService 实现

**文件：** `Infrastructure/Services/VideoGenerationService.cs`

**需要更新：**
```csharp
public async Task<string> GenerateVideoAsync(
    ShotItem shot,
    string? outputDirectory = null,
    string? filePrefix = null,
    CancellationToken cancellationToken = default)
{
    // 1. 构建完整的视频提示词
    var fullPrompt = BuildVideoPrompt(shot);

    // 2. 准备参考图（如果用户选择了）
    var referenceImages = new List<string>();
    if (shot.UseFirstFrameReference && !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath))
        referenceImages.Add(shot.FirstFrameImagePath);
    if (shot.UseLastFrameReference && !string.IsNullOrWhiteSpace(shot.LastFrameImagePath))
        referenceImages.Add(shot.LastFrameImagePath);

    // 3. 调用视频生成 API
    //    传递：prompt, duration, resolution, ratio, frames, seed,
    //          camera_fixed, watermark, reference_images, negative_prompt 等

    // 4. 保存生成的视频

    // 5. 返回视频路径
}

private string BuildVideoPrompt(ShotItem shot)
{
    var parts = new List<string>();

    // 主提示词
    if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
        parts.Add(shot.VideoPrompt);

    // 场景/动作/风格描述
    if (!string.IsNullOrWhiteSpace(shot.SceneDescription))
        parts.Add(shot.SceneDescription);
    if (!string.IsNullOrWhiteSpace(shot.ActionDescription))
        parts.Add(shot.ActionDescription);
    if (!string.IsNullOrWhiteSpace(shot.StyleDescription))
        parts.Add(shot.StyleDescription);

    // 专业参数
    if (!string.IsNullOrWhiteSpace(shot.CameraMovement))
        parts.Add($"camera movement: {shot.CameraMovement}");
    if (!string.IsNullOrWhiteSpace(shot.ShootingStyle))
        parts.Add($"shooting style: {shot.ShootingStyle}");
    // ... 其他参数

    return string.Join(", ", parts);
}
```

### 3. AiShotService 更新

**文件：** `Infrastructure/Services/AiShotService.cs`

**需要更新 AI Prompt 模板：**
- 让 AI 返回新增的专业参数字段
- 更新 JSON 解析逻辑以支持新字段

---

## ✅ 总结

### 已完成：
1. ✅ 视频抽帧 → 自动提取素材基本信息
2. ✅ AI 解析 → 支持专业参数字段
3. ✅ 图片生成 → 使用结构化请求模型传递所有参数
4. ✅ 视频生成 → 入参检查完成，ShotItem 包含所有字段
5. ✅ UI 重构 → 三标签页设计，可折叠专业参数
6. ✅ 数据模型 → 扩展支持所有新字段

### 待完成（服务层实现）：
1. ⏳ ImageGenerationService 实现新的 `GenerateImageAsync(ImageGenerationRequest)` 方法
2. ⏳ VideoGenerationService 更新以使用 ShotItem 的所有新字段
3. ⏳ AiShotService 更新 AI Prompt 模板以返回专业参数

### 整体流程已打通：
✅ 视频导入 → 抽帧 → 提取素材信息 → AI 解析 → 图片生成 → 视频生成

所有 ViewModel 层逻辑已完成，UI 已重构，数据流已打通。
只需要在服务层实现具体的 API 调用逻辑即可。
