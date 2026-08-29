using Microsoft.Web.WebView2.Core;
using System.Text;

namespace LogicArrowsLauncher;

public sealed class LocalResourceInterceptor
{
    private AssetSynchronizer synchronizer;

    public LocalResourceInterceptor(AssetSynchronizer synchronizer)
    {
        this.synchronizer = synchronizer;
    }

    public void SetSynchronizer(AssetSynchronizer nextSynchronizer)
    {
        synchronizer = nextSynchronizer;
    }

    /// <summary>Расширения пользователя; задаётся после создания менеджера в LauncherForm.</summary>
    public ExtensionManager? Extensions { get; set; }

    public void Attach(CoreWebView2 webView)
    {
        webView.AddWebResourceRequestedFilter(
            $"{ResourceCatalog.Origin}/*",
            CoreWebView2WebResourceContext.All);
        webView.WebResourceRequested += HandleRequest;
    }

    private async void HandleRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        using var deferral = args.GetDeferral();
        if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri)) return;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return;
        if (!string.Equals(uri.Host, "logic-arrows.io", StringComparison.OrdinalIgnoreCase)) return;
        if (sender is not CoreWebView2 webView) return;

        if (uri.AbsolutePath.Equals("/sw.js", StringComparison.OrdinalIgnoreCase))
        {
            var worker = Encoding.UTF8.GetBytes(
                "self.addEventListener('install', e => e.waitUntil(self.skipWaiting())); " +
                "self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));");
            args.Response = webView.Environment.CreateWebResourceResponse(
                new MemoryStream(worker, writable: false),
                200,
                "OK",
                "Content-Type: application/javascript; charset=utf-8\r\nCache-Control: no-store\r\n");
            return;
        }

        // Активное расширение пользователя (синхронный XHR моста до скриптов игры).
        if (uri.AbsolutePath.Equals("/__la_extension", StringComparison.OrdinalIgnoreCase))
        {
            var extensionCode = Extensions?.ReadActiveScripts();
            var hasCode = !string.IsNullOrWhiteSpace(extensionCode);
            args.Response = webView.Environment.CreateWebResourceResponse(
                new MemoryStream(
                    hasCode ? Encoding.UTF8.GetBytes(extensionCode) : Array.Empty<byte>(),
                    writable: false),
                hasCode ? 200 : 404,
                hasCode ? "OK" : "Not Found",
                "Content-Type: application/javascript; charset=utf-8\r\nCache-Control: no-store\r\n");
            return;
        }

        if (!synchronizer.TryGetAsset(uri.PathAndQuery, out var bytes, out var relativePath)) return;

        try
        {
            args.Response = webView.Environment.CreateWebResourceResponse(
                new MemoryStream(bytes, writable: false),
                200,
                "OK",
                $"Content-Type: {GetContentType(relativePath)}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: https://logic-arrows.io\r\n");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Leave the response unset so WebView2 can use the original network request.
        }
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".ttf" => "font/ttf",
            ".frag" or ".vert" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }
}
