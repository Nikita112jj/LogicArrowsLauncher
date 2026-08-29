using System.Text.Json;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CefNet;

namespace LogicArrowsLauncher.Linux.Cef;

/// <summary>
/// CEF-движок: инициализация, цикл сообщений (ExternalMessagePump + DispatcherTimer),
/// создание OSR-браузера, инъекция моста в каждый документ и замена ExecuteScriptAsync.
/// Мост шлёт сообщения и результаты скриптов через fetch('/__la_bridge?…') —
/// этот путь перехватывается так же, как снапшот игры (см. LaResourceRequestHandler).
/// </summary>
public sealed class CefEngine : IDisposable
{
    private const double WheelTickPixels = 53;

    private CefBrowser? browser;
    private GameClient? client;
    private OsrGameView? view;
    private DispatcherTimer? pumpTimer;
    private readonly Dictionary<int, TaskCompletionSource<string?>> execWaiters = new();
    private int execId;

    /// <summary>Инъекция: shim chrome.webview + мост игры (порядок важен).</summary>
    private static readonly string BridgeBootstrap =
        "(function(){" +
        "if (globalThis.__laShimInstalled) return; globalThis.__laShimInstalled = true;" +
        "var send = function(obj){ try { fetch('/__la_bridge?m=' + encodeURIComponent(JSON.stringify(obj)), {cache:'no-store'}); } catch(e){} };" +
        "globalThis.chrome = globalThis.chrome || {};" +
        "chrome.webview = chrome.webview || {};" +
        "chrome.webview.postMessage = function(msg){ send({ __kind:'message', payload: msg }); };" +
        "globalThis.__laRun = function(id, fn){" +
        "  var report = function(value, error){ send({ __kind:'exec', id:id, value:value, error:error||null }); };" +
        "  try {" +
        "    var result = fn();" +
        "    if (result && typeof result.then === 'function') { result.then(function(v){ try{ report(JSON.stringify(v), null); }catch(e2){ report('null', null); } }, function(e){ report(null, String(e)); }); return; }" +
        "    var json; try { json = JSON.stringify(result); } catch(e){ json = 'null'; }" +
        "    report(json, null);" +
        "  } catch(e){ report(null, String(e)); }" +
        "};" +
        "})();";

    /// <summary>Сообщение моста без envelope'а — как chrome.webview.postMessage на Windows.</summary>
    public event EventHandler<JsonDocument>? BridgeMessageReceived;

    /// <summary>Загрузка главного фрейма завершилась (аналог NavigationCompleted).</summary>
    public event EventHandler? MainFrameLoadEnd;

    /// <summary>Сигнализируется, когда главная страница игры загрузилась после Navigate.</summary>
    public TaskCompletionSource<bool> GamePageReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady => browser is not null;
    public double RenderScaling => view?.GetVisualRoot()?.RenderScaling ?? 1.0;

    public static bool Start(string[] args, out string error)
    {
        error = string.Empty;
        var dataRoot = Platform.LinuxPaths.DataRoot;
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(Platform.LinuxPaths.ProfileDirectory);
        Directory.CreateDirectory(Platform.LinuxPaths.CefCacheDirectory);
        Directory.CreateDirectory(Platform.LinuxPaths.MapsDirectory);

        var mainArgs = CefMainArgs.CreateDefault();
        var app = new LaApp();

        // Субпроцессы CEF (renderer/gpu) запускают этот же exe с --type=… и не возвращаются.
        var exitCode = CefApi.ExecuteProcess(mainArgs, app, IntPtr.Zero);
        if (exitCode >= 0) Environment.Exit(exitCode);

        var settings = new CefSettings
        {
            BrowserSubprocessPath = Environment.ProcessPath,
            CachePath = Platform.LinuxPaths.ProfileDirectory,
            RootCachePath = Platform.LinuxPaths.CefCacheDirectory,
            MultiThreadedMessageLoop = false, // Linux не поддерживает многопоточный цикл сообщений
            ExternalMessagePump = true,       // качаем DoMessageLoopWork из DispatcherTimer
            WindowlessRenderingEnabled = true,
            NoSandbox = true,
            BackgroundColor = (CefColor)unchecked((int)0xFF0D1117u),
        };

        if (!CefApi.Initialize(mainArgs, settings, app, IntPtr.Zero))
        {
            error = "Не удалось инициализировать CEF. Проверьте установку libcef и зависимостей WebKit/GTK.";
            return false;
        }
        return true;
    }

