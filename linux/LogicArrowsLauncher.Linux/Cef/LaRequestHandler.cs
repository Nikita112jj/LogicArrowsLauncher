using CefNet;

namespace LogicArrowsLauncher.Linux.Cef;

/// <summary>
/// Подключает LaResourceRequestHandler ко всем запросам к origin игры.
/// disableDefaultHandling не выставляем: если снапшот не содержит ресурс,
/// GetResourceHandler вернёт null и запрос пойдёт в обычную сеть — как в WebView2.
/// </summary>
public sealed class LaRequestHandler : CefRequestHandler
{
    private readonly LaResourceRequestHandler resourceRequestHandler;

    public LaRequestHandler(LaResourceRequestHandler resourceRequestHandler)
    {
        this.resourceRequestHandler = resourceRequestHandler;
    }

    protected override CefResourceRequestHandler? GetResourceRequestHandler(
        CefBrowser browser,
        CefFrame frame,
        CefRequest request,
        bool isNavigation,
        bool isDownload,
        string? requestInitiator,
        ref int disableDefaultHandling)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)) return null;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return null;
        return string.Equals(uri.Host, "logic-arrows.io", StringComparison.OrdinalIgnoreCase)
            ? resourceRequestHandler
            : null;
    }

    protected override bool OnBeforeBrowse(CefBrowser browser, CefFrame frame, CefRequest request, bool userGesture, bool isRedirect)
    {
        return false; // не запрещаем никакие переходы
    }
}
