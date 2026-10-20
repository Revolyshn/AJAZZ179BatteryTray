namespace AJ179BatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string AutostartText = "Запускать с Windows";

    private readonly Aj179BatteryReader _reader;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly System.Windows.Forms.Timer _timer;
    // One-shot timer for event-driven polls (device change, resume); restarting it debounces bursts.
    private readonly System.Windows.Forms.Timer _soonTimer;
    private readonly SynchronizationContext _uiContext;
    private readonly EventHandler<HidSharp.DeviceListChangedEventArgs> _deviceListChanged;
    private Icon? _currentIcon;
    private int? _currentIconPercent;
    private bool _refreshing;
    private bool _refreshPending;
    private bool _firstReadingLogged;
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
        // Creating the first Control installs the WinForms synchronization context.
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("WinForms synchronization context is not installed.");
        _panel.RefreshRequested += async (_, _) => await RefreshAsync();
        _panel.VisibleChanged += (_, _) => OnPanelVisibleChanged();
        _statusItem = new ToolStripMenuItem("AJ179 PRO: чтение…") { Enabled = false };
        _autostartItem = new ToolStripMenuItem(AutostartText);
        _autostartItem.Click += OnAutostartClick;
        TryRepairAutostart();
        UpdateAutostartItem();

        var refreshItem = new ToolStripMenuItem("Обновить сейчас");
        refreshItem.Click += async (_, _) => await RefreshAsync();
        var logItem = new ToolStripMenuItem("Открыть журнал");
        logItem.Click += (_, _) => OpenLog();
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_statusItem, new ToolStripSeparator(), refreshItem, _autostartItem, logItem,
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

        _timer = new System.Windows.Forms.Timer { Interval = (int)PollingPolicy.WaitingForTelemetry.TotalMilliseconds, Enabled = true };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _soonTimer = new System.Windows.Forms.Timer();
        _soonTimer.Tick += async (_, _) => { _soonTimer.Stop(); await RefreshAsync(); };

        // Both events arrive on background threads.
        _deviceListChanged = (_, _) =>
        {
            AppLog.Info("Device list changed");
            Post(() => RefreshSoon(PollingPolicy.DeviceChangeSettle));
        };
        HidSharp.DeviceList.Local.Changed += _deviceListChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;

        AppLog.Info($"Start {Application.ProductVersion}, {Environment.ProcessPath}");
        // First async read starts on the established WinForms message loop.
        EventHandler? start = null;
        start = async (_, _) => { Application.Idle -= start; await RefreshAsync(); };
        Application.Idle += start;
    }

    private void Post(Action action)
    {
        try
        {
            _uiContext.Post(_ => { if (!_exiting) action(); }, null);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
        {
            // The message loop is already shutting down.
        }
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
        {
            AppLog.Info("System suspend");
        }
        else if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            AppLog.Info("System resume");
            Post(() => RefreshSoon(PollingPolicy.ResumeSettle));
        }
    }

    /// <summary>Polls after <paramref name="delay"/>; repeated requests within the delay coalesce.</summary>
    private void RefreshSoon(TimeSpan delay)
    {
        _soonTimer.Stop();
        _soonTimer.Interval = Math.Max(1, (int)delay.TotalMilliseconds);
        _soonTimer.Start();
    }

    private void OnPanelVisibleChanged()
    {
        if (_exiting) return;
        // Opening the panel shows fresh data right away when the last poll is older than the panel interval.
        if (_panel.Visible && (_checkedAt is null || DateTime.Now - _checkedAt.Value >= PollingPolicy.PanelOpen))
        {
            RefreshSoon(TimeSpan.Zero);
        }
        ScheduleNextPoll();
    }

    private void ScheduleNextPoll()
    {
        var delay = PollingPolicy.NextDelay(_panel.Visible, _latestReading.ReceiverConnected, _lastKnownPercent is not null);
        var interval = (int)delay.TotalMilliseconds;
        // Setting Interval restarts the timer, so leave it alone when nothing changed.
        if (_timer.Interval != interval) _timer.Interval = interval;
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
        if (_exiting) return;
        if (_refreshing)
        {
            // A device change or resume during a poll must not be lost: poll again once this one ends.
            _refreshPending = true;
            return;
        }
        _refreshing = true;
        _refreshPending = false;
        UpdatePanel();
        BatteryReading reading;
        try
        {
            reading = await _reader.ReadAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Read failed", ex);
            reading = BatteryReading.Disconnected($"Не удалось прочитать заряд: {ex.Message}");
        }

        try
        {
            if (!_exiting)
            {
                LogTransition(_latestReading, reading);
                ApplyReading(reading);
            }
        }
        finally
        {
            _refreshing = false;
            if (!_exiting)
            {
                UpdatePanel();
                ScheduleNextPoll();
                if (_refreshPending) RefreshSoon(TimeSpan.Zero);
            }
        }
    }

    private void LogTransition(BatteryReading previous, BatteryReading next)
    {
        // Log only changes: a steady state produces no lines however often it is polled.
        if (_firstReadingLogged
            && previous.ReceiverConnected == next.ReceiverConnected
            && previous.MouseOnline == next.MouseOnline
            && previous.Percent == next.Percent
            && previous.Detail == next.Detail)
        {
            return;
        }
        _firstReadingLogged = true;
        AppLog.Info($"Receiver={(next.ReceiverConnected ? "on" : "off")} Mouse={(next.MouseOnline ? "online" : "offline")} " +
            $"Percent={next.Percent?.ToString() ?? "-"} Rev={next.FirmwareRevision} Detail=\"{next.Detail}\"");
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

    private static void OpenLog()
    {
        try
        {
            AppLog.Info("Log opened from the tray menu");
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{AppLog.FilePath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть журнал:\n{ex.Message}\n\n{AppLog.FilePath}", "AJ179 Battery Tray",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        AppLog.Info("Exit");
        // Static events would otherwise keep this context alive and call into disposed controls.
        HidSharp.DeviceList.Local.Changed -= _deviceListChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _panel.Dispose();
        _timer.Stop();
        _timer.Dispose();
        _soonTimer.Stop();
        _soonTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        base.ExitThreadCore();
    }
}
