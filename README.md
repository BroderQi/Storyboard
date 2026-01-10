# Storyboard - WPF 应用程序

## 功能概述

一个完整的 WPF 应用程序，用于视频分镜编辑和 AI 生成：

✅ **上传视频** → AI 自动分镜分析
✅ **表格编辑** → 可编辑所有参数
✅ **图像生成** → AI 生成首帧和尾帧
✅ **视频生成** → 基于参数生成视频
✅ **查看/定位文件** → 查看大图 / 观看视频 / 打开文件位置
✅ **拖拽排序** → 拖拽镜头号可调整顺序
✅ **横向滚动** → 查看所有列
✅ **固定列** → 镜头号列固定在左侧
✅ **顶部导航** → 分镜管理 / 使用文档 / 密钥配置

## 项目结构

```
Storyboard/
├── AI/                              # AI 能力与适配层
├── Models/                          # 数据模型
│   ├── ShotItem.cs                 # 分镜项模型
│   └── VideoAnalysisResult.cs      # 视频分析结果
├── ViewModels/                      # 视图模型
│   └── MainViewModel.cs            # 主窗口 ViewModel
├── Services/                        # 服务层
│   ├── VideoAnalysisService.cs     # 视频分析服务
│   ├── ImageGenerationService.cs   # 图像生成服务
│   └── VideoGenerationService.cs   # 视频生成服务
├── Converters/                      # 值转换器
│   └── ValueConverters.cs          # 布尔值等转换器
├── Resources/                       # 资源字典（颜色/样式）
│   └── Theme/
│       ├── Colors.xaml
│       ├── Controls.xaml
│       └── DataGrid.xaml
├── Views/                           # 视图层（Window/UserControl）
│   ├── MainWindow.xaml             # 主窗口 UI
│   ├── MainWindow.xaml.cs          # 主窗口代码
│   └── Windows/                    # 子窗口
│       ├── ImagePreviewWindow.xaml
│       ├── ImagePreviewWindow.xaml.cs
│       ├── AIConfigWindow.xaml
│       └── AIConfigWindow.xaml.cs
├── App.xaml                         # 应用程序配置
├── App.xaml.cs                     # 应用程序代码
├── appsettings.json                 # 配置文件
└── Storyboard.csproj                  # 项目文件
```

## 技术栈

- **框架**: .NET 8.0 + WPF
- **架构**: MVVM (Model-View-ViewModel)
- **依赖注入**: Microsoft.Extensions.DependencyInjection
- **MVVM 工具**: CommunityToolkit.Mvvm

## 功能特性

### 1. 上传视频与 AI 分析
- 点击右上角“📁 上传视频”按钮选择视频文件
- AI 自动分析视频，提取分镜信息
- 显示加载动画和进度提示

### 2. 表格编辑
表格包含以下列：
- **镜头号** (#) - 固定列，拖拽可调整顺序
- **时长** - 可编辑
- **首帧提示词** - 可编辑，支持多行
- **尾帧提示词** - 可编辑，支持多行
- **镜头类型** - 推镜/特写/中景等
- **核心画面** - 场景描述
- **动作指令** - 镜头运动描述
- **场景设定** - 光线、色调等
- **选用模型** - RunwayGen3/Pika/Stable Diffusion
- **首帧图** - 无图时显示“🎨 生成首帧”；有图时显示缩略图 + “查看大图 / 打开文件位置”
- **尾帧图** - 无图时显示“🎨 生成尾帧”；有图时显示缩略图 + “查看大图 / 打开文件位置”
- **视频** - 无视频时显示“🎬 生成视频”；有视频时显示封面 + “观看视频 / 打开文件位置”

### 3. 拖拽排序
- 点击并拖拽镜头号列可调整分镜顺序
- 自动更新镜头编号
- 视觉反馈

### 4. 图像和视频生成
- 异步生成，不阻塞 UI
- 生成中显示"生成中..."状态
- 生成完成后显示缩略图/封面，并提供快捷操作按钮
- 底部状态栏显示进度

### 5. 顶部导航
- **分镜管理**：主表格与生成入口
- **使用文档**：内置的使用说明（应用内可直接查看）
- **密钥配置**：用于配置第三方服务密钥（当前以界面为主，具体接入可按服务实现补齐）

## 运行项目

1. 确保已安装 .NET 8.0 SDK
2. 打开项目文件夹
3. 运行命令：
```bash
dotnet restore
dotnet build
dotnet run
```

也可以直接运行：
```powershell
.\bin\Debug\net8.0-windows\Storyboard.exe
```

或使用 Visual Studio 2022 打开 `Storyboard.sln` 直接运行。

## 待实现功能

以下功能目前返回模拟数据，需要集成真实的 AI 服务：

- [ ] **VideoAnalysisService**: 集成真实的视频分析 AI API
- [ ] **ImageGenerationService**: 集成 Stable Diffusion/DALL-E 等图像生成 API
- [ ] **VideoGenerationService**: 集成 RunwayML/Pika 等视频生成 API
- [ ] **本地存储**: 保存和加载项目
- [ ] **导出功能**: 导出最终合成视频

## UI 截图说明

应用程序界面包含：
- **顶部栏**: 标题和上传按钮
- **顶部导航**: 分镜管理 / 使用文档 / 密钥配置
- **提示栏**: 操作说明
- **主表格**: 可编辑的分镜数据
- **底部栏**: 总时长和生成进度统计

## 开发说明

### 添加新的 AI 模型
在 `MainWindow.xaml` 中的 ComboBox 添加新选项：
```xml
<sys:String>新模型名称</sys:String>
```

### 修改表格列
在 `MainWindow.xaml` 的 `DataGrid.Columns` 中添加或修改列定义。

### 自定义样式
修改 `Styles/DataGridStyles.xaml` 文件来调整表格样式。
