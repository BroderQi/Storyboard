namespace Storyboard.Application.Abstractions;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
}
