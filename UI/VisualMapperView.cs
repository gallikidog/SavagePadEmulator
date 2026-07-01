using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SavagePadEmu;

public sealed class VisualMapperView : Control
{
    private readonly Dictionary<string, Rectangle> _hotspots = new();
    private readonly Dictionary<string, string> _bindingText = new();

    public event EventHandler<string>? BindRequested;
    public VirtualTestState State { get; set; } = new();
    public string Language { get; set; } = "es";

    public VisualMapperView()
    {
        DoubleBuffered = true;
        BackColor = ModernTheme.Surface;
        MinimumSize = new Size(430, 350);
        Cursor = Cursors.Hand;
    }

    public void SetBindings(IEnumerable<Binding> bindings)
    {
        _bindingText.Clear();
        foreach (var binding in bindings) _bindingText[binding.Target] = Describe(binding);
        Invalidate();
    }

    private static string Describe(Binding b) => b.Kind switch
    {
        SourceKind.Button => $"Button {b.Index + 1}",
        SourceKind.Axis => $"Axis {b.Index}{(b.Invert ? "-" : "+")}",
        SourceKind.Pov => b.Index switch { 0 => "D-Pad ↑", 1 => "D-Pad →", 2 => "D-Pad ↓", 3 => "D-Pad ←", _ => "D-Pad" },
        _ => "—"
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        _hotspots.Clear();
        using var text = new SolidBrush(ModernTheme.Text);
        using var muted = new SolidBrush(ModernTheme.MutedText);
        using var outline = new Pen(Color.FromArgb(148, 163, 184), 2);
        using var body = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var active = new SolidBrush(ModernTheme.Accent);
        using var inactive = new SolidBrush(Color.White);

        var w = ClientSize.Width; var h = ClientSize.Height;
        var scale = Math.Max(.72f, Math.Min(w / 560f, h / 410f));
        var ox = (w - 560 * scale) / 2f; var oy = (h - 400 * scale) / 2f;
        Rectangle R(float x, float y, float width, float height) => Rectangle.Round(new RectangleF(ox + x * scale, oy + y * scale, width * scale, height * scale));
        Point P(float x, float y) => Point.Round(new PointF(ox + x * scale, oy + y * scale));

        var main = R(55, 65, 450, 270);
        g.FillEllipse(body, main); g.DrawEllipse(outline, main);

        DrawStick(g, "LeftStickX", R(128,172,88,88), State.LeftX, State.LeftY, "LS / L3", text, muted, outline, active, inactive);
        DrawStick(g, "RightStickX", R(336,198,88,88), State.RightX, State.RightY, "RS / R3", text, muted, outline, active, inactive);
        DrawButton(g, "Y", R(425,126,42,42), "Y / △", IsOn("Y"), text, outline, active, inactive);
        DrawButton(g, "B", R(466,167,42,42), "B / ○", IsOn("B"), text, outline, active, inactive);
        DrawButton(g, "A", R(425,208,42,42), "A / ✕", IsOn("A"), text, outline, active, inactive);
        DrawButton(g, "X", R(384,167,42,42), "X / □", IsOn("X"), text, outline, active, inactive);
        DrawButton(g, "DPadUp", R(96,235,34,34), "↑", IsOn("DPadUp"), text, outline, active, inactive);
        DrawButton(g, "DPadRight", R(130,269,34,34), "→", IsOn("DPadRight"), text, outline, active, inactive);
        DrawButton(g, "DPadDown", R(96,303,34,34), "↓", IsOn("DPadDown"), text, outline, active, inactive);
        DrawButton(g, "DPadLeft", R(62,269,34,34), "←", IsOn("DPadLeft"), text, outline, active, inactive);
        DrawPill(g, "LB", R(95,43,115,28), "LB / L1", IsOn("LB"), text, outline, active, inactive);
        DrawPill(g, "RB", R(350,43,115,28), "RB / R1", IsOn("RB"), text, outline, active, inactive);
        DrawTrigger(g, "LeftTrigger", R(75,8,110,26), State.LeftTrigger, "LT / L2", text, outline, active);
        DrawTrigger(g, "RightTrigger", R(375,8,110,26), State.RightTrigger, "RT / R2", text, outline, active);
        DrawPill(g, "Back", R(224,171,45,25), Language == "en" ? "Back" : "Share", IsOn("Back"), text, outline, active, inactive);
        DrawPill(g, "Start", R(291,171,45,25), Language == "en" ? "Start" : "Options", IsOn("Start"), text, outline, active, inactive);

        using var titleFont = new Font("Segoe UI Semibold", Math.Max(9, 10 * scale));
        g.DrawString(Language == "en" ? "Click a control to bind it" : "Hacé clic en un control para bindearlo", titleFont, muted, P(155,355));
        foreach (var pair in _hotspots)
            if (_bindingText.TryGetValue(pair.Key, out var value) && value != "—")
            {
                var size = g.MeasureString(value, Font);
                g.DrawString(value, Font, muted, pair.Value.Left + (pair.Value.Width-size.Width)/2f, pair.Value.Bottom+2);
            }
    }

