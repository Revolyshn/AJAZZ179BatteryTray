namespace AJ179BatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string AutostartText = "Запускать с Windows";

    private readonly Aj179BatteryReader _reader;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly System.Windows.Forms.Timer _timer;
    private Icon? _currentIcon;
    private int? _currentIconPercent;
    private bool _refreshing;
    private bool _lowBatteryNotified;
    private int? _lastKnownPercent;
    private readonly BatteryPanel _panel;
    private BatteryReading _latestReading = BatteryReading.Disconnected();
    private DateTime? _checkedAt;
    private bool _exiting;

    public TrayApplicationContext(Aj179BatteryReader reader)
    {
        _reader = reader;
        _panel = new BatteryPanel();
        _panel.RefreshRequested += async (_, _) => await RefreshAsync();
        _statusItem = new ToolStripMenuItem("AJ179 PRO: чтение…") { Enabled = false };
        _autostartItem = new ToolStripMenuItem(AutostartText);
        _autostartItem.Click += OnAutostartClick;
        TryRepairAutostart();
        UpdateAutostartItem();

        var refreshItem = new ToolStripMenuItem("Обновить сейчас");
        refreshItem.Click += async (_, _) => await RefreshAsync();
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_statusItem, new ToolStripSeparator(), refreshItem, _autostartItem,
            new ToolStripSeparator(), exitItem]);
        // Another copy may have changed the Run entry since the menu was last shown.
        menu.Opening += (_, _) => UpdateAutostartItem();

        _currentIcon = TrayIconRenderer.Create(null, false, false);
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "AJ179 PRO — чтение заряда…",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) TogglePanel();
        };

        _timer = new System.Windows.Forms.Timer { Interval = 5_000, Enabled = true };
        _timer.Tick += async (_, _) => await RefreshAsync();
        // First async read starts on the established WinForms message loop.
        EventHandler? start = null;
        start = async (_, _) => { Application.Idle -= start; await RefreshAsync(); };
        Application.Idle += start;
    }

    public void TogglePanel()
    {
        if (_panel.Visible) { _panel.HidePanel(); return; }
        if ((DateTime.UtcNow - _panel.LastHiddenAt).TotalMilliseconds < 250) return;
        _panel.ShowAt(Cursor.Position);
    }

    /// <summary>Opens the panel or brings it to the front; used by --panel and repeated launches.</summary>
    public void ShowPanel()
    {
        if (_exiting) return;
        if (_panel.Visible)
        {
            _panel.Activate();
            return;
        }
        _panel.ShowAt(Cursor.Position);
    }

    private void UpdatePanel() => _panel.SetReading(_latestReading,
        _latestReading.Percent ?? (_latestReading.ReceiverConnected ? _lastKnownPercent : null), _checkedAt, _refreshing);

    private async Task RefreshAsync()
    {
        if (_refreshing || _exiting) return;
        _refreshing = true;
        UpdatePanel();
        BatteryReading reading;
        try
        {
            reading = await _reader.ReadAsync();
        }
        catch (Exception ex)
        {
            reading = BatteryReading.Disconnected($"Не удалось прочитать заряд: {ex.Message}");
        }

        try
        {
            if (!_exiting) ApplyReading(reading);
        }
        finally
        {
            _refreshing = false;
            if (!_exiting) UpdatePanel();
        }
    }

    private void ApplyReading(BatteryReading reading)
    {
        _latestReading = reading;
        _checkedAt = DateTime.Now;
        if (reading.Percent is not null)
        {
            _lastKnownPercent = reading.Percent;
        }

        var shownPercent = reading.Percent ?? (reading.ReceiverConnected ? _lastKnownPercent : null);
        var suffix = reading.Percent is null && shownPercent is not null ? " (последнее)" : string.Empty;
        _statusItem.Text = shownPercent is null
            ? $"AJ179 PRO: {(reading.ReceiverConnected ? "нет данных" : "не подключена")}"
            : $"AJ179 PRO: {shownPercent}%{suffix}";

        var tooltip = shownPercent is null
            ? $"AJ179 PRO: {(reading.ReceiverConnected ? "пошевелите мышь" : "приёмник не найден")}"
            : $"AJ179 PRO: {shownPercent}%{suffix}";
        _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];

        // The icon shows only the percentage, so it is redrawn only when that changes.
        if (shownPercent != _currentIconPercent)
        {
            var nextIcon = TrayIconRenderer.Create(shownPercent, reading.ReceiverConnected, reading.MouseOnline);
            var oldIcon = _currentIcon;
            _currentIcon = nextIcon;
            _currentIconPercent = shownPercent;
            _notifyIcon.Icon = nextIcon;
            oldIcon?.Dispose();
        }

        if (reading.MouseOnline && shownPercent is <= 20 && !_lowBatteryNotified)
        {
            _notifyIcon.ShowBalloonTip(5_000, "AJ179 PRO", $"Низкий заряд: {shownPercent}%", ToolTipIcon.Warning);
            _lowBatteryNotified = true;
        }
        else if (shownPercent is > 25)
        {
            _lowBatteryNotified = false;
        }
    }

    private static void TryRepairAutostart()
    {
        try
        {
            AutostartManager.RepairIfStale();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // The menu item shows the remaining stale state; the user can retry from there.
        }
    }

    private void UpdateAutostartItem()
    {
        var state = SafeAutostartState();
        _autostartItem.Checked = state == AutostartState.Enabled;
        _autostartItem.Text = state switch
        {
            AutostartState.Stale => $"{AutostartText} (путь устарел)",
            AutostartState.OtherCopy => $"{AutostartText} (другая копия)",
            _ => AutostartText
        };
        _autostartItem.ToolTipText = state switch
        {
            AutostartState.Stale => "Запись автозапуска указывает на удалённый файл. Нажмите, чтобы исправить.",
            AutostartState.OtherCopy => "Автозапуск настроен на другую копию программы. Нажмите, чтобы использовать эту.",
            _ => null
        };
    }

    private static AutostartState SafeAutostartState()
    {
        try
        {
            return AutostartManager.GetState();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return AutostartState.Disabled;
        }
    }

    private void OnAutostartClick(object? sender, EventArgs e)
    {
        try
        {
            // Stale or foreign entries are retargeted at this copy; only a matching entry is removed.
            AutostartManager.SetEnabled(SafeAutostartState() != AutostartState.Enabled);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось изменить автозапуск:\n{ex.Message}", "AJ179 Battery Tray",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        UpdateAutostartItem();
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        _panel.Dispose();
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        base.ExitThreadCore();
    }
}
