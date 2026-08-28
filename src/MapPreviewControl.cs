using System.Drawing.Drawing2D;

namespace LogicArrowsLauncher;

public sealed class MapPreviewControl : Control
{
    private MapBlueprint? blueprint;
    private float cellSize = 32f;
    private float offsetX = 0f;
    private float offsetY = 0f;
    private bool isDragging;
    private Point lastMousePos;
    private Point? hoveredCell;

    public MapBlueprint? Blueprint
    {
        get => blueprint;
        set
        {
            blueprint = value;
            ResetView();
            Invalidate();
        }
    }

    public MapPreviewControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(13, 17, 23);
        Cursor = Cursors.Hand;
    }

    public void ResetView()
    {
        if (blueprint is null || blueprint.Cells.Count == 0 || Width <= 0 || Height <= 0)
        {
            cellSize = 32f;
            offsetX = Width / 2f;
            offsetY = Height / 2f;
            Invalidate();
            return;
        }

        var bbox = blueprint.BoundingBox;
        float padding = 40f;
        float availableW = Math.Max(50f, Width - padding * 2);
        float availableH = Math.Max(50f, Height - padding * 2);

        float fitScaleX = availableW / Math.Max(1, bbox.Width);
        float fitScaleY = availableH / Math.Max(1, bbox.Height);
        cellSize = Math.Clamp(Math.Min(fitScaleX, fitScaleY), 16f, 72f);

        float mechanismCenterX = (bbox.Left + bbox.Width / 2f) * cellSize;
        float mechanismCenterY = (bbox.Top + bbox.Height / 2f) * cellSize;

        offsetX = (Width / 2f) - mechanismCenterX;
        offsetY = (Height / 2f) - mechanismCenterY;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
        {
            isDragging = true;
            lastMousePos = e.Location;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isDragging)
        {
            offsetX += e.X - lastMousePos.X;
            offsetY += e.Y - lastMousePos.Y;
            lastMousePos = e.Location;
            Invalidate();
        }
        else
        {
            // Compute hovered cell
            int cx = (int)Math.Floor((e.X - offsetX) / cellSize);
            int cy = (int)Math.Floor((e.Y - offsetY) / cellSize);
            var nextHover = new Point(cx, cy);
            if (hoveredCell != nextHover)
            {
                hoveredCell = nextHover;
                Invalidate();
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        isDragging = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        float oldCellSize = cellSize;
        float zoomFactor = e.Delta > 0 ? 1.15f : 0.85f;
        cellSize = Math.Clamp(cellSize * zoomFactor, 8f, 120f);

        // Zoom centered at mouse cursor
        float mouseWorldX = (e.X - offsetX) / oldCellSize;
        float mouseWorldY = (e.Y - offsetY) / oldCellSize;

        offsetX = e.X - mouseWorldX * cellSize;
        offsetY = e.Y - mouseWorldY * cellSize;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // 1. Draw Grid
        DrawGrid(g);

        // 2. Draw Mechanism Cells
        if (blueprint is not null && blueprint.Cells.Count > 0)
        {
            foreach (var cell in blueprint.Cells)
            {
                DrawCell(g, cell);
            }

            // Draw bounding box outline
            var bbox = blueprint.BoundingBox;
            var bx = offsetX + bbox.Left * cellSize;
            var by = offsetY + bbox.Top * cellSize;
            var bw = bbox.Width * cellSize;
            var bh = bbox.Height * cellSize;
            using var bboxPen = new Pen(Color.FromArgb(60, 88, 166, 255), 1.5f)
            {
                DashStyle = DashStyle.Dash
            };
            g.DrawRectangle(bboxPen, bx, by, bw, bh);
        }

        // 3. Hover indicator and cell coordinate tooltip
        if (hoveredCell.HasValue && cellSize >= 16f)
        {
            var hx = offsetX + hoveredCell.Value.X * cellSize;
            var hy = offsetY + hoveredCell.Value.Y * cellSize;
            using var hPen = new Pen(Color.FromArgb(180, 255, 255, 255), 1.5f);
            g.DrawRectangle(hPen, hx, hy, cellSize, cellSize);

            var matchingCell = blueprint?.Cells.FirstOrDefault(c => c.X == hoveredCell.Value.X && c.Y == hoveredCell.Value.Y);
            string infoText = matchingCell is not null
                ? $"({matchingCell.X}, {matchingCell.Y}) • {GetArrowTypeName(matchingCell.Type)}"
                : $"({hoveredCell.Value.X}, {hoveredCell.Value.Y})";

            using var font = new Font("Segoe UI", 9f, FontStyle.Regular);
            var size = g.MeasureString(infoText, font);
            var tipRect = new RectangleF(12, Height - size.Height - 14, size.Width + 16, size.Height + 8);
            using var tipBrush = new SolidBrush(Color.FromArgb(220, 22, 27, 34));
            using var tipBorder = new Pen(Color.FromArgb(48, 54, 61), 1f);
            g.FillRectangle(tipBrush, tipRect);
            g.DrawRectangle(tipBorder, tipRect.X, tipRect.Y, tipRect.Width, tipRect.Height);
            using var textBrush = new SolidBrush(Color.FromArgb(201, 209, 217));
            g.DrawString(infoText, font, textBrush, tipRect.X + 8, tipRect.Y + 4);
        }
    }

    private void DrawGrid(Graphics g)
    {
        int minVisibleX = (int)Math.Floor(-offsetX / cellSize) - 1;
        int maxVisibleX = (int)Math.Ceiling((Width - offsetX) / cellSize) + 1;
        int minVisibleY = (int)Math.Floor(-offsetY / cellSize) - 1;
        int maxVisibleY = (int)Math.Ceiling((Height - offsetY) / cellSize) + 1;

        using var cellPen = new Pen(Color.FromArgb(22, 27, 34), 1f);
        using var chunkPen = new Pen(Color.FromArgb(48, 54, 61), 1.5f);

        // Vertical lines
        for (int x = minVisibleX; x <= maxVisibleX; x++)
        {
            float screenX = offsetX + x * cellSize;
            var pen = (x % 16 == 0) ? chunkPen : cellPen;
            g.DrawLine(pen, screenX, 0, screenX, Height);
        }

        // Horizontal lines
        for (int y = minVisibleY; y <= maxVisibleY; y++)
        {
            float screenY = offsetY + y * cellSize;
            var pen = (y % 16 == 0) ? chunkPen : cellPen;
            g.DrawLine(pen, 0, screenY, Width, screenY);
        }

        // Coordinate Origin Marker (0, 0)
        float originX = offsetX;
        float originY = offsetY;
        if (originX >= -20 && originX <= Width + 20 && originY >= -20 && originY <= Height + 20)
        {
            using var originBrush = new SolidBrush(Color.FromArgb(60, 56, 139, 253));
            g.FillEllipse(originBrush, originX - 4, originY - 4, 8, 8);
        }
    }

    private void DrawCell(Graphics g, MapCell cell)
    {
        float x = offsetX + cell.X * cellSize;
        float y = offsetY + cell.Y * cellSize;
        if (x + cellSize < 0 || x > Width || y + cellSize < 0 || y > Height) return;

        var center = new PointF(x + cellSize / 2f, y + cellSize / 2f);
        var state = g.Save();

        // Rotate & position
        g.TranslateTransform(center.X, center.Y);
        float angle = cell.Rotation * 90f;
        g.RotateTransform(angle);
        if (cell.Flipped) g.ScaleTransform(-1, 1);

        float s = cellSize * 0.85f;
        var r = new RectangleF(-s / 2f, -s / 2f, s, s);

        Color cellColor = GetArrowColor(cell.Type);
        using var brush = new SolidBrush(cellColor);
        using var pen = new Pen(cellColor, Math.Max(1.5f, cellSize * 0.08f));

        switch (cell.Type)
        {
            case 1: // Standard Red Arrow
                DrawArrowGlyph(g, cellColor, s);
                break;
            case 2: // Source Block (4-way emitter)
                using (var fill = new SolidBrush(Color.FromArgb(60, cellColor)))
                    g.FillRectangle(fill, r);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                DrawMiniArrow(g, cellColor, 0, -s / 2f, 0);
                DrawMiniArrow(g, cellColor, s / 2f, 0, 90);
                DrawMiniArrow(g, cellColor, 0, s / 2f, 180);
                DrawMiniArrow(g, cellColor, -s / 2f, 0, 270);
                break;
            case 3: // Blocker
                using (var fill = new SolidBrush(Color.FromArgb(40, cellColor)))
                    g.FillRectangle(fill, r);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                using (var barPen = new Pen(Color.FromArgb(248, 81, 73), Math.Max(2f, cellSize * 0.12f)))
                    g.DrawLine(barPen, -s * 0.35f, -s * 0.3f, s * 0.35f, -s * 0.3f);
                break;
            case 4: // Delay Arrow
                DrawArrowGlyph(g, Color.FromArgb(88, 166, 255), s);
                using (var barPen = new Pen(Color.FromArgb(248, 81, 73), Math.Max(2f, cellSize * 0.1f)))
                    g.DrawLine(barPen, -s * 0.25f, s * 0.2f, s * 0.25f, s * 0.2f);
                break;
            case 5: // Detector
                DrawArrowGlyph(g, cellColor, s);
                using (var dotBrush = new SolidBrush(Color.FromArgb(255, 235, 59)))
                    g.FillEllipse(dotBrush, -s * 0.18f, s * 0.15f, s * 0.36f, s * 0.36f);
                break;
            case 6: // Up-Down Splitter
                DrawArrowGlyph(g, cellColor, s * 0.8f);
                DrawMiniArrow(g, cellColor, 0, s * 0.25f, 180);
                break;
            case 7: // Up-Right Splitter
                DrawArrowGlyph(g, cellColor, s * 0.8f);
                DrawMiniArrow(g, cellColor, s * 0.25f, 0, 90);
                break;
            case 8: // Up-Left-Right Splitter
                DrawArrowGlyph(g, cellColor, s * 0.7f);
                DrawMiniArrow(g, cellColor, -s * 0.25f, 0, 270);
                DrawMiniArrow(g, cellColor, s * 0.25f, 0, 90);
                break;
            case 9: // Pulse generator
                using (var fill = new SolidBrush(Color.FromArgb(50, cellColor)))
                    g.FillEllipse(fill, r);
                g.DrawEllipse(pen, r);
                DrawArrowGlyph(g, cellColor, s * 0.65f);
                break;
            case 10: // Blue Arrow (Fast double-step)
                DrawDoubleArrowGlyph(g, cellColor, s);
                break;
            case 11: // Diagonal Arrow
                DrawDiagonalArrowGlyph(g, cellColor, s);
                break;
            case 15: // NOT Gate
                DrawGateBadge(g, "NOT", cellColor, s);
                break;
            case 16: // AND Gate
                DrawGateBadge(g, "AND", cellColor, s);
                break;
            case 17: // XOR Gate
                DrawGateBadge(g, "XOR", cellColor, s);
                break;
            case 18: // Latch
                DrawGateBadge(g, "LATCH", cellColor, s);
                break;
            case 19: // T Flip-Flop
                DrawGateBadge(g, "T-FF", cellColor, s);
                break;
            case 20: // Randomizer
                DrawGateBadge(g, "RND", cellColor, s);
                break;
            case 21: // Button
            case 22: // Directional Button
                using (var btnFill = new SolidBrush(Color.FromArgb(80, cellColor)))
                    g.FillEllipse(btnFill, -s * 0.4f, -s * 0.4f, s * 0.8f, s * 0.8f);
                g.DrawEllipse(pen, -s * 0.4f, -s * 0.4f, s * 0.8f, s * 0.8f);
                break;
            case 25: // 7-Segment / Display
                DrawDisplayBadge(g, s);
                break;
            default:
                DrawArrowGlyph(g, cellColor, s);
                break;
        }

        g.Restore(state);
    }

    private static void DrawArrowGlyph(Graphics g, Color color, float size)
    {
        float w = size * 0.5f;
        float h = size * 0.55f;
        var points = new[]
        {
            new PointF(0, -h),
            new PointF(w, h * 0.7f),
            new PointF(0, h * 0.3f),
            new PointF(-w, h * 0.7f),
        };
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, points);
    }

    private static void DrawDoubleArrowGlyph(Graphics g, Color color, float size)
    {
        float w = size * 0.45f;
        float h = size * 0.35f;
        using var brush = new SolidBrush(color);

        var top = new[]
        {
            new PointF(0, -size * 0.55f),
            new PointF(w, -size * 0.55f + h),
            new PointF(0, -size * 0.55f + h * 0.5f),
            new PointF(-w, -size * 0.55f + h),
        };
        g.FillPolygon(brush, top);

        var bot = new[]
        {
            new PointF(0, -size * 0.1f),
            new PointF(w, -size * 0.1f + h),
            new PointF(0, -size * 0.1f + h * 0.5f),
            new PointF(-w, -size * 0.1f + h),
        };
        g.FillPolygon(brush, bot);
    }

    private static void DrawDiagonalArrowGlyph(Graphics g, Color color, float size)
    {
        using var pen = new Pen(color, Math.Max(2f, size * 0.15f));
        g.DrawLine(pen, -size * 0.35f, size * 0.35f, size * 0.35f, -size * 0.35f);
        using var brush = new SolidBrush(color);
        var head = new[]
        {
            new PointF(size * 0.35f, -size * 0.35f),
            new PointF(size * 0.05f, -size * 0.35f),
            new PointF(size * 0.35f, -size * 0.05f),
        };
        g.FillPolygon(brush, head);
    }

    private static void DrawMiniArrow(Graphics g, Color color, float cx, float cy, float rotDeg)
    {
        var s = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(rotDeg);
        float w = 3.5f, h = 4.5f;
        var pts = new[] { new PointF(0, -h), new PointF(w, h), new PointF(-w, h) };
        using var b = new SolidBrush(color);
        g.FillPolygon(b, pts);
        g.Restore(s);
    }

    private static void DrawGateBadge(Graphics g, string label, Color color, float size)
    {
        var r = new RectangleF(-size * 0.45f, -size * 0.45f, size * 0.9f, size * 0.9f);
        using var fill = new SolidBrush(Color.FromArgb(40, color));
        using var pen = new Pen(color, Math.Max(1.5f, size * 0.08f));
        g.FillRectangle(fill, r);
        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

        // Arrow indicator pointing up
        DrawMiniArrow(g, color, 0, -size * 0.25f, 0);

        if (size >= 24f)
        {
            using var font = new Font("Segoe UI", Math.Max(6f, size * 0.22f), FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(240, 246, 252));
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(label, font, textBrush, new RectangleF(r.X, r.Y + size * 0.1f, r.Width, r.Height - size * 0.1f), sf);
        }
    }

    private static void DrawDisplayBadge(Graphics g, float size)
    {
        var color = Color.FromArgb(0, 210, 255);
        var r = new RectangleF(-size * 0.45f, -size * 0.45f, size * 0.9f, size * 0.9f);
        using var fill = new SolidBrush(Color.FromArgb(30, color));
        using var pen = new Pen(color, Math.Max(1.5f, size * 0.08f));
        g.FillRectangle(fill, r);
        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

        if (size >= 20f)
        {
            using var font = new Font("Segoe UI", Math.Max(7f, size * 0.35f), FontStyle.Bold);
            using var textBrush = new SolidBrush(color);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("8", font, textBrush, r, sf);
        }
    }

    public static Color GetArrowColor(int type)
    {
        return type switch
        {
            1 => Color.FromArgb(248, 81, 73),    // Red Straight Arrow
            2 => Color.FromArgb(255, 107, 107),  // Source Block
            3 => Color.FromArgb(248, 81, 73),    // Blocker
            4 => Color.FromArgb(88, 166, 255),   // Delay Arrow
            5 => Color.FromArgb(255, 193, 7),    // Detector
            6 or 7 or 8 => Color.FromArgb(248, 81, 73), // Red Splitters
            9 => Color.FromArgb(255, 64, 129),   // Pulse Generator
            10 => Color.FromArgb(88, 166, 255),  // Blue Arrow
            11 or 12 or 13 or 14 => Color.FromArgb(88, 166, 255), // Blue diagonal/splitters
            15 => Color.FromArgb(227, 179, 65),  // NOT Gate
            16 => Color.FromArgb(227, 179, 65),  // AND Gate
            17 => Color.FromArgb(227, 179, 65),  // XOR Gate
            18 => Color.FromArgb(227, 179, 65),  // Latch
            19 => Color.FromArgb(227, 179, 65),  // T Flip-Flop
            20 => Color.FromArgb(219, 109, 40),  // Randomizer
            21 or 22 => Color.FromArgb(219, 109, 40), // Button
            23 => Color.FromArgb(63, 185, 80),   // Level Source
            24 => Color.FromArgb(248, 81, 73),   // Level Target
            25 => Color.FromArgb(0, 210, 255),   // 7-Seg Display
            _ => Color.FromArgb(139, 148, 158)
        };
    }

    public static string GetArrowTypeName(int type)
    {
        return type switch
        {
            1 => "Стрелка (Красная)",
            2 => "Источник (Source Block)",
            3 => "Блокировщик (Blocker)",
            4 => "Задержка (Delay)",
            5 => "Детектор сигнала (Detector)",
            6 => "Разветвитель Вверх-Вниз",
            7 => "Разветвитель Вверх-Вправо",
            8 => "Разветвитель Тройной (T-Splitter)",
            9 => "Генератор импульсов (Pulse)",
            10 => "Синяя стрелка (Fast)",
            11 => "Диагональная стрелка",
            12 => "Синий разветвитель Вверх-Вверх",
            13 => "Синий разветвитель Вверх-Вправо",
            14 => "Синий диагональный разветвитель",
            15 => "Элемент НЕ (NOT Gate)",
            16 => "Элемент И (AND Gate)",
            17 => "Элемент ИСКЛ-ИЛИ (XOR Gate)",
            18 => "Защёлка (Latch)",
            19 => "T-триггер (T Flip-Flop)",
            20 => "Случайный выбор (Randomizer)",
            21 => "Кнопка (Button)",
            22 => "Направленная кнопка",
            23 => "Источник уровня (Level Source)",
            24 => "Цель уровня (Level Target)",
            25 => "7-сегментный индикатор",
            _ => $"Блок #{type}"
        };
    }
}
