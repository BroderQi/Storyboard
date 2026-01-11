using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models;

public partial class ProjectInfo : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _updatedAt = DateTimeOffset.Now;

    [ObservableProperty]
    private string _updatedTimeAgo = string.Empty;

    [ObservableProperty]
    private string _completionText = "0%";

    [ObservableProperty]
    private double _completionWidth = 0;

    [ObservableProperty]
    private string _shotCountText = "0 分镜";

    [ObservableProperty]
    private string _imageCountText = "0 图片";
}
