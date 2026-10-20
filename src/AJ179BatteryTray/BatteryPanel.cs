using System.Drawing.Drawing2D;
using System.Reflection;

namespace AJ179BatteryTray;

internal sealed class BatteryPanel : Form
{
    // Layout is drawn in a 400 x 494 design space and scaled to the DPI-adjusted client size.
    private const int DesignWidth = 400;
    private const int DesignHeight = 494;
    private const int ButtonTop = 412;
    private const int FooterTop = 473;

    private readonly Image _art;
    private readonly GlassButton _refresh;
    private readonly GlassButton _close;
    private readonly Dictionary<(float Size, FontStyle Style), Font> _fonts = new();
    private BatteryReading _reading = BatteryReading.Disconnected("Ожидание первого опроса");
    private int? _percent;
    private bool _busy;
    public event EventHandler? RefreshRequested;
    public DateTime LastHiddenAt { get; private set; }

    public BatteryPanel(bool dismissOnDeactivate = true)
    {
        Text = "AJAZZ · заряд мыши";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(DesignWidth, DesignHeight);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(27, 29, 34);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AJ179BatteryTray.Assets.ajazz-silver.png")!;
        using var source = Image.FromStream(stream);
        _art = GlassBackdrop.Create(source);

        _refresh = CreateButton("Обновить сейчас", new Rectangle(24, ButtonTop, 352, 48));
        _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _close = CreateButton("×", new Rectangle(332, 16, 44, 44));
        _close.AccessibleName = "Закрыть панель";
        _close.Click += (_, _) => HidePanel();
        Controls.AddRange([_refresh, _close]);
        if (dismissOnDeactivate) Deactivate += (_, _) => HidePanel();
        Shown += (_, _) => _refresh.Focus();
    }

    private static GlassButton CreateButton(string text, Rectangle bounds) => new()
    {
        Text = text, Bounds = bounds,
        BackColor = Color.Transparent, ForeColor = Color.White,
        Font = AppFonts.Create(10), Cursor = Cursors.Hand
    };

    internal sealed class GlassButton : Control, IButtonControl
    {
        [System.ComponentModel.DefaultValue(DialogResult.None)]
        public DialogResult DialogResult { get; set; }
        public void NotifyDefault(bool value) => Invalidate();
        public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

        public GlassButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Paint the entire buffer in OnPaint, including invalidations that
            // do not request a separate WM_ERASEBKGND pass.
        }

        private void PaintParentBackground(Graphics graphics)
        {
            graphics.Clear(Parent?.BackColor ?? Color.FromArgb(27,29,34));
            if (Parent is not BatteryPanel panel) return;
            var state = graphics.Save();
            graphics.TranslateTransform(-Left, -Top);
            panel.PaintBackdrop(graphics);
            graphics.Restore(state);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            PaintParentBackground(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Text == "×")
            {
                var scale = Width / 44f;
                var cx = Width / 2f; var cy = Height / 2f;
                using var shadow = new Pen(Color.FromArgb(80, Color.Black), 4 * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                using var stroke = new Pen(Color.FromArgb(Focused ? 255 : 220, Color.White), 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var half = 5 * scale;
                g.DrawLine(shadow, cx-half, cy-half+scale, cx+half, cy+half+scale);
                g.DrawLine(shadow, cx-half, cy+half+scale, cx+half, cy-half+scale);
                g.DrawLine(stroke, cx-half, cy-half, cx+half, cy+half);
                g.DrawLine(stroke, cx-half, cy+half, cx+half, cy-half);
                if (Focused && ShowFocusCues)
                {
                    using var focus = new Pen(Color.FromArgb(100,Color.White),1);
                    g.DrawEllipse(focus, Width/2f-13*scale, Height/2f-13*scale, 26*scale, 26*scale);
                }
                return;
            }
            var bounds = new RectangleF(1, 1, Width - 3, Height - 3);
            using var path = Round(bounds, 15 * Width / 352f);
            var hovered = ClientRectangle.Contains(PointToClient(Cursor.Position));
            using var fill = new LinearGradientBrush(bounds,
                Color.FromArgb(hovered ? 112 : 89, hovered ? 116 : 93, hovered ? 124 : 103),
                Color.FromArgb(42, 45, 53), 90f);
            using var edge = new Pen(Color.FromArgb(Focused ? 235 : 145, 230, 233, 240), 1);
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
            using var textBrush = new SolidBrush(Enabled ? Color.White : Color.Silver);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var buttonFont = AppFonts.Create(10 * Width / 352f);
            g.DrawString(Text, buttonFont, textBrush, ClientRectangle, format);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button == MouseButtons.Left) Focus(); base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.KeyCode is Keys.Enter or Keys.Space) { e.Handled = true; e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if(e.KeyCode is Keys.Enter or Keys.Space) { PerformClick(); e.Handled = true; }
            base.OnKeyUp(e);
        }
        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter or Keys.Space || base.IsInputKey(keyData);
    }

