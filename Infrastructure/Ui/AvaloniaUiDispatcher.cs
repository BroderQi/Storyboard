using Storyboard.Application.Abstractions;

namespace Storyboard.Infrastructure.Ui;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Avalonia.Threading.Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }
}
