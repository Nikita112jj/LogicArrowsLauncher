using System.Drawing.Drawing2D;

namespace LogicArrowsLauncher;

public sealed class RoundedPanel : Panel
{
    private int cornerRadius = 14;

    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = Math.Max(0, value);
            UpdateRegion();
            Invalidate();
        }
    }

    public Color BorderColor { get; set; } = Color.Transparent;

    public int BorderThickness { get; set; } = 1;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreatePath(ClientRectangle, CornerRadius);
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (BorderThickness <= 0 || BorderColor == Color.Transparent) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Inflate(-BorderThickness / 2, -BorderThickness / 2);
        using var path = CreatePath(bounds, CornerRadius);
        using var pen = new Pen(BorderColor, BorderThickness);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        Region?.Dispose();
        using var path = CreatePath(ClientRectangle, CornerRadius);
        Region = new Region(path);
    }

    internal static GraphicsPath CreatePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        var diameter = Math.Min(Math.Min(radius * 2, bounds.Width), bounds.Height);
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.X;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class RoundedButton : Button
{
    private int cornerRadius = 8;
    private bool hovered;
    private bool pressed;

    public int CornerRadius
    {
        get => cornerRadius;
        set
        {
            cornerRadius = Math.Max(0, value);
            UpdateRegion();
            Invalidate();
        }
    }

    public Color BorderColor { get; set; } = Color.Transparent;

    public int BorderThickness { get; set; } = 1;

    public Color? HoverBackColor { get; set; }

    public Color? PressedBackColor { get; set; }

    public RoundedButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        pressed = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressed = true;
        base.OnMouseDown(e);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        base.OnMouseUp(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        using var path = RoundedPanel.CreatePath(bounds, CornerRadius);

        Color fillColor;
        if (!Enabled)
        {
            fillColor = Color.FromArgb(33, 38, 45);
        }
        else if (pressed)
        {
            fillColor = PressedBackColor ?? Color.FromArgb(
                Math.Max(0, BackColor.R - 20),
                Math.Max(0, BackColor.G - 20),
                Math.Max(0, BackColor.B - 20));
        }
        else if (hovered)
        {
            fillColor = HoverBackColor ?? ControlPaint.Light(BackColor, 0.12f);
        }
        else
        {
            fillColor = BackColor;
        }

        using (var fill = new SolidBrush(fillColor))
        {
            e.Graphics.FillPath(fill, path);
        }

        if (BorderThickness > 0 && BorderColor != Color.Transparent)
        {
            var borderBounds = bounds;
            borderBounds.Inflate(-BorderThickness / 2, -BorderThickness / 2);
            using var borderPath = RoundedPanel.CreatePath(borderBounds, CornerRadius);
            using var pen = new Pen(Enabled ? BorderColor : Color.FromArgb(48, 54, 61), BorderThickness);
            e.Graphics.DrawPath(pen, borderPath);
        }

        var content = ClientRectangle;
        if (Image is not null)
        {
            var iconSize = 16;
            var iconBounds = new Rectangle(12, (Height - iconSize) / 2, iconSize, iconSize);
            e.Graphics.DrawImage(Image, iconBounds);
            content.X += 28;
            content.Width -= 34;
        }

        var textColor = !Enabled ? Color.FromArgb(139, 148, 158) : ForeColor;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            content,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        Region?.Dispose();
        using var path = RoundedPanel.CreatePath(ClientRectangle, CornerRadius);
        Region = new Region(path);
    }
}

public sealed class RoundedProgressBar : Control
{
    private int progress;

    public int Progress
    {
        get => progress;
        set
        {
            progress = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    public Color TrackColor { get; set; } = Color.FromArgb(33, 38, 45);

    public Color ProgressColor { get; set; } = Color.FromArgb(35, 134, 54);

    public RoundedProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 6;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = ClientRectangle;
        using var trackPath = RoundedPanel.CreatePath(track, Math.Max(1, track.Height / 2));
        using var trackBrush = new SolidBrush(TrackColor);
        e.Graphics.FillPath(trackBrush, trackPath);

        if (Progress <= 0) return;
        var fillWidth = Math.Max(track.Height, track.Width * Progress / 100);
        var fill = new Rectangle(track.X, track.Y, Math.Min(fillWidth, track.Width), track.Height);
        using var fillPath = RoundedPanel.CreatePath(fill, Math.Max(1, fill.Height / 2));
        using var fillBrush = new SolidBrush(ProgressColor);
        e.Graphics.FillPath(fillBrush, fillPath);
    }
}