    public void SetReading(BatteryReading reading, int? shownPercent, bool busy)
    {
        _reading = reading;
        _percent = shownPercent;
        _busy = busy;
        _refresh.Enabled = !busy;
        _refresh.Text = busy ? "Проверяем заряд…" : "Обновить сейчас";
        AccessibleDescription = $"Заряд: {shownPercent?.ToString() ?? "неизвестен"}. {StatusText()}";
        Invalidate();
    }

    public void ShowAt(Point anchor)
    {
        var area = Screen.FromPoint(anchor).WorkingArea;
        var margin = 12;
        Location = new Point(Math.Clamp(anchor.X - Width + 22, area.Left + margin, Math.Max(area.Left + margin, area.Right - Width - margin)),
            Math.Clamp(anchor.Y - Height - margin, area.Top + margin, Math.Max(area.Top + margin, area.Bottom - Height - margin)));
        Show();
        Activate();
    }

    public void HidePanel()
    {
        if (!Visible) return;
        LastHiddenAt = DateTime.UtcNow;
        Hide();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { HidePanel(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (ClientSize.Width <= 0) return;
        using var path = Round(new RectangleF(0, 0, Width, Height), 24f * ClientSize.Width / 400f);
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
        if (_refresh is not null)
        {
            var sx = ClientSize.Width / (float)DesignWidth; var sy = ClientSize.Height / (float)DesignHeight;
            _refresh.Bounds = new Rectangle((int)(24*sx),(int)(ButtonTop*sy),(int)(352*sx),(int)(48*sy));
            _close.Bounds = new Rectangle((int)(332*sx),(int)(16*sy),(int)(44*sx),(int)(44*sy));
        }
    }

    private void PaintBackdrop(Graphics graphics)
    {
        var state = graphics.Save();
        graphics.ScaleTransform(ClientSize.Width / (float)DesignWidth, ClientSize.Height / (float)DesignHeight);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (_art is not null) graphics.DrawImage(_art, new RectangleF(-4, -4, DesignWidth + 8, DesignHeight + 8));
        graphics.Restore(state);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        PaintBackdrop(g);
        g.ScaleTransform(ClientSize.Width / (float)DesignWidth, ClientSize.Height / (float)DesignHeight);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var border = new Pen(Color.FromArgb(115, 235, 238, 246), 1);
        using var outer = Round(new RectangleF(0.8f, 0.8f, DesignWidth - 1.6f, DesignHeight - 1.6f), 24);
        g.DrawPath(border, outer);

        Label(g, "A J A Z Z", 24, 23, 14, FontStyle.Bold);
        Label(g, "PLAY MORE · WITH YOU", 24, 50, 7, FontStyle.Regular, 175);
        Label(g, "AJ179 PRO", 24, 88, 22, FontStyle.Bold);
        Label(g, "WIRELESS / BATTERY", 26, 128, 8, FontStyle.Regular, 180);

        // Unknown charge is "--", placed on the same baseline as real digits (aligned by the
        // top of a reference "0"), so it sits where the number would be instead of floating.
        var number = _percent?.ToString() ?? "--";
        using (var digits = new GraphicsPath())
        using (var reference = new GraphicsPath())
        {
            digits.AddString(number, AppFonts.Family, (int)FontStyle.Bold, 80, PointF.Empty, StringFormat.GenericTypographic);
            reference.AddString("0", AppFonts.Family, (int)FontStyle.Bold, 80, PointF.Empty, StringFormat.GenericTypographic);
            var bounds = digits.GetBounds();
            using var move = new Matrix();
            move.Translate(26-bounds.Left, 168-reference.GetBounds().Top);
            digits.Transform(move);
            using var silver = new LinearGradientBrush(new RectangleF(24,168,250,68),Color.White,Color.FromArgb(183,183,183),90f);
            g.FillPath(silver,digits);
            Label(g, "%", 26+bounds.Width+14, 194, 22, FontStyle.Regular, 210);
        }
        Label(g, "УРОВЕНЬ ЗАРЯДА", 26, 249, 8, FontStyle.Regular, 185);

        GlassCard(g, new RectangleF(24, 289, 352, 99));
        Label(g, StatusText(), 40, 303, 10, FontStyle.Bold);
        var subline = _reading.Percent is not null ? (_reading.OnCable ? "AJ179 PRO / USB-кабель" : "AJ179 PRO / 2.4G")
            : _percent.HasValue ? "Последнее известное значение"
            : _reading.OnCable ? "По кабелю заряд не передаётся" : "AJ179 PRO / 2.4G";
        Label(g, subline, 40, 328, 8, FontStyle.Regular, 180);
        var track = new RectangleF(40, 359, 320, 9);
        using var trackPath = Round(track, track.Height / 2);
        using var trackBrush = new SolidBrush(Color.FromArgb(110, 9, 10, 14));
        g.FillPath(trackBrush, trackPath);
        if (_percent is > 0)
        {
            var saved = g.Save();
            g.SetClip(trackPath);
            var fillWidth = 320f * Math.Clamp(_percent.Value, 0, 100) / 100;
            using var fill = new LinearGradientBrush(track, Color.FromArgb(251, 252, 255), Color.FromArgb(133, 140, 157), 90f);
            g.FillRectangle(fill, 40, 359, fillWidth, 9);
            using var shine = new Pen(Color.FromArgb(210, Color.White), 1);
            g.DrawLine(shine, 42, 361, 40 + fillWidth, 361);
            g.Restore(saved);
        }
        g.DrawPath(border, trackPath);
        Label(g, "AJ179 / SILVER EDITION", 26, FooterTop, 7, FontStyle.Regular, 145);
        Label(g, "ESC · ЗАКРЫТЬ", 278, FooterTop, 7, FontStyle.Regular, 145);
    }

    private string StatusText() => _reading.OnCable ? "Подключена по кабелю" :
        !_reading.ReceiverConnected ? "Приёмник не подключён" :
        _reading.Percent is null ? (_percent.HasValue ? "Ожидание обновления" : "Заряд пока неизвестен") :
        _reading.MouseOnline ? "Мышь на связи" : "Мышь спит";

    private void Label(Graphics g, string text, float x, float y, float size, FontStyle style, int brightness = 244)
    {
        using var brush = new SolidBrush(Color.FromArgb(brightness, brightness, Math.Min(255, brightness + 4)));
        g.DrawString(text, GetFont(size, style), brush, x, y);
    }

    // The panel repaints often while open; reuse the handful of fonts it needs instead of creating one per label.
    private Font GetFont(float size, FontStyle style)
    {
        if (!_fonts.TryGetValue((size, style), out var font))
        {
            font = AppFonts.Create(size, style);
            _fonts[(size, style)] = font;
        }
        return font;
    }

    private static void GlassCard(Graphics g, RectangleF bounds)
    {
        using var path = Round(bounds, 20);
        using var fill = new LinearGradientBrush(bounds, Color.FromArgb(65, 227, 232, 244), Color.FromArgb(12, 227, 232, 244), 95f);
        using var edge = new Pen(Color.FromArgb(90, 232, 236, 245), 1);
        g.FillPath(fill, path);
        g.DrawPath(edge, path);
    }

    private static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); var d = radius * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90); p.AddArc(r.Right-d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.Left, r.Bottom-d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _art.Dispose(); _refresh.Font.Dispose(); _close.Font.Dispose();
            foreach (var font in _fonts.Values) font.Dispose();
            _fonts.Clear();
        }
        base.Dispose(disposing);
    }
}
