using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SavagePadEmu;

public sealed class TestPadView : UserControl
{
    public VirtualTestState State { get; set; } = new();
    public string Language { get; set; } = "es";
    public CalibrationSettings Calibration { get; set; } = new();

    public TestPadView()
    {
        DoubleBuffered = true;
        BackColor = ModernTheme.Surface;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var text = new SolidBrush(ModernTheme.Text);
        using var muted = new SolidBrush(ModernTheme.MutedText);
        using var body = new SolidBrush(Color.FromArgb(235, 241, 250));
        using var outline = new Pen(Color.FromArgb(148, 163, 184), 2);
        using var on = new SolidBrush(ModernTheme.Accent);
        using var off = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var axisPen = new Pen(Color.FromArgb(100, 116, 139), 2);
        using var driftPen = new Pen(Color.FromArgb(245, 158, 11), 1);
        using var deadzonePen = new Pen(Color.FromArgb(34, 197, 94), 1) { DashStyle = DashStyle.Dash };

        var width = ClientSize.Width;
        var height = ClientSize.Height;
        var bodyRect = new Rectangle(Math.Max(30, width / 2 - 240), 70, 480, Math.Max(250, height - 130));
        g.FillEllipse(body, bodyRect);
        g.DrawEllipse(outline, bodyRect);

        DrawStick(g, bodyRect.Left + 145, bodyRect.Top + 145, State.LeftX, State.LeftY,
            Language == "en" ? "Left Stick" : "Stick izquierdo", Calibration.LeftStickDeadzone, axisPen, driftPen, deadzonePen, text, muted,
            State.LeftXRaw, State.LeftYRaw);
        DrawStick(g, bodyRect.Right - 145, bodyRect.Top + 200, State.RightX, State.RightY,
            Language == "en" ? "Right Stick" : "Stick derecho", Calibration.RightStickDeadzone, axisPen, driftPen, deadzonePen, text, muted,
            State.RightXRaw, State.RightYRaw);
        DrawDPad(g, bodyRect.Left + 120, bodyRect.Top + 260, text, on, off, outline);
        DrawFaceButtons(g, bodyRect.Right - 135, bodyRect.Top + 105, text, on, off, outline);
        DrawTrigger(g, bodyRect.Left + 80, bodyRect.Top - 25, State.LeftTrigger, "LT / L2", text, outline);
        DrawTrigger(g, bodyRect.Right - 180, bodyRect.Top - 25, State.RightTrigger, "RT / R2", text, outline);
        DrawSmallButton(g, bodyRect.Left + 210, bodyRect.Top + 210, "Back", IsOn("Back"), text, on, off, outline);
        DrawSmallButton(g, bodyRect.Left + 285, bodyRect.Top + 210, "Start", IsOn("Start"), text, on, off, outline);
        DrawSmallButton(g, bodyRect.Left + 195, bodyRect.Top + 20, "LB/L1", IsOn("LB"), text, on, off, outline);
        DrawSmallButton(g, bodyRect.Right - 265, bodyRect.Top + 20, "RB/R1", IsOn("RB"), text, on, off, outline);
        g.DrawString(Language == "en"
            ? "Green dashed circle = configured deadzone · Orange circle = drift warning center"
            : "Círculo verde punteado = deadzone configurada · Círculo naranja = centro para detectar drift",
            Font, muted, 20, height - 30);
    }

    private bool IsOn(string key) => State.Buttons.TryGetValue(key, out var value) && value;

