namespace AJ179BatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Aj179BatteryReader _reader;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly System.Windows.Forms.Timer _timer;
    private Icon? _currentIcon;
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
        _autostartItem = new ToolStripMenuItem("Запускать с Windows")
        {
            Checked = AutostartManager.IsEnabled(),
            CheckOnClick = true
        };
        _autostartItem.CheckedChanged += OnAutostartChanged;

        var refreshItem = new ToolStripMenuItem("Обновить сейчас");
        refreshItem.Click += async (_, _) => await RefreshAsync();
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_statusItem, new ToolStripSeparator(), refreshItem, _autostartItem,
            new ToolStripSeparator(), exitItem]);

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

    private void UpdatePanel() => _panel.SetReading(_latestReading,
        _latestReading.Percent ?? (_latestReading.ReceiverConnected ? _lastKnownPercent : null), _checkedAt, _refreshing);

    private async Task RefreshAsync()
    {
        if (_refreshing || _exiting) return;
        _refreshing = true;
        UpdatePanel();
        try
        {
            var reading = await _reader.ReadAsync();
            if (_exiting) return;
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

            var nextIcon = TrayIconRenderer.Create(shownPercent, reading.ReceiverConnected, reading.MouseOnline);
            var oldIcon = _currentIcon;
            _currentIcon = nextIcon;
            _notifyIcon.Icon = nextIcon;
            oldIcon?.Dispose();

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
        catch (Exception ex)
        {
            if (!_exiting) _latestReading = BatteryReading.Disconnected($"Не удалось прочитать заряд: {ex.Message}");
        }
        finally
        {
            _refreshing = false;
            if (!_exiting) UpdatePanel();
        }
    }

    private void OnAutostartChanged(object? sender, EventArgs e)
    {
        try
        {
            AutostartManager.SetEnabled(_autostartItem.Checked);
        }
        catch (Exception ex)
        {
            _autostartItem.CheckedChanged -= OnAutostartChanged;
            _autostartItem.Checked = AutostartManager.IsEnabled();
            _autostartItem.CheckedChanged += OnAutostartChanged;
            MessageBox.Show($"Не удалось изменить автозапуск:\n{ex.Message}", "AJ179 Battery Tray",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
