using System.Collections.ObjectModel;
using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IJobQueueService
{
    ObservableCollection<GenerationJob> Jobs { get; }
    GenerationJob Enqueue(GenerationJobType type, int? shotNumber, Func<CancellationToken, IProgress<double>, Task> runner, int maxAttempts = 2);
    void Cancel(GenerationJob job);
    void Retry(GenerationJob job);
}
