namespace AJ179BatteryTray;

/// <summary>
/// Keeps one tray instance per session. A repeated launch signals the running
/// instance to open its panel instead of exiting silently.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\AJ179BatteryTray";
    private const string ShowPanelEventName = @"Local\AJ179BatteryTray.ShowPanel";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showPanel;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle showPanel)
    {
        _mutex = mutex;
        _showPanel = showPanel;
    }

    /// <summary>Returns null when another instance already owns the session.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(mutex, CreateEvent());
    }

    public static void SignalShowPanel()
    {
        using var showPanel = CreateEvent();
        showPanel.Set();
    }

    /// <summary>Invokes <paramref name="onShowPanel"/> on the captured UI context for every signal.</summary>
    public void ListenForShowPanel(SynchronizationContext uiContext, Action onShowPanel)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(_showPanel,
            (_, _) => Post(uiContext, onShowPanel),
            null, Timeout.Infinite, executeOnlyOnce: false);
    }

    private static void Post(SynchronizationContext uiContext, Action action)
    {
        try
        {
            uiContext.Post(_ => action(), null);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // The message loop is already shutting down.
        }
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showPanel.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    private static EventWaitHandle CreateEvent() =>
        new(false, EventResetMode.AutoReset, ShowPanelEventName);
}
