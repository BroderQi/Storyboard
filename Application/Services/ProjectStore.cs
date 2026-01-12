using Microsoft.EntityFrameworkCore;
using Storyboard.Application.Abstractions;
using Storyboard.Domain.Entities;

namespace Storyboard.Application.Services;

public sealed class ProjectStore : IProjectStore
{
    private readonly IUnitOfWorkFactory _uowFactory;

    public ProjectStore(IUnitOfWorkFactory uowFactory)
    {
        _uowFactory = uowFactory;
    }

    public async Task<IReadOnlyList<ProjectSummary>> GetRecentAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = await _uowFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var projects = await uow.Projects.Query()
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Take(50)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.UpdatedAt,
                TotalShots = p.Shots.Count,
                CompletedShots = p.Shots.Count(s => s.GeneratedVideoPath != null && s.GeneratedVideoPath != ""),
                HasImages = p.Shots.SelectMany(s => s.Assets)
                    .Count(a => a.Type == ShotAssetType.FirstFrameImage || a.Type == ShotAssetType.LastFrameImage)
                    + p.Shots.Count(s => s.Assets.Count == 0 && ((s.FirstFrameImagePath != null && s.FirstFrameImagePath != "") || (s.LastFrameImagePath != null && s.LastFrameImagePath != "")))
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return projects
            .Select(p => new ProjectSummary(p.Id, p.Name, p.UpdatedAt, p.TotalShots, p.CompletedShots, p.HasImages))
            .ToList();
    }

    public async Task<ProjectState?> LoadAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await using var uow = await _uowFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var project = await uow.Projects.Query()
            .AsNoTracking()
            .Include(p => p.Shots)
            .ThenInclude(s => s.Assets)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (project == null)
            return null;

        var shots = project.Shots
            .OrderBy(s => s.ShotNumber)
            .Select(s => new ShotState(
                s.ShotNumber,
                s.Duration,
                s.StartTime,
                s.EndTime,
                s.FirstFramePrompt,
                s.LastFramePrompt,
                s.ShotType,
                s.CoreContent,
                s.ActionCommand,
                s.SceneSettings,
                s.SelectedModel,
                s.FirstFrameImagePath,
                s.LastFrameImagePath,
                s.GeneratedVideoPath,
                s.MaterialThumbnailPath,
                s.MaterialFilePath,
                BuildAssetStates(s)))
            .ToList();

        return new ProjectState(
            project.Id,
            project.Name,
            project.SelectedVideoPath,
            project.HasVideoFile,
            project.VideoFileDuration,
            project.VideoFileResolution,
            project.VideoFileFps,
            project.ExtractModeIndex,
            project.FrameCount,
            project.TimeInterval,
            project.DetectionSensitivity,
            shots);
    }

    private static IReadOnlyList<ShotAssetState> BuildAssetStates(Shot shot)
    {
        var list = shot.Assets
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ShotAssetState(
                a.Type,
                a.FilePath,
                a.ThumbnailPath,
                a.Prompt,
                a.Model,
                a.CreatedAt))
            .ToList();

        if (list.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(shot.FirstFrameImagePath))
                list.Add(new ShotAssetState(ShotAssetType.FirstFrameImage, shot.FirstFrameImagePath, shot.FirstFrameImagePath, shot.FirstFramePrompt, shot.SelectedModel, DateTimeOffset.Now));
            if (!string.IsNullOrWhiteSpace(shot.LastFrameImagePath))
                list.Add(new ShotAssetState(ShotAssetType.LastFrameImage, shot.LastFrameImagePath, shot.LastFrameImagePath, shot.LastFramePrompt, shot.SelectedModel, DateTimeOffset.Now));
            if (!string.IsNullOrWhiteSpace(shot.GeneratedVideoPath))
                list.Add(new ShotAssetState(ShotAssetType.GeneratedVideo, shot.GeneratedVideoPath, null, null, shot.SelectedModel, DateTimeOffset.Now));
        }

        return list;
    }

    public async Task<string> CreateAsync(string projectName, CancellationToken cancellationToken = default)
    {
        await using var uow = await _uowFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.Now;
        var project = new Project
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = projectName,
            CreatedAt = now,
            UpdatedAt = now
        };

        await uow.Projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return project.Id;
    }

    public async Task SaveAsync(ProjectState state, CancellationToken cancellationToken = default)
    {
        await using var uow = await _uowFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var project = await uow.Projects.Query()
            .Include(p => p.Shots)
            .FirstOrDefaultAsync(p => p.Id == state.Id, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.Now;

        if (project == null)
        {
            project = new Project
            {
                Id = state.Id,
                CreatedAt = now
            };

            await uow.Projects.AddAsync(project, cancellationToken).ConfigureAwait(false);
        }

        project.Name = state.Name;
        project.SelectedVideoPath = state.SelectedVideoPath;
        project.HasVideoFile = state.HasVideoFile;
        project.VideoFileDuration = state.VideoFileDuration;
        project.VideoFileResolution = state.VideoFileResolution;
        project.VideoFileFps = state.VideoFileFps;
        project.ExtractModeIndex = state.ExtractModeIndex;
        project.FrameCount = state.FrameCount;
        project.TimeInterval = state.TimeInterval;
        project.DetectionSensitivity = state.DetectionSensitivity;
        project.UpdatedAt = now;

        // Replace shots (simple, predictable). Keep it thin, avoid tracking complex diffs.
        if (project.Shots.Count > 0)
            uow.Shots.RemoveRange(project.Shots);

        project.Shots = state.Shots
            .OrderBy(s => s.ShotNumber)
            .Select(s => new Shot
            {
                ProjectId = project.Id,
                ShotNumber = s.ShotNumber,
                Duration = s.Duration,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                FirstFramePrompt = s.FirstFramePrompt,
                LastFramePrompt = s.LastFramePrompt,
                ShotType = s.ShotType,
                CoreContent = s.CoreContent,
                ActionCommand = s.ActionCommand,
                SceneSettings = s.SceneSettings,
                SelectedModel = s.SelectedModel,
                FirstFrameImagePath = s.FirstFrameImagePath,
                LastFrameImagePath = s.LastFrameImagePath,
                GeneratedVideoPath = s.GeneratedVideoPath,
                MaterialThumbnailPath = s.MaterialThumbnailPath,
                MaterialFilePath = s.MaterialFilePath,
                Assets = s.Assets
                    .Select(a => new ShotAsset
                    {
                        ProjectId = project.Id,
                        Type = a.Type,
                        FilePath = a.FilePath,
                        ThumbnailPath = a.ThumbnailPath,
                        Prompt = a.Prompt,
                        Model = a.Model,
                        CreatedAt = a.CreatedAt
                    })
                    .ToList()
            })
            .ToList();

        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await using var uow = await _uowFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var project = await uow.Projects.GetByIdAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project == null)
            return;

        uow.Projects.Remove(project);
        await uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
