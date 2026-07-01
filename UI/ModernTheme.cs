using System;
using System.Drawing;
using System.Windows.Forms;

namespace SavagePadEmu;

public static class ModernTheme
{
    public static readonly Color AppBackground = Color.FromArgb(245, 247, 251);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(222, 226, 235);
    public static readonly Color Text = Color.FromArgb(31, 41, 55);
    public static readonly Color MutedText = Color.FromArgb(100, 116, 139);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);

    public static void StylePrimaryButton(Button button)
    {
        StyleButton(button, Accent, Color.White);
    }

    public static void StyleSecondaryButton(Button button)
    {
        StyleButton(button, Surface, Text);
        button.FlatAppearance.BorderColor = Border;
    }

    public static void StyleDangerButton(Button button)
    {
        StyleButton(button, Color.FromArgb(254, 242, 242), Danger);
        button.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
    }

    private static void StyleButton(Button button, Color backColor, Color foreColor)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = backColor == Accent ? AccentHover : Color.FromArgb(241, 245, 249);
        button.FlatAppearance.MouseDownBackColor = backColor == Accent ? Color.FromArgb(30, 64, 175) : Color.FromArgb(226, 232, 240);
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.Margin = new Padding(3);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    public static void StyleInput(Control control)
    {
        control.BackColor = Surface;
        control.ForeColor = Text;
    }

    public static Label SectionTitle(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 10F),
        ForeColor = Text,
        Margin = new Padding(0, 0, 0, 8)
    };

    public static Label Caption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = MutedText,
        Font = new Font("Segoe UI", 8.5F)
    };
}

public sealed class ModernCard : Panel
{
    public ModernCard()
    {
        BackColor = ModernTheme.Surface;
        Padding = new Padding(14);
        BorderStyle = BorderStyle.FixedSingle;
        DoubleBuffered = true;
    }
}

public sealed class ModernTabControl : TabControl
{
    public ModernTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(150, 34);
        Padding = new Point(12, 4);
        Font = new Font("Segoe UI Semibold", 9F);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = e.Index == SelectedIndex;
        var bounds = GetTabRect(e.Index);
        using var background = new SolidBrush(selected ? ModernTheme.Surface : ModernTheme.AppBackground);
        using var textBrush = new SolidBrush(selected ? ModernTheme.Accent : ModernTheme.MutedText);
        e.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(
            e.Graphics,
            TabPages[e.Index].Text,
            Font,
            bounds,
            textBrush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (selected)
        {
            using var accent = new SolidBrush(ModernTheme.Accent);
            e.Graphics.FillRectangle(accent, bounds.Left + 18, bounds.Bottom - 3, Math.Max(20, bounds.Width - 36), 3);
        }
    }
}