    private bool IsOn(string target) => State.Buttons.TryGetValue(target, out var on) && on;
    private void DrawStick(Graphics g, string target, Rectangle rect, double x, double y, string label, Brush text, Brush muted, Pen outline, Brush active, Brush inactive)
    {
        _hotspots[target]=rect; g.FillEllipse(inactive,rect); g.DrawEllipse(outline,rect);
        var cx=rect.Left+rect.Width/2; var cy=rect.Top+rect.Height/2;
        g.DrawLine(outline,cx,rect.Top+5,cx,rect.Bottom-5); g.DrawLine(outline,rect.Left+5,cy,rect.Right-5,cy);
        var px=cx+(int)(x*rect.Width*.32); var py=cy-(int)(y*rect.Height*.32);
        g.FillEllipse(active,px-9,py-9,18,18); g.DrawEllipse(Pens.Black,px-9,py-9,18,18);
        var size=g.MeasureString(label,Font); g.DrawString(label,Font,text,cx-size.Width/2,rect.Bottom+2);
    }
    private void DrawButton(Graphics g,string target,Rectangle rect,string label,bool on,Brush text,Pen outline,Brush active,Brush inactive)
    {
        _hotspots[target]=rect; g.FillEllipse(on?active:inactive,rect); g.DrawEllipse(outline,rect);
        var size=g.MeasureString(label,Font); g.DrawString(label,Font,text,rect.Left+(rect.Width-size.Width)/2,rect.Top+(rect.Height-size.Height)/2);
    }
    private void DrawPill(Graphics g,string target,Rectangle rect,string label,bool on,Brush text,Pen outline,Brush active,Brush inactive)
    {
        _hotspots[target]=rect; g.FillRectangle(on?active:inactive,rect); g.DrawRectangle(outline,rect);
        var size=g.MeasureString(label,Font); g.DrawString(label,Font,text,rect.Left+(rect.Width-size.Width)/2,rect.Top+(rect.Height-size.Height)/2);
    }
    private void DrawTrigger(Graphics g,string target,Rectangle rect,int value,string label,Brush text,Pen outline,Brush active)
    {
        _hotspots[target]=rect; g.DrawRectangle(outline,rect);
        g.FillRectangle(active,new Rectangle(rect.X+1,rect.Y+1,Math.Max(0,(int)((rect.Width-2)*(value/255.0))),rect.Height-2));
        var size=g.MeasureString(label,Font); g.DrawString(label,Font,text,rect.Left+(rect.Width-size.Width)/2,rect.Top+(rect.Height-size.Height)/2);
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        foreach(var pair in _hotspots)
            if(pair.Value.Contains(e.Location)) { BindRequested?.Invoke(this,pair.Key); return; }
    }
}
