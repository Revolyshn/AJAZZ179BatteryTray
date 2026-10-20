namespace AJ179BatteryTray;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\AJ179BatteryTray", out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        var context = new TrayApplicationContext(new Aj179BatteryReader());
        if (args.Contains("--panel"))
        {
            EventHandler? show = null;
            show = (_, _) => { Application.Idle -= show; context.TogglePanel(); };
            Application.Idle += show;
        }
        Application.Run(context);
    }
}
