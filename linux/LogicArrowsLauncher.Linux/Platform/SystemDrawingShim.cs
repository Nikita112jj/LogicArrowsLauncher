// Шим System.Drawing для переиспользуемых Shared-файлов (MapData, MapOptimizer):
// они используют только структуры Rectangle/Point как геометрию клеток.
// На Linux System.Drawing.Common недоступен, поэтому даём свои структуры
// с минимальной поверхностью API, которой хватит этим файлам.
#nullable enable
namespace System.Drawing
{
    public struct Point : IEquatable<Point>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Point(int x, int y) { X = x; Y = y; }

        public static readonly Point Empty = new(0, 0);

        public bool IsEmpty => X == 0 && Y == 0;

        public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
        public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Point a, Point b) => a.Equals(b);
        public static bool operator !=(Point a, Point b) => !a.Equals(b);

        public bool Equals(Point other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is Point other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"{{X={X},Y={Y}}}";
    }

    public struct Rectangle : IEquatable<Rectangle>
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }

        public static readonly Rectangle Empty = new(0, 0, 0, 0);

        public bool IsEmpty => Width <= 0 || Height <= 0;
        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;

        public static Rectangle Inflate(Rectangle rect, int x, int y)
            => new(rect.X - x, rect.Y - y, rect.Width + 2 * x, rect.Height + 2 * y);

        public void Inflate(int x, int y) { X -= x; Y -= y; Width += 2 * x; Height += 2 * y; }

        public bool Contains(Point point) => point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

        public bool IntersectsWith(Rectangle other)
            => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

        public static Rectangle Intersect(Rectangle a, Rectangle b)
        {
            int left = Math.Max(a.Left, b.Left);
            int top = Math.Max(a.Top, b.Top);
            int right = Math.Min(a.Right, b.Right);
            int bottom = Math.Min(a.Bottom, b.Bottom);
            return right > left && bottom > top ? new Rectangle(left, top, right - left, bottom - top) : Empty;
        }

        public static Rectangle Union(Rectangle a, Rectangle b)
        {
            int left = Math.Min(a.Left, b.Left);
            int top = Math.Min(a.Top, b.Top);
            int right = Math.Max(a.Right, b.Right);
            int bottom = Math.Max(a.Bottom, b.Bottom);
            return new Rectangle(left, top, right - left, bottom - top);
        }

        public static bool operator ==(Rectangle a, Rectangle b) => a.Equals(b);
        public static bool operator !=(Rectangle a, Rectangle b) => !a.Equals(b);

        public bool Equals(Rectangle other)
            => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is Rectangle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"{{X={X},Y={Y},Width={Width},Height={Height}}}";
    }

    public struct Size : IEquatable<Size>
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public Size(int width, int height) { Width = width; Height = height; }

        public static readonly Size Empty = new(0, 0);

        public bool IsEmpty => Width == 0 && Height == 0;

        public static bool operator ==(Size a, Size b) => a.Equals(b);
        public static bool operator !=(Size a, Size b) => !a.Equals(b);

        public bool Equals(Size other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is Size other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height);
        public override string ToString() => $"{{Width={Width},Height={Height}}}";
    }
}
