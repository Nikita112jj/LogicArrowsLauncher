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
    private int cornerRadius = 12;

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

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
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

    public Color TrackColor { get; set; } = Color.FromArgb(53, 59, 78);

    public Color ProgressColor { get; set; } = Color.FromArgb(107, 177, 255);

    public RoundedProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 12;
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
        using var fillBrush = new SolidBrush(ProgressColor);
        e.Graphics.FillPath(fillBrush, fillPath);
    }
}
