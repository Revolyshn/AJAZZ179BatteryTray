namespace AJ179BatteryTray;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SingleInstance.SignalShowPanel();
            return;
        }

        ApplicationConfiguration.Initialize();
        var context = new TrayApplicationContext(new Aj179BatteryReader());
        // BatteryPanel is a Control, so WinForms has installed its synchronization context by now.
        instance.ListenForShowPanel(SynchronizationContext.Current!, context.ShowPanel);
        if (args.Contains("--panel"))
        {
            EventHandler? show = null;
            show = (_, _) => { Application.Idle -= show; context.ShowPanel(); };
            Application.Idle += show;
        }
        Application.Run(context);
    }
}
