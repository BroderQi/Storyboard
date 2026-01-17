using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Project;

/// <summary>
/// 项目管理 ViewModel - 负责项目的 CRUD 操作
/// </summary>
public partial class ProjectManagementViewModel : ObservableObject
{
    private readonly IProjectStore _projectStore;
    private readonly IMessenger _messenger;
    private readonly ILogger<ProjectManagementViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _projects = new();

    [ObservableProperty]
    private string? _currentProjectId;

    [ObservableProperty]
    private string _projectName = "未命名项目";

    [ObservableProperty]
    private bool _hasCurrentProject;

    [ObservableProperty]
    private bool _isNewProjectDialogOpen;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    public bool HasProjects => Projects.Count > 0;

    public ProjectManagementViewModel(
        IProjectStore projectStore,
        IMessenger messenger,
        ILogger<ProjectManagementViewModel> logger)
    {
        _projectStore = projectStore;
        _messenger = messenger;
        _logger = logger;

        // 订阅查询消息 - 允许其他ViewModel查询当前项目ID
        _messenger.Register<GetCurrentProjectIdQuery>(this, (r, m) => m.ProjectId = CurrentProjectId);

        _ = ReloadProjectsAsync();
    }

    [RelayCommand]
    private void ShowCreateProjectDialog()
    {
        _logger.LogInformation("ShowCreateProjectDialog called");
        IsNewProjectDialogOpen = true;
        _logger.LogInformation("IsNewProjectDialogOpen set to true");
    }

    [RelayCommand]
    private async Task CreateNewProject(string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = $"新项目 {DateTime.Now:MMdd-HHmm}";
        }

        // 保存到数据库并获取项目 ID
        CurrentProjectId = await _projectStore.CreateAsync(projectName);
        ProjectName = projectName;
        HasCurrentProject = true;
        IsNewProjectDialogOpen = false;

        // 发送项目创建消息
        _messenger.Send(new ProjectCreatedMessage(CurrentProjectId, ProjectName));

        // 更新项目列表
        UpsertCurrentProject(0, 0, 0);

