namespace Storyboard.Domain.Entities;

public sealed class ShotAsset
{
    public long Id { get; set; }

    public long ShotId { get; set; }
    public Shot Shot { get; set; } = default!;

    public string ProjectId { get; set; } = default!;

    public ShotAssetType Type { get; set; }

    public string FilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
