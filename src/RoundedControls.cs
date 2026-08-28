using System.Drawing.Drawing2D;

namespace LogicArrowsLauncher;

public sealed class RoundedPanel : Panel
{
    private int cornerRadius = 18;

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

    public Color? GradientEndColor { get; set; }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreatePath(ClientRectangle, CornerRadius);
        if (GradientEndColor.HasValue && GradientEndColor.Value != Color.Transparent && ClientRectangle.Width > 0 && ClientRectangle.Height > 0)
        {
            using var brush = new LinearGradientBrush(ClientRectangle, BackColor, GradientEndColor.Value, LinearGradientMode.Vertical);
            e.Graphics.FillPath(brush, path);
        }
        else
        {
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
        }
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
    private int cornerRadius = 12;
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

    public int BorderThickness { get; set; } = 0;

    public Color? GradientEndColor { get; set; }

    public Color? HoverBackColor { get; set; }

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

        if (!Enabled)
        {
            using var fill = new SolidBrush(Color.FromArgb(40, 48, 64));
            e.Graphics.FillPath(fill, path);
        }
        else if (GradientEndColor.HasValue && GradientEndColor.Value != Color.Transparent && bounds.Width > 0 && bounds.Height > 0)
        {
            var start = BackColor;
            var end = GradientEndColor.Value;
            if (pressed)
            {
                start = Color.FromArgb(Math.Max(0, start.R - 25), Math.Max(0, start.G - 25), Math.Max(0, start.B - 25));
                end = Color.FromArgb(Math.Max(0, end.R - 25), Math.Max(0, end.G - 25), Math.Max(0, end.B - 25));
            }
            else if (hovered)
            {
                start = ControlPaint.Light(start, 0.15f);
                end = ControlPaint.Light(end, 0.15f);
            }
            using var grad = new LinearGradientBrush(bounds, start, end, LinearGradientMode.Horizontal);
            e.Graphics.FillPath(grad, path);
        }
        else
        {
            var fillColor = pressed
                ? Color.FromArgb(Math.Max(0, BackColor.R - 20), Math.Max(0, BackColor.G - 20), Math.Max(0, BackColor.B - 20))
                : hovered
                    ? (HoverBackColor ?? ControlPaint.Light(BackColor, 0.15f))
                    : BackColor;
            using var fill = new SolidBrush(fillColor);
            e.Graphics.FillPath(fill, path);
        }

        if (BorderThickness > 0 && BorderColor != Color.Transparent)
        {
            var borderBounds = bounds;
            borderBounds.Inflate(-BorderThickness / 2, -BorderThickness / 2);
            using var borderPath = RoundedPanel.CreatePath(borderBounds, CornerRadius);
            using var pen = new Pen(BorderColor, BorderThickness);
            e.Graphics.DrawPath(pen, borderPath);
        }

        var content = ClientRectangle;
        var hasImage = Image is not null;
        if (hasImage)
        {
            var iconBounds = new Rectangle(14, (Height - 18) / 2, 18, 18);
            e.Graphics.DrawImage(Image!, iconBounds);
            content.X += 36;
            content.Width -= 42;
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            content,
            Enabled ? ForeColor : Color.FromArgb(115, 128, 153),
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

    public Color TrackColor { get; set; } = Color.FromArgb(32, 40, 56);

    public Color ProgressColor { get; set; } = Color.FromArgb(56, 189, 248);

    public Color? GradientEndColor { get; set; } = Color.FromArgb(16, 185, 129);

    public RoundedProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 10;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = ClientRectangle;
        track.Inflate(0, -1);
        using var trackPath = RoundedPanel.CreatePath(track, Math.Max(1, track.Height / 2));
        using var trackBrush = new SolidBrush(TrackColor);
        e.Graphics.FillPath(trackBrush, trackPath);

        if (Progress <= 0) return;
        var fill = new Rectangle(track.X, track.Y, Math.Max(track.Height, track.Width * Progress / 100), track.Height);
        fill.Width = Math.Min(fill.Width, track.Width);
        using var fillPath = RoundedPanel.CreatePath(fill, Math.Max(1, fill.Height / 2));

        if (GradientEndColor.HasValue && GradientEndColor.Value != Color.Transparent && fill.Width > 1)
        {
            using var grad = new LinearGradientBrush(fill, ProgressColor, GradientEndColor.Value, LinearGradientMode.Horizontal);
            e.Graphics.FillPath(grad, fillPath);
        }
        else
        {
            using var fillBrush = new SolidBrush(ProgressColor);
            e.Graphics.FillPath(fillBrush, fillPath);
        }
    }
}

public sealed class PillBadge : Control
{
    public Color BadgeColor { get; set; } = Color.FromArgb(16, 185, 129);
    public Color BackgroundColor { get; set; } = Color.FromArgb(10, 36, 28);
    public Color BorderColor { get; set; } = Color.FromArgb(20, 83, 45);

    public PillBadge()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ForeColor = Color.FromArgb(167, 243, 208);
        Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        Height = 26;
        Width = 140;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);
        var radius = bounds.Height / 2;

        using var path = RoundedPanel.CreatePath(bounds, radius);
        using var bg = new SolidBrush(BackgroundColor);
        e.Graphics.FillPath(bg, path);

        if (BorderColor != Color.Transparent)
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawPath(pen, path);
        }

        // Draw dot
        var dotSize = 7;
        var dotRect = new Rectangle(bounds.X + 10, bounds.Y + (bounds.Height - dotSize) / 2, dotSize, dotSize);
        using var dotBrush = new SolidBrush(BadgeColor);
        e.Graphics.FillEllipse(dotBrush, dotRect);

        var textRect = new Rectangle(bounds.X + 22, bounds.Y, bounds.Width - 26, bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textRect,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}