        _logger.LogInformation("创建新项目: {ProjectName} (ID: {ProjectId})", ProjectName, CurrentProjectId);
    }

    [RelayCommand]
    private async Task OpenProject(ProjectInfo? project)
    {
        if (project == null)
            return;

        CurrentProjectId = project.Id;
        ProjectName = project.Name;
        HasCurrentProject = true;

        _logger.LogInformation("打开项目: {ProjectName} (ID: {ProjectId})", ProjectName, CurrentProjectId);

        // 发送项目打开消息
        _messenger.Send(new ProjectOpenedMessage(CurrentProjectId, ProjectName));

        // 加载项目数据
        try
        {
            var state = await _projectStore.LoadAsync(project.Id);
            if (state != null)
            {
                // 发送项目数据加载完成消息，让其他 ViewModel 接收并处理
                _messenger.Send(new ProjectDataLoadedMessage(state));
                _logger.LogInformation("项目数据加载成功: {ShotCount} 个镜头", state.Shots.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载项目失败: {ProjectId}", project.Id);
        }
    }

    [RelayCommand]
    private void CloseProject()
    {
        if (!HasCurrentProject || string.IsNullOrWhiteSpace(CurrentProjectId))
            return;

        var projectId = CurrentProjectId;

        CurrentProjectId = null;
        ProjectName = "未命名项目";
        HasCurrentProject = false;

        // 发送项目关闭消息
        _messenger.Send(new ProjectClosedMessage(projectId));

        _logger.LogInformation("关闭项目: {ProjectId}", projectId);
    }

    [RelayCommand]
    private async Task DeleteProject(ProjectInfo? project)
    {
        if (project == null)
            return;

        try
        {
            await _projectStore.DeleteAsync(project.Id).ConfigureAwait(false);

            // 从列表中移除
            var item = Projects.FirstOrDefault(p => p.Id == project.Id);
            if (item != null)
                Projects.Remove(item);

            // 如果删除的是当前项目，关闭它
            if (CurrentProjectId == project.Id)
            {
                CloseProject();
            }

            // 发送项目删除消息
            _messenger.Send(new ProjectDeletedMessage(project.Id));

            _logger.LogInformation("删除项目: {ProjectName} (ID: {ProjectId})", project.Name, project.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除项目失败: {ProjectId}", project.Id);
        }
    }

    public async Task ReloadProjectsAsync()
    {
        try
        {
            var list = await _projectStore.GetRecentAsync().ConfigureAwait(false);

            Projects.Clear();
            foreach (var dto in list)
                Projects.Add(ToProjectInfo(dto));

            OnPropertyChanged(nameof(HasProjects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载项目列表失败");
        }
    }

    public void UpsertCurrentProject(int totalShots, int completedShots, int hasImages)
    {
        if (!HasCurrentProject)
            return;

        if (string.IsNullOrWhiteSpace(CurrentProjectId))
            CurrentProjectId = Guid.NewGuid().ToString("N");

        var updatedAt = DateTimeOffset.Now;
        var dto = new ProjectSummary(
            CurrentProjectId!,
            ProjectName,
            updatedAt,
            totalShots,
            completedShots,
            hasImages);

        var existing = Projects.FirstOrDefault(p => p.Id == dto.Id);
        if (existing == null)
        {
            Projects.Insert(0, ToProjectInfo(dto));
        }
        else
        {
            existing.Name = dto.Name;
            existing.UpdatedAt = dto.UpdatedAt;
            existing.UpdatedTimeAgo = FormatTimeAgo(dto.UpdatedAt);
            existing.CompletionText = dto.TotalShots > 0 ? $"{dto.CompletedShots} / {dto.TotalShots}" : "0%";
            var completionRate = dto.TotalShots > 0 ? (int)Math.Round((double)dto.CompletedShots / dto.TotalShots * 100) : 0;
            existing.CompletionWidth = dto.TotalShots > 0 ? Math.Clamp(2.2 * completionRate, 0, 220) : 0;
            existing.ShotCountText = $"{dto.TotalShots} 分镜";
            existing.ImageCountText = $"{dto.HasImages} 图片";

            // 移到顶部
            var idx = Projects.IndexOf(existing);
            if (idx > 0)
                Projects.Move(idx, 0);
        }

        OnPropertyChanged(nameof(HasProjects));

        // 发送项目更新消息
        _messenger.Send(new ProjectUpdatedMessage(CurrentProjectId!));
    }

    private static ProjectInfo ToProjectInfo(ProjectSummary dto)
    {
        var completionRate = dto.TotalShots > 0 ? (int)Math.Round((double)dto.CompletedShots / dto.TotalShots * 100) : 0;
        return new ProjectInfo
        {
            Id = dto.Id,
            Name = dto.Name,
            UpdatedAt = dto.UpdatedAt,
            UpdatedTimeAgo = FormatTimeAgo(dto.UpdatedAt),
            CompletionText = dto.TotalShots > 0 ? $"{dto.CompletedShots} / {dto.TotalShots}" : "0%",
            CompletionWidth = dto.TotalShots > 0 ? Math.Clamp(2.2 * completionRate, 0, 220) : 0,
            ShotCountText = $"{dto.TotalShots} 分镜",
            ImageCountText = $"{dto.HasImages} 图片"
        };
    }

    private static string FormatTimeAgo(DateTimeOffset timestamp)
    {
        var now = DateTimeOffset.Now;
        var diff = now - timestamp;

        if (diff.TotalHours < 1)
            return "刚刚";
        if (diff.TotalHours < 24)
            return $"{(int)Math.Floor(diff.TotalHours)} 小时前";
        if (diff.TotalDays < 7)
            return $"{(int)Math.Floor(diff.TotalDays)} 天前";

        return timestamp.LocalDateTime.ToString("yyyy/M/d");
    }
}
