using Storyboard.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;

namespace Storyboard.Services;

public sealed class JobQueueService
{
    private readonly SemaphoreSlim _concurrency;
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, Func<CancellationToken, IProgress<double>, Task>> _runners = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = new();

    private readonly string _historyFilePath;
    private volatile bool _workerStarted;

    public ObservableCollection<GenerationJob> Jobs { get; } = new();

    public JobQueueService(int maxConcurrency = 2)
    {
        _concurrency = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        _historyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "job-history.json");

        LoadHistory();
        StartWorkerIfNeeded();
    }

    public GenerationJob Enqueue(
        GenerationJobType type,
        int? shotNumber,
        Func<CancellationToken, IProgress<double>, Task> runner,
        int maxAttempts = 2)
    {
        var job = new GenerationJob
        {
            Type = type,
            ShotNumber = shotNumber,
            MaxAttempts = Math.Max(1, maxAttempts),
            Status = GenerationJobStatus.Queued,
            Progress = 0,
            Error = string.Empty,
            Attempt = 0
        };

        _runners[job.Id] = runner;
        _cancellations[job.Id] = new CancellationTokenSource();

        OnUi(() => Jobs.Insert(0, job));
        SaveHistory();

        _channel.Writer.TryWrite(job.Id);
        StartWorkerIfNeeded();

        return job;
    }

    public void Cancel(GenerationJob job)
    {
        if (!_cancellations.TryGetValue(job.Id, out var cts))
            return;

        try { cts.Cancel(); } catch { }
    }

    private void StartWorkerIfNeeded()
    {
        if (_workerStarted)
            return;

        _workerStarted = true;
        _ = Task.Run(WorkerLoopAsync);
    }

    private async Task WorkerLoopAsync()
    {
        await foreach (var jobId in _channel.Reader.ReadAllAsync())
        {
            await _concurrency.WaitAsync().ConfigureAwait(false);
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessJobAsync(jobId).ConfigureAwait(false);
                }
                finally
                {
                    _concurrency.Release();
                }
            });
        }
    }

    private async Task ProcessJobAsync(Guid jobId)
    {
        var job = Jobs.FirstOrDefault(j => j.Id == jobId);
        if (job == null)
            return;

        if (!_runners.TryGetValue(jobId, out var runner))
        {
            OnUi(() =>
            {
                job.Status = GenerationJobStatus.Failed;
                job.Error = "无法执行：缺少任务执行器（可能来自历史记录）。";
                job.CompletedAt = DateTimeOffset.Now;
            });
            SaveHistory();
            return;
        }

        if (!_cancellations.TryGetValue(jobId, out var cts))
        {
            cts = new CancellationTokenSource();
            _cancellations[jobId] = cts;
        }

        var progress = new Progress<double>(p =>
        {
            var value = double.IsNaN(p) ? 0 : Math.Clamp(p, 0, 1);
            OnUi(() => job.Progress = value);
        });

        for (int attempt = 1; attempt <= job.MaxAttempts; attempt++)
        {
            OnUi(() =>
            {
                job.Attempt = attempt;
                job.Status = attempt == 1 ? GenerationJobStatus.Running : GenerationJobStatus.Retrying;
                job.StartedAt ??= DateTimeOffset.Now;
                job.Error = string.Empty;
            });
            SaveHistory();

            try
            {
                await runner(cts.Token, progress).ConfigureAwait(false);

                OnUi(() =>
                {
                    job.Status = GenerationJobStatus.Succeeded;
                    job.Progress = 1;
                    job.CompletedAt = DateTimeOffset.Now;
                });
                SaveHistory();
                return;
            }
            catch (OperationCanceledException)
            {
                OnUi(() =>
                {
                    job.Status = GenerationJobStatus.Canceled;
                    job.Error = "已取消";
                    job.CompletedAt = DateTimeOffset.Now;
                });
                SaveHistory();
                return;
            }
            catch (Exception ex)
            {
                OnUi(() => job.Error = ex.Message);
                SaveHistory();

                if (attempt >= job.MaxAttempts)
                {
                    OnUi(() =>
                    {
                        job.Status = GenerationJobStatus.Failed;
                        job.CompletedAt = DateTimeOffset.Now;
                    });
                    SaveHistory();
                    return;
                }

                await Task.Delay(300, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
                return;

            var json = File.ReadAllText(_historyFilePath);
            var items = JsonSerializer.Deserialize<GenerationJobSnapshot[]>(json);
            if (items == null || items.Length == 0)
                return;

            OnUi(() =>
            {
                Jobs.Clear();
                foreach (var it in items.OrderByDescending(i => i.CreatedAt))
                {
                    Jobs.Add(new GenerationJob
                    {
                        Id = it.Id,
                        Type = it.Type,
                        ShotNumber = it.ShotNumber,
                        MaxAttempts = it.MaxAttempts,
                        Status = it.Status,
                        Progress = it.Progress,
                        Error = it.Error ?? string.Empty,
                        Attempt = it.Attempt,
                        CreatedAt = it.CreatedAt,
                        StartedAt = it.StartedAt,
                        CompletedAt = it.CompletedAt
                    });
                }
            });
        }
        catch
        {
            // ignore broken history
        }
    }

    private void SaveHistory()
    {
        try
        {
            // Keep only recent N
            var snapshot = Jobs
                .Take(200)
                .Select(j => new GenerationJobSnapshot
                {
                    Id = j.Id,
                    Type = j.Type,
                    ShotNumber = j.ShotNumber,
                    Status = j.Status,
                    Progress = j.Progress,
                    Error = j.Error,
                    Attempt = j.Attempt,
                    MaxAttempts = j.MaxAttempts,
                    CreatedAt = j.CreatedAt,
                    StartedAt = j.StartedAt,
                    CompletedAt = j.CompletedAt
                })
                .ToArray();

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyFilePath, json);
        }
        catch
        {
            // ignore write failures
        }
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    private sealed class GenerationJobSnapshot
    {
        public Guid Id { get; set; }
        public GenerationJobType Type { get; set; }
        public int? ShotNumber { get; set; }
        public GenerationJobStatus Status { get; set; }
        public double Progress { get; set; }
        public string? Error { get; set; }
        public int Attempt { get; set; }
        public int MaxAttempts { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
