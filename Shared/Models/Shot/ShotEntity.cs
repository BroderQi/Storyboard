using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models.Shot;

/// <summary>
/// 镜头核心领域实体，包含基础属性
/// </summary>
public partial class ShotEntity : ObservableObject
{
    [ObservableProperty]
    private int _shotNumber;

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private double _startTime;

    [ObservableProperty]
    private double _endTime;

    [ObservableProperty]
    private string _coreContent = string.Empty;

    [ObservableProperty]
    private string _actionCommand = string.Empty;

    [ObservableProperty]
    private string _sceneSettings = string.Empty;

    [ObservableProperty]
    private string _shotType = string.Empty;

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    public ShotEntity(int shotNumber)
    {
        ShotNumber = shotNumber;
        Duration = 3.5;
    }
}
