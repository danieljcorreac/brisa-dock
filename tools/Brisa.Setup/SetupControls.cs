using System.Drawing.Drawing2D;

namespace Brisa.Setup;

internal static class SetupColors
{
    public static Color Background => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(38, 54, 74);
    public static Color Surface => SystemInformation.HighContrast ? SystemColors.Control : Color.FromArgb(48, 65, 85);
    public static Color Input => SystemInformation.HighContrast ? SystemColors.Window : Color.FromArgb(35, 49, 68);
    public static Color Text => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(241, 243, 250);
    public static Color Muted => SystemInformation.HighContrast ? SystemColors.GrayText : Color.FromArgb(176, 189, 207);
    public static Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(193, 218, 207);
    public static Color OnAccent => SystemInformation.HighContrast ? SystemColors.HighlightText : Input;
    public static Color Error => SystemInformation.HighContrast ? SystemColors.WindowText : Color.FromArgb(242, 169, 149);
    public static Color Behind(Control? parent)
    {
        for (var current = parent; current != null; current = current.Parent)
        {
            if (current is RoundedPanel panel) return panel.FillColor;
            if (current.BackColor.A == 255) return current.BackColor;
        }
        return Background;
    }
    public static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        float diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }
}

internal sealed class HeaderLabel : Label
{
    protected override void OnPaint(PaintEventArgs e)
    {
        // Label's default glyph-overhang padding grows with the font size, which
        // shifts the large heading to the right of the smaller caption below it.
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.Top);
    }
}

internal sealed class RoundedPanel : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = SetupColors.Surface;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 16;
    public RoundedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(SetupColors.Behind(Parent));
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(SetupColors.Behind(Parent)); base.OnPaint(e); if (Width < 2 || Height < 2) return;
        e.Graphics.Clear(SetupColors.Behind(Parent));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = SetupColors.Rounded(new(.5f, .5f, Width - 1, Height - 1), Radius * DeviceDpi / 96f);
        using var fill = new SolidBrush(FillColor); e.Graphics.FillPath(fill, shape);
        using var border = new Pen(Color.FromArgb(18, 255, 255, 255)); e.Graphics.DrawPath(border, shape);
    }
}

internal sealed class RoundedButton : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    bool hovered, pressed;
    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; BackColor = Color.Transparent;
        Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { pressed = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(SetupColors.Behind(Parent));
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 2 || Height < 2) return;
        e.Graphics.Clear(SetupColors.Behind(Parent));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Primary ? SetupColors.Accent : SetupColors.Surface;
        if (Enabled && (hovered || pressed)) fill = ControlPaint.Light(fill, pressed ? .02f : .12f);
        if (!Enabled) fill = SetupColors.Surface;
        using var shape = SetupColors.Rounded(new(.5f, .5f, Width - 1, Height - 1), 12 * DeviceDpi / 96f);
        using var brush = new SolidBrush(fill); e.Graphics.FillPath(brush, shape);
        if (Focused && ShowFocusCues)
        {
            float inset = 3 * DeviceDpi / 96f;
            using var focus = SetupColors.Rounded(new(inset, inset, Width - inset * 2, Height - inset * 2), 9 * DeviceDpi / 96f);
            using var pen = new Pen(Primary ? SetupColors.Input : SetupColors.Accent, 1.5f); e.Graphics.DrawPath(pen, focus);
        }
        using var text = new SolidBrush(!Enabled ? SetupColors.Muted : Primary ? SetupColors.OnAccent : SetupColors.Text);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        e.Graphics.DrawString(Text, Font, text, ClientRectangle, format);
    }
}

internal sealed class SetupCheckBox : CheckBox
{
    public SetupCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent; Dock = DockStyle.Fill; AutoSize = false; Cursor = Cursors.Hand; Margin = new Padding(0);
        CheckedChanged += (_, _) => Invalidate();
    }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(SetupColors.Behind(Parent));
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(SetupColors.Behind(Parent));
        float scale = DeviceDpi / 96f, side = 19 * scale, x = 4 * scale, y = (Height - side) / 2;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = SetupColors.Rounded(new(x, y, side, side), 5 * scale);
        using var fill = new SolidBrush(Checked && Enabled ? SetupColors.Accent : SetupColors.Input); e.Graphics.FillPath(fill, shape);
        using var border = new Pen(Focused && ShowFocusCues ? SetupColors.Accent : SetupColors.Muted); e.Graphics.DrawPath(border, shape);
        if (Checked)
        {
            using var check = new Pen(Enabled ? SetupColors.OnAccent : SetupColors.Muted, 2 * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            e.Graphics.DrawLines(check, new[] { new PointF(x + side * .24f, y + side * .51f), new PointF(x + side * .43f, y + side * .7f), new PointF(x + side * .77f, y + side * .32f) });
        }
        var text = new Rectangle((int)(x + side + 12 * scale), 0, Math.Max(1, Width - (int)(x + side + 14 * scale)), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? SetupColors.Text : SetupColors.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

internal sealed class SetupProgress : Control
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    float phase;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Busy { set { int animate = 1; SystemParametersInfo(0x1042, 0, ref animate, 0); if (value && animate != 0) timer.Start(); else { timer.Stop(); phase = .65f; } Invalidate(); } }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint parameter, ref int value, uint flags);
    public SetupProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AccessibleRole = AccessibleRole.ProgressBar; AccessibleName = "Progreso de instalación";
        BackColor = SetupColors.Surface; timer.Tick += (_, _) => { phase = (phase + .025f) % 1; Invalidate(); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = new SolidBrush(SetupColors.Input); e.Graphics.FillRectangle(track, ClientRectangle);
        using var accent = new SolidBrush(SetupColors.Accent); e.Graphics.FillRectangle(accent, phase * Width - Width * .3f, 0, Width * .3f, Height);
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
