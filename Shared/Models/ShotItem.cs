using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace Storyboard.Models;

public partial class ShotItem : ObservableObject
{
    [ObservableProperty]
    private int _shotNumber;

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private string _firstFramePrompt = string.Empty;

    [ObservableProperty]
    private string _lastFramePrompt = string.Empty;

    [ObservableProperty]
    private string _shotType = string.Empty;

    [ObservableProperty]
    private string _coreContent = string.Empty;

    [ObservableProperty]
    private string _actionCommand = string.Empty;

    [ObservableProperty]
    private string _sceneSettings = string.Empty;

    [ObservableProperty]
    private string _selectedModel = string.Empty;

    [ObservableProperty]
    private string? _firstFrameImagePath;

    [ObservableProperty]
    private string? _lastFrameImagePath;

    [ObservableProperty]
    private bool _isFirstFrameGenerating;

    [ObservableProperty]
    private bool _isLastFrameGenerating;

    [ObservableProperty]
    private string? _generatedVideoPath;

    [ObservableProperty]
    private bool _isVideoGenerating;

    [ObservableProperty]
    private string? _materialThumbnailPath;

    [ObservableProperty]
    private string? _materialFilePath;

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

    public string? VideoOutputPath => GeneratedVideoPath;
    public bool CanGenerateVideo => !string.IsNullOrEmpty(FirstFrameImagePath) && !string.IsNullOrEmpty(LastFrameImagePath);

    // Events for communicating with parent ViewModel
    public event EventHandler? DuplicateRequested;
    public event EventHandler? DeleteRequested;

    public ShotItem(int shotNumber)
    {
        ShotNumber = shotNumber;
        Duration = 3.5;
        SelectedModel = "RunwayGen3";
    }

    [RelayCommand]
    private void Duplicate()
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Delete()
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AIParse()
    {
        // TODO: Implement AI parse
    }

    [RelayCommand]
    private void GenerateFirstFrame()
    {
        // TODO: Implement generate first frame
    }

    [RelayCommand]
    private void RegenerateFirstFrame()
    {
        // TODO: Implement regenerate first frame
    }

    [RelayCommand]
    private void GenerateLastFrame()
    {
        // TODO: Implement generate last frame
    }

    [RelayCommand]
    private void RegenerateLastFrame()
    {
        // TODO: Implement regenerate last frame
    }

    [RelayCommand]
    private void GenerateVideo()
    {
        // TODO: Implement generate video
    }
}
