using System.Text;
using System.Text.Json;
using CefNet;

namespace LogicArrowsLauncher.Linux.Cef;

/// <summary>
/// Порт LocalResourceInterceptor (WebView2) на CEF: отдаёт снапшот игры из локального кэша,
/// глушит service worker и принимает сообщения моста (замена chrome.webview.postMessage).
/// </summary>
public sealed class LaResourceRequestHandler : CefResourceRequestHandler
{
    private const string BridgePath = "/__la_bridge";
    private const string SwPath = "/sw.js";

    private readonly Func<JsonDocument, Task> dispatchBridgeMessage;

    public LaResourceRequestHandler(AssetSynchronizer synchronizer, Func<JsonDocument, Task> dispatchBridgeMessage)
    {
        Synchronizer = synchronizer;
        this.dispatchBridgeMessage = dispatchBridgeMessage;
    }

    public AssetSynchronizer Synchronizer { get; set; }

    /// <summary>Расширения пользователя; задаётся после создания менеджера в MainWindow.</summary>
    public ExtensionManager? Extensions { get; set; }

    protected override CefResourceHandler? GetResourceHandler(CefBrowser browser, CefFrame frame, CefRequest request)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)) return null;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.Equals(uri.Host, "logic-arrows.io", StringComparison.OrdinalIgnoreCase)) return null;

        // Состояние встроенного расширения: '1' = активно (нет включённых сторонних).
        if (uri.AbsolutePath.Equals("/__la_builtin_state", StringComparison.OrdinalIgnoreCase))
        {
            var builtInActive = Extensions is null || Extensions.IsBuiltInActive;
            return LaResourceHandler.Ok(builtInActive ? "1"u8.ToArray() : "0"u8.ToArray(), "text/plain",
                "Cache-Control: no-store");
        }

        // Активное расширение пользователя (синхронный XHR моста до скриптов игры).
        if (uri.AbsolutePath.Equals("/__la_extension", StringComparison.OrdinalIgnoreCase))
        {
            var extensionCode = Extensions?.ReadActiveScripts();
            return string.IsNullOrWhiteSpace(extensionCode)
                ? LaResourceHandler.NotFound()
                : LaResourceHandler.Ok(Encoding.UTF8.GetBytes(extensionCode), "application/javascript",
                    "Cache-Control: no-store");
        }

        if (uri.AbsolutePath.Equals(BridgePath, StringComparison.OrdinalIgnoreCase))
        {
            HandleBridgeRequest(uri);
            return LaResourceHandler.Ok("{}"u8.ToArray(), "application/json");
        }

        if (uri.AbsolutePath.Equals(SwPath, StringComparison.OrdinalIgnoreCase))
        {
            var worker = Encoding.UTF8.GetBytes(
                "self.addEventListener('install', e => e.waitUntil(self.skipWaiting())); " +
                "self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));");
            return LaResourceHandler.Ok(worker, "application/javascript",
                "Cache-Control: no-store");
        }

        if (!Synchronizer.TryGetAsset(uri.PathAndQuery, out var bytes, out var relativePath)) return null;

        return LaResourceHandler.Ok(bytes, GetContentType(relativePath),
            "Cache-Control: no-store\r\nAccess-Control-Allow-Origin: https://logic-arrows.io");
    }

    /// <summary>Сообщения моста приходят query-параметром ?m=… (JSON, закодированный в URI).</summary>
    private void HandleBridgeRequest(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query)) return;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var name = pair[..eq];
            var value = Uri.UnescapeDataString(pair[(eq + 1)..].Replace("+", "%20"));
            if (!name.Equals("m", StringComparison.Ordinal)) continue;
            try
            {
                var doc = JsonDocument.Parse(value);
                // CEF вызывает GetResourceHandler не на UI-потоке — переводим в главный.
                var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
                _ = dispatcher.InvokeAsync(() => dispatchBridgeMessage(doc));
            }
            catch
            {
                // битые сообщения моста молча игнорируем — как Windows-версия (catch { }).
            }
        }
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".mp3" => "audio/mpeg",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".wasm" => "application/wasm",
            ".map" => "application/json",
            ".webp" => "image/webp",
            ".xml" => "application/xml",
            _ => "application/octet-stream",
        };
    }
}
