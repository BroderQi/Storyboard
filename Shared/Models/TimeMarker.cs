namespace Storyboard.Models;

public class TimeMarker
{
    public double Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public string TimeText => Label; // Alias for binding compatibility
}