    private static void DrawStick(Graphics g, int centerX, int centerY, double x, double y, string label, double deadzone, Pen axisPen, Pen driftPen, Pen deadzonePen, Brush text, Brush muted, int rawX, int rawY)
    {
        const int radius = 55;
        g.DrawEllipse(axisPen, centerX - radius, centerY - radius, radius * 2, radius * 2);
        var dzRadius = Math.Max(2, (int)(Math.Clamp(deadzone, 0, 1) * radius));
        g.DrawEllipse(deadzonePen, centerX - dzRadius, centerY - dzRadius, dzRadius * 2, dzRadius * 2);
        g.DrawEllipse(driftPen, centerX - 8, centerY - 8, 16, 16);
        g.DrawLine(axisPen, centerX - radius, centerY, centerX + radius, centerY);
        g.DrawLine(axisPen, centerX, centerY - radius, centerX, centerY + radius);
        var pointX = centerX + (int)(x * radius);
        var pointY = centerY - (int)(y * radius);
        using var knob = new SolidBrush(ModernTheme.Accent);
        g.FillEllipse(knob, pointX - 11, pointY - 11, 22, 22);
        g.DrawEllipse(Pens.Black, pointX - 11, pointY - 11, 22, 22);
        g.DrawString(label, SystemFonts.MessageBoxFont, text, centerX - 42, centerY + radius + 8);
        g.DrawString($"RAW {rawX} / {rawY}", SystemFonts.MessageBoxFont, muted, centerX - 52, centerY + radius + 25);
    }

    private void DrawFaceButtons(Graphics g, int centerX, int centerY, Brush text, Brush on, Brush off, Pen outline)
    {
        DrawRoundButton(g, centerX, centerY - 42, "Y / △", IsOn("Y"), text, on, off, outline);
        DrawRoundButton(g, centerX + 42, centerY, "B / ○", IsOn("B"), text, on, off, outline);
        DrawRoundButton(g, centerX, centerY + 42, "A / ✕", IsOn("A"), text, on, off, outline);
        DrawRoundButton(g, centerX - 42, centerY, "X / □", IsOn("X"), text, on, off, outline);
    }

    private void DrawDPad(Graphics g, int centerX, int centerY, Brush text, Brush on, Brush off, Pen outline)
    {
        DrawSmallButton(g, centerX, centerY - 36, "↑", IsOn("DPadUp"), text, on, off, outline);
        DrawSmallButton(g, centerX + 36, centerY, "→", IsOn("DPadRight"), text, on, off, outline);
        DrawSmallButton(g, centerX, centerY + 36, "↓", IsOn("DPadDown"), text, on, off, outline);
        DrawSmallButton(g, centerX - 36, centerY, "←", IsOn("DPadLeft"), text, on, off, outline);
    }

    private static void DrawRoundButton(Graphics g, int centerX, int centerY, string label, bool active, Brush text, Brush on, Brush off, Pen outline)
    {
        const int radius = 31;
        g.FillEllipse(active ? on : off, centerX - radius, centerY - radius, radius * 2, radius * 2);
        g.DrawEllipse(outline, centerX - radius, centerY - radius, radius * 2, radius * 2);
        var size = g.MeasureString(label, SystemFonts.MessageBoxFont);
        g.DrawString(label, SystemFonts.MessageBoxFont, text, centerX - size.Width / 2, centerY - size.Height / 2);
    }

    private static void DrawSmallButton(Graphics g, int centerX, int centerY, string label, bool active, Brush text, Brush on, Brush off, Pen outline)
    {
        var rect = new Rectangle(centerX - 28, centerY - 14, 56, 28);
        g.FillRectangle(active ? on : off, rect);
        g.DrawRectangle(outline, rect);
        var size = g.MeasureString(label, SystemFonts.MessageBoxFont);
        g.DrawString(label, SystemFonts.MessageBoxFont, text, centerX - size.Width / 2, centerY - size.Height / 2);
    }

    private static void DrawTrigger(Graphics g, int x, int y, int value, string label, Brush text, Pen outline)
    {
        var rect = new Rectangle(x, y, 100, 22);
        g.DrawRectangle(outline, rect);
        using var fill = new SolidBrush(Color.FromArgb(70, 120, 210));
        g.FillRectangle(fill, rect.X + 1, rect.Y + 1, (int)((rect.Width - 2) * (value / 255.0)), rect.Height - 2);
        g.DrawString($"{label}: {value}", SystemFonts.MessageBoxFont, text, x, y - 20);
    }
}
