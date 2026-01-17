using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models.Shot;

/// <summary>
/// 视频生成参数
/// </summary>
public partial class VideoGenerationParams : ObservableObject
{
    [ObservableProperty]
    private string _videoPrompt = string.Empty;

    [ObservableProperty]
    private string _sceneDescription = string.Empty;

    [ObservableProperty]
    private string _actionDescription = string.Empty;

    [ObservableProperty]
    private string _styleDescription = string.Empty;

    [ObservableProperty]
    private string _videoNegativePrompt = string.Empty;

    // 专业参数
    [ObservableProperty]
    private string _cameraMovement = string.Empty;

    [ObservableProperty]
    private string _shootingStyle = string.Empty;

    [ObservableProperty]
    private string _videoEffect = string.Empty;

    [ObservableProperty]
    private string _videoResolution = string.Empty;

    [ObservableProperty]
    private string _videoRatio = string.Empty;

    [ObservableProperty]
    private int _videoFrames;

    [ObservableProperty]
    private bool _useFirstFrameReference = true;

    [ObservableProperty]
    private bool _useLastFrameReference;

    [ObservableProperty]
    private bool _cameraFixed;

    [ObservableProperty]
    private bool _watermark;

    // 折叠状态
    [ObservableProperty]
    private bool _isVideoSceneActionExpanded;

    [ObservableProperty]
    private bool _isVideoProfessionalParamsExpanded;

    [ObservableProperty]
    private bool _isVideoNegativePromptExpanded;

    [ObservableProperty]
    private bool _isVideoAdvancedOptionsExpanded;
}