    public void StartMessagePump()
    {
        if (pumpTimer is not null) return;
        pumpTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(8), DispatcherPriority.Normal, (_, _) =>
            CefApi.DoMessageLoopWork());
        pumpTimer.Start();
    }

    public void AttachView(OsrGameView gameView)
    {
        view = gameView;
    }

    public void Navigate(string url)
    {
        if (browser is not null) return; // переходим один раз за жизнь окна

        client = new GameClient(this);
        var windowInfo = new CefWindowInfo
        {
            WindowlessRenderingEnabled = true,
        };
        windowInfo.SetAsWindowless(IntPtr.Zero);

        browser = CefApi.CreateBrowserSync(windowInfo, client, url, new CefBrowserSettings(), null, null);
    }

    public void NotifyViewResized(Size size)
    {
        if (browser is null) return;
        browser.Host.WasResized();
    }

    public void SetFocus(bool focused)
    {
        browser?.Host.SetFocus(focused);
    }

    // ——— Ввод ———

    private CefMouseEvent MakeMouseEvent(Point position)
    {
        var scaling = RenderScaling;
        return new CefMouseEvent
        {
            X = (int)Math.Round(position.X * scaling),
            Y = (int)Math.Round(position.Y * scaling),
        };
    }

    public void SendMouseMove(Point position)
    {
        if (browser is null) return;
        browser.Host.SendMouseMoveEvent(MakeMouseEvent(position), mouseLeave: false);
    }

    public void SendMouseLeave()
    {
        if (browser is null) return;
        browser.Host.SendMouseMoveEvent(MakeMouseEvent(new Point(-1, -1)), mouseLeave: true);
    }

    public void SendMouseClick(Point position, PointerUpdateKind kind, bool mouseUp, int clickCount)
    {
        if (browser is null) return;
        browser.Host.SendMouseClickEvent(MakeMouseEvent(position), ToCefButton(kind), mouseUp, clickCount);
    }

    public void SendMouseWheel(Point position, Vector delta)
    {
        if (browser is null) return;
        browser.Host.SendMouseWheelEvent(
            MakeMouseEvent(position),
            (int)Math.Round(delta.X * WheelTickPixels),
            (int)Math.Round(delta.Y * WheelTickPixels));
    }

    private static CefMouseButtonType ToCefButton(PointerUpdateKind kind)
    {
        return kind switch
        {
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => CefMouseButtonType.Right,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => CefMouseButtonType.Middle,
            _ => CefMouseButtonType.Left,
        };
    }

    private const uint FlagShiftDown = 0x02;
    private const uint FlagControlDown = 0x04;
    private const uint FlagAltDown = 0x08;

    public bool SendKey(Key key, KeyModifiers modifiers, bool keyUp)
    {
        if (browser is null) return false;
        var vk = ToWindowsKeyCode(key);
        if (vk == 0) return false;

        var flags = 0u;
        if (modifiers.HasFlag(KeyModifiers.Shift)) flags |= FlagShiftDown;
        if (modifiers.HasFlag(KeyModifiers.Control)) flags |= FlagControlDown;
        if (modifiers.HasFlag(KeyModifiers.Alt)) flags |= FlagAltDown;

        browser.Host.SendKeyEvent(new CefKeyEvent
        {
            Type = keyUp ? CefKeyEventType.KeyUp : CefKeyEventType.RawKeyDown,
            WindowsKeyCode = vk,
            Modifiers = flags,
        });

        // Печатаемые символы Chromium ждёт отдельным Char-событием.
        if (!keyUp && vk >= 32 && vk <= 126)
        {
            var c = (char)vk;
            if ((flags & FlagShiftDown) == 0 && char.IsAsciiLetterUpper(c)) c = char.ToLowerInvariant(c);
            browser.Host.SendKeyEvent(new CefKeyEvent
            {
                Type = CefKeyEventType.Char,
                WindowsKeyCode = vk,
                Character = c,
                UnmodifiedCharacter = c,
                Modifiers = flags,
            });
        }
        return true;
    }

    /// <summary>Avalonia.Key → Win32 VK-код (CFF ожидает VK); 0 = не поддерживается.</summary>
    private static int ToWindowsKeyCode(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return 'A' + (int)key - (int)Key.A;
        if (key >= Key.D0 && key <= Key.D9) return '0' + (int)key - (int)Key.D0;
        if (key >= Key.F1 && key <= Key.F12) return 112 + (int)key - (int)Key.F1;
        return key switch
        {
            Key.Space => 32,
            Key.Enter => 13,
            Key.Escape => 27,
            Key.Back => 8,
            Key.Tab => 9,
            Key.LeftShift or Key.RightShift => 16,
            Key.LeftCtrl or Key.RightCtrl => 17,
            Key.LeftAlt or Key.RightAlt => 18,
            Key.CapsLock => 20,
            Key.Left => 37,
            Key.Up => 38,
            Key.Right => 39,
            Key.Down => 40,
            Key.Delete => 46,
            Key.Home => 36,
            Key.End => 35,
            Key.PageUp => 33,
            Key.PageDown => 34,
            Key.Insert => 45,
            _ => 0,
        };
    }

    /// <summary>Мост: аналог ExecuteScriptAsync — возвращает JSON-текст результата (как WebView2).</summary>
    public async Task<string?> ExecuteScriptAsync(string script)
    {
        if (browser?.MainFrame is not { } frame) return null;
        var id = Interlocked.Increment(ref execId);
        var waiter = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        execWaiters[id] = waiter;
        try
        {
            frame.ExecuteJavaScript(
                $"__laRun({id}, function(){{ return ({script}); }});",
                "https://logic-arrows.io/__la_engine",
                1);
            var timeout = Task.Delay(TimeSpan.FromSeconds(15));
            var finished = await Task.WhenAny(waiter.Task, timeout).ConfigureAwait(false);
            if (finished == timeout) return "null";
            return await waiter.Task.ConfigureAwait(false);
        }
        finally
        {
            execWaiters.Remove(id);
        }
    }

    internal void HandleBridgeEnvelope(JsonDocument envelope)
    {
        try
        {
            var root = envelope.RootElement;
            var kind = root.TryGetProperty("__kind", out var kindEl) ? kindEl.GetString() : null;

            if (kind == "exec")
            {
                var id = root.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var parsed) ? parsed : 0;
                var value = root.TryGetProperty("value", out var valueEl) && valueEl.ValueKind == JsonValueKind.String
                    ? valueEl.GetString()
                    : "null";
                if (execWaiters.TryGetValue(id, out var waiter)) waiter.TrySetResult(value);
                return;
            }

            if (kind == "message")
            {
                if (root.TryGetProperty("payload", out var payload))
                {
                    // payload — это объект {channel, type, …}: дальше его ждёт UI-слой.
                    var payloadJson = payload.GetRawText();
                    BridgeMessageReceived?.Invoke(this, JsonDocument.Parse(payloadJson));
                }
            }
        }
        catch
        {
            // битые конверты моста игнорируем — так же, как Windows-версия.
        }
    }

    // ——— CEF-хендлеры (один клиент на браузер) ———

    private sealed class GameClient : CefClient
    {
        private readonly CefEngine engine;

        public GameClient(CefEngine engine)
        {
            this.engine = engine;
        }

        protected override CefRequestHandler? GetRequestHandler()
            => new LaRequestHandler(engine.ResourceRequestHandler);

        protected override CefLoadHandler? GetLoadHandler() => new GameLoadHandler(engine);

        protected override CefRenderHandler? GetRenderHandler() => new GameRenderHandler(engine);

        protected override CefLifeSpanHandler? GetLifeSpanHandler() => new GameLifeSpanHandler(engine);
    }

    internal LaResourceRequestHandler ResourceRequestHandler { get; private set; } = null!;

    /// <summary>Вызывается после синхронизации — подменяет Synchronizer в перехватчике.</summary>
    public void BindResourceHandler(AssetSynchronizer synchronizer, EventHandler<JsonDocument> bridgeHandler)
    {
        ResourceRequestHandler = new LaResourceRequestHandler(synchronizer, doc =>
        {
            HandleBridgeEnvelope(doc);
            return Task.CompletedTask;
        });
        BridgeMessageReceived += bridgeHandler;
    }

    internal void RaiseMainLoadEnd() => MainFrameLoadEnd?.Invoke(this, EventArgs.Empty);

    private sealed class GameLifeSpanHandler : CefLifeSpanHandler
    {
        private readonly CefEngine engine;

        public GameLifeSpanHandler(CefEngine engine) { this.engine = engine; }

        protected override void OnAfterCreated(CefBrowser browser)
        {
            engine.StartMessagePump();
        }

        protected override bool OnBeforePopup(CefBrowser browser, CefFrame frame, string targetUrl, string targetFrameName,
            CefWindowOpenDisposition targetDisposition, bool userGesture, CefPopupFeatures popupFeatures,
            CefWindowInfo windowInfo, ref CefClient client, CefBrowserSettings settings, ref CefDictionaryValue extraInfo,
            ref int noJavascriptAccess)
        {
            // всплывающие окна игры открываем в системном браузере
            try
            {
                System.Diagnostics.Process.Start("xdg-open", targetUrl);
            }
            catch { }
            return true; // popup в CEF не создаём
        }
    }

    private sealed class GameLoadHandler : CefLoadHandler
    {
        private readonly CefEngine engine;

        public GameLoadHandler(CefEngine engine) { this.engine = engine; }

        protected override void OnLoadStart(CefBrowser browser, CefFrame frame, CefTransitionType transitionType)
        {
            // Аналог AddScriptToExecuteOnDocumentCreatedAsync: shim + мост в каждый документ,
            // до выполнения скриптов страницы (OnLoadStart срабатывает после commit'а навигации).
            if (frame.Url.StartsWith("https://logic-arrows.io", StringComparison.OrdinalIgnoreCase) ||
                frame.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(frame.Url))
            {
                frame.ExecuteJavaScript(BridgeBootstrap + "\n;\n" + MapBridgeScript.Source,
                    "https://logic-arrows.io/__la_bridge_bootstrap", 1);
            }
        }

        protected override void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
        {
            if (frame.IsMain) engine.RaiseMainLoadEnd();
        }
    }

    private sealed class GameRenderHandler : CefRenderHandler
    {
        private readonly CefEngine engine;

        public GameRenderHandler(CefEngine engine) { this.engine = engine; }

        protected override void GetViewRect(CefBrowser browser, ref CefRect rect)
        {
            var scaling = engine.RenderScaling;
            var width = Math.Max(1, (int)Math.Round((engine.view?.Bounds.Width ?? 800) * scaling));
            var height = Math.Max(1, (int)Math.Round((engine.view?.Bounds.Height ?? 600) * scaling));
            rect = new CefRect(0, 0, width, height);
        }

        protected override void OnPaint(CefBrowser browser, CefPaintElementType type, CefRect[] dirtyRects, IntPtr buffer, int width, int height)
        {
            if (type != CefPaintElementType.View) return;
            engine.view?.PresentFrame(buffer, width, height);
        }

        protected override bool GetScreenInfo(CefBrowser browser, ref CefScreenInfo screenInfo)
        {
            screenInfo.DeviceScaleFactor = (float)engine.RenderScaling;
            return true;
        }
    }

    public void Dispose()
    {
        pumpTimer?.Stop();
        CefApi.Shutdown();
    }
}
