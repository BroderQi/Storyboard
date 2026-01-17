using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models.Shot;

/// <summary>
/// 图像生成参数
/// </summary>
public partial class ImageGenerationParams : ObservableObject
{
    [ObservableProperty]
    private string _firstFramePrompt = string.Empty;

    [ObservableProperty]
    private string _lastFramePrompt = string.Empty;

    [ObservableProperty]
    private string _imageSize = string.Empty;

    [ObservableProperty]
    private string _negativePrompt = string.Empty;

    // 专业参数
    [ObservableProperty]
    private string _aspectRatio = string.Empty;

    [ObservableProperty]
    private string _lightingType = string.Empty;

    [ObservableProperty]
    private string _timeOfDay = string.Empty;

    [ObservableProperty]
    private string _composition = string.Empty;

    [ObservableProperty]
    private string _colorStyle = string.Empty;

    [ObservableProperty]
    private string _lensType = string.Empty;

    [ObservableProperty]
    private int? _seed;

    // 折叠状态
    [ObservableProperty]
    private bool _isImageProfessionalParamsExpanded;

    [ObservableProperty]
    private bool _isImageNegativePromptExpanded;
}
