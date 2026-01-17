using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models.Shot;

/// <summary>
/// 镜头 UI 状态
/// </summary>
public partial class ShotUIState : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private double _timelineStartPosition;

    [ObservableProperty]
    private double _timelineWidth;
}
