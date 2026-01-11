namespace Storyboard.Models;

public enum GenerationJobStatus
{
    Queued,
    Running,
    Retrying,
    Succeeded,
    Failed,
    Canceled
}
