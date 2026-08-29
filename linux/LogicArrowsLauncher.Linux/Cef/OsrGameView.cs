using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using CefNet;

namespace LogicArrowsLauncher.Linux.Cef;

/// <summary>
/// Avalonia-контрол, в который CEF рисует игру в режиме off-screen rendering (OSR).
/// OnPaint копирует BGRA-буфер в WriteableBitmap (формат совпадает 1:1),
/// ввод (мышь/колесо/клавиатура) пересылается в CefBrowserHost.
/// </summary>
public sealed class OsrGameView : Control
{
    private readonly CefEngine engine;
    private WriteableBitmap? bitmap;
    private PixelSize bitmapSize;

    public OsrGameView(CefEngine engine)
    {
        this.engine = engine;
        Focusable = true;
        ClipToBounds = true;
        engine.AttachView(this);
    }

    internal void PresentFrame(IntPtr buffer, int width, int height)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => PresentFrame(buffer, width, height));
            return;
        }

        var size = new PixelSize(width, height);
        if (bitmap is null || bitmapSize != size)
        {
            bitmap?.Dispose();
            bitmap = new WriteableBitmap(size, new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Opaque);
            bitmapSize = size;
        }

        using (var frame = bitmap.Lock())
        {
            unsafe
            {
                var src = (byte*)buffer;
                var dst = (byte*)frame.Address;
                var srcStride = width * 4;
                var dstStride = frame.RowBytes;
                var copyStride = Math.Min(srcStride, dstStride);
                for (var y = 0; y < height; y++)
                    Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, copyStride, copyStride);
            }
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (bitmap is not null)
        {
            var dest = new Rect(0, 0, bitmapSize.Width / engine.RenderScaling, bitmapSize.Height / engine.RenderScaling);
            context.DrawImage(bitmap, dest);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse("#0d1117")), Bounds);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        engine.NotifyViewResized(finalSize);
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        engine.SetFocus(true);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        engine.SetFocus(false);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        engine.SendMouseMove(e.GetPosition(this));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        engine.SendMouseClick(point.Position, point.Properties.PointerUpdateKind, mouseUp: false, clickCount: point.Properties.IsLeftButtonPressed && e.ClickCount == 2 ? 2 : 1);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetCurrentPoint(this);
        engine.SendMouseClick(point.Position, point.Properties.PointerUpdateKind, mouseUp: true, clickCount: 1);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        engine.SendMouseWheel(e.GetPosition(this), e.Delta);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        engine.SendMouseLeave();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (engine.SendKey(e.Key, e.KeyModifiers, keyUp: false))
            e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (engine.SendKey(e.Key, e.KeyModifiers, keyUp: true))
            e.Handled = true;
    }
}
