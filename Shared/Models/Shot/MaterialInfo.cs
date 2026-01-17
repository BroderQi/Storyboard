using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models.Shot;

/// <summary>
/// 素材信息
/// </summary>
public partial class MaterialInfo : ObservableObject
{
    [ObservableProperty]
    private string _materialResolution = string.Empty;

    [ObservableProperty]
    private string _materialFileSize = string.Empty;

    [ObservableProperty]
    private string _materialFormat = string.Empty;

    [ObservableProperty]
    private string _materialColorTone = string.Empty;

    [ObservableProperty]
    private string _materialBrightness = string.Empty;

    [ObservableProperty]
    private string? _materialThumbnailPath;

    [ObservableProperty]
    private string? _materialFilePath;
}
