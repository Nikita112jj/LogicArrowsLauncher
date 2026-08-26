using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LogicArrowsLauncher;

public sealed class LauncherForm : Form
{
    private readonly Panel header = new();
    private readonly Label headerTitle = new();
    private readonly Label headerDetail = new();
    private readonly Button playButton = new();
    private readonly Button importMapButton = new();
    private readonly WebView2 webView = new();
    private readonly Panel loadingOverlay = new();
    private readonly Panel loadingCard = new();
    private readonly Label loadingTitle = new();
    private readonly Label loadingStatus = new();
    private readonly Label loadingFile = new();
    private readonly ProgressBar loadingProgress = new();
    private readonly Label loadingCount = new();
    private readonly Label loadingError = new();

    private AssetSynchronizer? synchronizer;
    private LocalResourceInterceptor? interceptor;
    private CoreWebView2Controller? webViewController;
    private bool initialized;
    private bool isBusy;
    private bool isGameFullscreen;
    private bool gameWindowChromeVisible;
    private FormBorderStyle launcherBorderStyle;
    private FormWindowState launcherWindowState;
    private Rectangle launcherBounds;
    private TaskCompletionSource<bool>? gamePageReadySignal;
    private bool gamePageReady;
    private MapFileEnvelope? pendingMapImport;
    private bool exportInProgress;

    public LauncherForm()
    {
        Text = "Logic Arrows Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);
        ClientSize = new Size(1280, 800);
        BackColor = Color.FromArgb(24, 24, 32);
        ShowIcon = true;
        try
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            Icon = SystemIcons.Application;
        }
        ControlBox = true;
        MinimizeBox = true;
        MaximizeBox = true;
        launcherBorderStyle = FormBorderStyle.Sizable;
        launcherWindowState = FormWindowState.Normal;
        launcherBounds = Bounds;

        BuildHeader();
        BuildLoadingOverlay();

        webView.Dock = DockStyle.Fill;
        webView.Visible = false;

        Controls.Add(webView);
        Controls.Add(loadingOverlay);
        Controls.Add(header);
        loadingOverlay.Resize += (_, _) => CenterLoadingCard();
        Resize += (_, _) => CenterLoadingCard();
        Shown += LauncherForm_Shown;
    }

    private void BuildHeader()
    {
        header.Dock = DockStyle.Top;
        header.Height = 64;
        header.BackColor = Color.FromArgb(31, 31, 43);
        header.Padding = new Padding(14, 7, 14, 7);

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var headerText = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };

        headerTitle.AutoSize = true;
        headerTitle.ForeColor = Color.White;
        headerTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        headerTitle.Location = new Point(0, 0);
        headerTitle.Text = "Logic Arrows Launcher";

        headerDetail.AutoSize = false;
        headerDetail.ForeColor = Color.FromArgb(190, 190, 200);
        headerDetail.Location = new Point(0, 27);
        headerDetail.Size = new Size(700, 22);
        headerDetail.AutoEllipsis = true;
        headerDetail.Text = "Проверяю сохранённую версию";

        headerText.Controls.Add(headerTitle);
        headerText.Controls.Add(headerDetail);

        playButton.Text = "Играть";
        playButton.AutoSize = true;
        playButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        playButton.MinimumSize = new Size(92, 30);
        playButton.MaximumSize = new Size(108, 30);
        playButton.Height = 30;
        playButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        playButton.Margin = new Padding(8, 5, 0, 5);
        playButton.FlatStyle = FlatStyle.Flat;
        playButton.ForeColor = Color.White;
        playButton.BackColor = Color.FromArgb(38, 145, 92);
        playButton.FlatAppearance.BorderSize = 0;
        playButton.Visible = false;
        playButton.Enabled = false;
        playButton.Click += PlayButton_Click;

        importMapButton.Text = "Импорт .map";
        importMapButton.AutoSize = true;
        importMapButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        importMapButton.MinimumSize = new Size(118, 30);
        importMapButton.MaximumSize = new Size(132, 30);
        importMapButton.Height = 30;
        importMapButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        importMapButton.Margin = new Padding(8, 5, 0, 5);
        importMapButton.FlatStyle = FlatStyle.Flat;
        importMapButton.ForeColor = Color.White;
        importMapButton.BackColor = Color.FromArgb(58, 76, 112);
        importMapButton.FlatAppearance.BorderSize = 0;
        importMapButton.Visible = false;
        importMapButton.Enabled = false;
        importMapButton.Click += ImportMapButton_Click;

        headerLayout.Controls.Add(headerText, 0, 0);
        headerLayout.Controls.Add(importMapButton, 1, 0);
        headerLayout.Controls.Add(playButton, 2, 0);
        header.Controls.Add(headerLayout);
    }

    private void BuildLoadingOverlay()
    {
        loadingOverlay.Dock = DockStyle.Fill;
        loadingOverlay.BackColor = Color.FromArgb(24, 24, 32);
        loadingOverlay.Visible = true;

        loadingCard.Size = new Size(680, 300);
        loadingCard.BackColor = Color.FromArgb(34, 34, 47);
        loadingCard.BorderStyle = BorderStyle.FixedSingle;
        loadingOverlay.Controls.Add(loadingCard);

        loadingTitle.AutoSize = false;
        loadingTitle.ForeColor = Color.White;
        loadingTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        loadingTitle.TextAlign = ContentAlignment.MiddleCenter;
        loadingTitle.Bounds = new Rectangle(28, 22, 624, 34);
        loadingTitle.Text = "Подготовка Logic Arrows";

        loadingStatus.AutoSize = false;
        loadingStatus.ForeColor = Color.FromArgb(114, 168, 255);
        loadingStatus.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        loadingStatus.TextAlign = ContentAlignment.MiddleCenter;
        loadingStatus.Bounds = new Rectangle(28, 64, 624, 28);
        loadingStatus.Text = "Скачивается...";

        loadingFile.AutoSize = false;
        loadingFile.ForeColor = Color.FromArgb(210, 210, 220);
        loadingFile.Font = new Font("Segoe UI", 9.5F);
        loadingFile.TextAlign = ContentAlignment.MiddleCenter;
        loadingFile.AutoEllipsis = true;
        loadingFile.Bounds = new Rectangle(28, 98, 624, 28);
        loadingFile.Text = "Подключаюсь к logic-arrows.io";

        loadingProgress.Minimum = 0;
        loadingProgress.Maximum = 100;
        loadingProgress.Value = 0;
        loadingProgress.Style = ProgressBarStyle.Continuous;
        loadingProgress.UseWaitCursor = false;
        loadingProgress.Bounds = new Rectangle(48, 142, 584, 20);

        loadingCount.AutoSize = false;
        loadingCount.ForeColor = Color.FromArgb(175, 175, 188);
        loadingCount.TextAlign = ContentAlignment.MiddleCenter;
        loadingCount.Bounds = new Rectangle(28, 171, 624, 25);
        loadingCount.Text = "Проверено 0 из 0";

        loadingError.AutoSize = false;
        loadingError.ForeColor = Color.FromArgb(255, 145, 145);
        loadingError.Font = new Font("Segoe UI", 8.5F);
        loadingError.TextAlign = ContentAlignment.MiddleCenter;
        loadingError.AutoEllipsis = true;
        loadingError.Bounds = new Rectangle(28, 202, 624, 72);
        loadingError.Text = "";
        loadingError.Visible = false;

        loadingCard.Controls.Add(loadingTitle);
        loadingCard.Controls.Add(loadingStatus);
        loadingCard.Controls.Add(loadingFile);
        loadingCard.Controls.Add(loadingProgress);
        loadingCard.Controls.Add(loadingCount);
        loadingCard.Controls.Add(loadingError);
        CenterLoadingCard();
    }

    private async void LauncherForm_Shown(object? sender, EventArgs e)
    {
        if (initialized) return;
        initialized = true;
        CenterLoadingCard();
        await StartAsync();
    }

    private async void PlayButton_Click(object? sender, EventArgs e)
    {
        if (isBusy || synchronizer is null || !synchronizer.HasRequiredCache()) return;
        isBusy = true;
        playButton.Enabled = false;
        try
        {
            if (webView.CoreWebView2 is null)
            {
                await InitializeWebViewAsync();
            }
            else
            {
                interceptor?.SetSynchronizer(synchronizer);
                if (!gamePageReady)
                {
                    gamePageReadySignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    webView.CoreWebView2.Reload();
                }
            }
            await WaitForGamePageReadyAsync();
            if (pendingMapImport is not null)
            {
                await ImportPendingMapAsync();
            }
            EnterGameFullscreen();
        }
        catch (Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException)
        {
            ShowLaunchError("Не найден Microsoft Edge WebView2 Runtime. Установи Evergreen Runtime от Microsoft и запусти EXE ещё раз.", true);
        }
        catch (Exception exception)
        {
            ShowLaunchError(exception.Message, false);
        }
        finally
        {
            isBusy = false;
            if (!isGameFullscreen)
            {
                var canPlay = synchronizer?.HasRequiredCache() == true;
                playButton.Enabled = canPlay;
                importMapButton.Enabled = canPlay;
            }
        }
    }

    private void ImportMapButton_Click(object? sender, EventArgs e)
    {
        if (isBusy || isGameFullscreen || synchronizer?.HasRequiredCache() != true) return;

        using var dialog = new OpenFileDialog
        {
            Title = "Импорт карты Logic Arrows",
            Filter = "Logic Arrows map (*.map)|*.map|Все файлы (*.*)|*.*",
            DefaultExt = "map",
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            pendingMapImport = MapFileService.Read(dialog.FileName);
            headerDetail.Text = $"Карта выбрана: {Path.GetFileName(dialog.FileName)}. Нажми «Играть» для импорта";
            loadingFile.Text = "Карта будет импортирована после запуска Logic Arrows";
        }
        catch (Exception exception)
        {
            pendingMapImport = null;
            MessageBox.Show(this, exception.Message, "Не удалось прочитать .map", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task StartAsync()
    {
        if (isBusy || isGameFullscreen) return;
        isBusy = true;
        playButton.Visible = false;
        playButton.Enabled = false;
        importMapButton.Visible = false;
        importMapButton.Enabled = false;
        webView.Visible = false;
        loadingOverlay.Visible = true;
        loadingError.Visible = false;
        loadingTitle.Text = "Подготовка Logic Arrows";
        loadingProgress.Value = 0;
        loadingCount.Text = $"Проверено 0 из {ResourceCatalog.VersionSentinels.Count} ключевых файлов";
        loadingStatus.Text = "Быстро проверяю версию...";
        loadingStatus.ForeColor = Color.FromArgb(114, 168, 255);
        loadingFile.Text = "Подключаюсь к logic-arrows.io";
        headerTitle.Text = "Logic Arrows Launcher";
        headerDetail.Text = "Синхронизирую официальный код в памяти";
        CenterLoadingCard();

        var previousSynchronizer = synchronizer;
        AssetSynchronizer? nextSynchronizer = null;
        try
        {
            var updateStore = new UpdateStore(GetUpdatesDirectory());
            nextSynchronizer = new AssetSynchronizer(updateStore);
            var progress = new Progress<SyncProgress>(UpdateSyncProgress);
            var summary = await nextSynchronizer.SyncAsync(progress, CancellationToken.None);

            synchronizer = nextSynchronizer;
            previousSynchronizer?.Dispose();
            nextSynchronizer = null;
            ShowReadyState(summary);
        }
        catch (Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException)
        {
            nextSynchronizer?.Dispose();
            synchronizer = previousSynchronizer;
            ShowLaunchError("Не найден Microsoft Edge WebView2 Runtime. Установи Evergreen Runtime от Microsoft и запусти EXE ещё раз.", true);
        }
        catch (Exception exception)
        {
            nextSynchronizer?.Dispose();
            synchronizer = previousSynchronizer;
            if (previousSynchronizer?.HasRequiredCache() == true)
            {
                ShowReadyState(null, "Обновление не удалось; доступна предыдущая копия в памяти");
            }
            else
            {
                ShowLaunchError(exception.Message, false);
            }
        }
        finally
        {
            isBusy = false;
        }
    }

    private void ShowReadyState(SyncSummary? summary, string? customMessage = null)
    {
        loadingOverlay.Visible = true;
        webView.Visible = false;
        loadingError.Visible = false;
        loadingTitle.Text = "Logic Arrows готова";
        loadingStatus.Text = summary?.FastVersionChecked == true
            ? "Версия не изменилась"
            : "Загрузка завершена";
        loadingStatus.ForeColor = Color.FromArgb(114, 210, 150);
        loadingFile.Text = summary?.FastVersionChecked == true
            ? "Использую сохранённую версию"
            : "Официальный код получен в память";
        loadingProgress.Value = 100;
        loadingCount.Text = summary is null
            ? "Предыдущая копия готова к запуску"
            : summary.FastVersionChecked
                ? $"Быстрая проверка: {summary.Checked} ключевых файлов, скачано 0"
                : $"{summary.Downloaded} файлов готовы к запуску";
        headerTitle.Text = "Logic Arrows Launcher";
        headerDetail.Text = customMessage ?? "Код готов. Нажми «Играть» — вход в игру; Esc — меню игры; F1 — в лаунчер";
        playButton.Visible = true;
        playButton.Enabled = synchronizer?.HasRequiredCache() == true;
        importMapButton.Visible = true;
        importMapButton.Enabled = playButton.Enabled;
        CenterLoadingCard();
    }

    private void UpdateSyncProgress(SyncProgress progress)
    {
        if (IsDisposed) return;
        var percent = progress.Total <= 0
            ? 0
            : Math.Clamp((int)Math.Round(progress.Completed * 100.0 / progress.Total), 0, 100);
        loadingProgress.Value = percent;
        loadingCount.Text = $"Проверено {progress.Completed} из {progress.Total}  •  {percent}%";
        loadingFile.Text = progress.AssetPath;
        loadingStatus.Text = progress.IsError ? "Ошибка" : progress.Status;
        loadingStatus.ForeColor = progress.IsError
            ? Color.FromArgb(255, 170, 120)
            : Color.FromArgb(114, 168, 255);
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogicArrowsLauncher",
            "webview2-profile");
        Directory.CreateDirectory(userDataDirectory);

        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataDirectory,
            options: null);
        await webView.EnsureCoreWebView2Async(environment);
        gamePageReady = false;
        gamePageReadySignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
        webView.CoreWebView2.NavigationCompleted -= NavigationCompleted;
        webView.CoreWebView2.NavigationCompleted += NavigationCompleted;
        webView.CoreWebView2.WebMessageReceived -= WebMessageReceivedHandler;
        webView.CoreWebView2.WebMessageReceived += WebMessageReceivedHandler;
        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(MapBridgeScript.Source);
        webViewController = GetWebViewController();
        if (webViewController is not null)
        {
            webViewController.AcceleratorKeyPressed -= WebViewAcceleratorKeyPressed;
            webViewController.AcceleratorKeyPressed += WebViewAcceleratorKeyPressed;
        }

        interceptor = new LocalResourceInterceptor(synchronizer!);
        interceptor.Attach(webView.CoreWebView2);
        webView.CoreWebView2.Navigate(ResourceCatalog.Origin + "/");
        await WaitForGamePageReadyAsync();
    }

    private async Task WaitForGamePageReadyAsync()
    {
        if (gamePageReady) return;
        var signal = gamePageReadySignal
            ?? throw new InvalidOperationException("Страница Logic Arrows ещё не начала загрузку.");
        await signal.Task.WaitAsync(TimeSpan.FromSeconds(45));
        gamePageReady = true;
    }

    private void WebMessageReceivedHandler(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) ||
            !string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(source.Host, new Uri(ResourceCatalog.Origin).Host, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("channel", out var channel) ||
                !string.Equals(channel.GetString(), MapBridgeScript.Channel, StringComparison.Ordinal))
            {
                return;
            }

            if (root.TryGetProperty("type", out var type) && type.GetString() == "export-request")
            {
                if (isGameFullscreen && !exportInProgress)
                {
                    _ = ExportCurrentMapAsync();
                }
            }
            else if (root.TryGetProperty("type", out type) && type.GetString() == "bridge-error")
            {
                var message = root.TryGetProperty("message", out var value) ? value.GetString() : null;
                if (!string.IsNullOrWhiteSpace(message)) headerDetail.Text = message;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages from page content.
        }
    }

    private async Task ImportPendingMapAsync()
    {
        var envelope = pendingMapImport;
        if (envelope is null || webView.CoreWebView2 is null) return;

        var payload = JsonSerializer.Serialize(new { data = envelope.Data });
        var script = $"globalThis.__logicArrowsLauncherImport?.({payload})";
        var response = await webView.CoreWebView2.ExecuteScriptAsync(script);
        using var document = JsonDocument.Parse(response);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Bridge импорта карты недоступен.");
        }
        if (root.TryGetProperty("error", out var error))
        {
            throw new InvalidDataException(error.GetString() ?? "Импорт карты отклонён игрой.");
        }
        if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            throw new InvalidDataException("Импорт карты отклонён игрой.");
        }

        pendingMapImport = null;
        loadingFile.Text = "Карта импортирована в Logic Arrows";
    }

    private async Task ExportCurrentMapAsync()
    {
        if (exportInProgress || webView.CoreWebView2 is null) return;
        exportInProgress = true;
        try
        {
            var response = await webView.CoreWebView2.ExecuteScriptAsync(
                "globalThis.__logicArrowsLauncherExport?.() ?? ({error:'Экспорт недоступен'})");
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Экспорт карты недоступен.");
            }
            if (root.TryGetProperty("error", out var error))
            {
                throw new InvalidDataException(error.GetString() ?? "Экспорт карты отклонён.");
            }

            var data = root.TryGetProperty("data", out var dataElement) ? dataElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(data)) throw new InvalidDataException("Игра не вернула данные карты.");
            var version = root.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString()
                : ResourceCatalog.CurrentVersion;
            var mapId = root.TryGetProperty("mapId", out var mapIdElement) ? mapIdElement.GetString() : null;
            var mapName = root.TryGetProperty("mapName", out var mapNameElement) ? mapNameElement.GetString() : null;
            var envelope = new MapFileEnvelope
            {
                SiteVersion = version ?? ResourceCatalog.CurrentVersion,
                MapId = mapId,
                MapName = mapName,
                Data = data,
            };

            using var dialog = new SaveFileDialog
            {
                Title = "Экспорт карты Logic Arrows",
                Filter = "Logic Arrows map (*.map)|*.map|Все файлы (*.*)|*.*",
                DefaultExt = "map",
                AddExtension = true,
                OverwritePrompt = true,
                RestoreDirectory = true,
                FileName = BuildMapFileName(mapName),
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                MapFileService.Write(dialog.FileName, envelope);
                await NotifyMapPageAsync("Экспорт .map сохранён", false);
            }
            else
            {
                await NotifyMapPageAsync("Экспорт отменён", false);
            }
        }
        catch (Exception exception)
        {
            try { await NotifyMapPageAsync(exception.Message, true); } catch { }
        }
        finally
        {
            exportInProgress = false;
        }
    }

    private async Task NotifyMapPageAsync(string message, bool isError)
    {
        if (webView.CoreWebView2 is null) return;
        var safeMessage = JsonSerializer.Serialize(message);
        var errorLiteral = isError ? "true" : "false";
        await webView.CoreWebView2.ExecuteScriptAsync(
            $"globalThis.__logicArrowsLauncherNotify?.({safeMessage}, {errorLiteral})");
    }

    private static string BuildMapFileName(string? mapName)
    {
        var name = string.IsNullOrWhiteSpace(mapName) ? "logic-arrows-map" : mapName.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "logic-arrows-map.map" : $"{name}.map";
    }

    private void EnterGameFullscreen()
    {
        if (isGameFullscreen) return;
        launcherBorderStyle = FormBorderStyle;
        launcherWindowState = WindowState;
        launcherBounds = Bounds;
        isGameFullscreen = true;
        gameWindowChromeVisible = false;
        loadingOverlay.Visible = false;
        header.Visible = false;
        webView.Visible = true;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        QueueGameViewFocus();
    }

    private void ExitGameFullscreen()
    {
        if (!isGameFullscreen) return;
        isGameFullscreen = false;
        gameWindowChromeVisible = false;
        Text = "Logic Arrows Launcher";
        FormBorderStyle = launcherBorderStyle;
        WindowState = launcherWindowState;
        if (!launcherBounds.IsEmpty && launcherWindowState == FormWindowState.Normal)
        {
            Bounds = launcherBounds;
        }
        header.Visible = true;
        ShowReadyState(null, "Игра закрыта в fullscreen. Нажми «Играть», чтобы открыть её снова");
    }

    private void SetGameWindowChromeVisible(bool visible)
    {
        if (!isGameFullscreen) return;
        gameWindowChromeVisible = visible;
        header.Visible = false;
        Text = visible ? "Logic Arrows" : "";

        // Recreate the non-client area while keeping the form maximized.
        SuspendLayout();
        try
        {
            WindowState = FormWindowState.Normal;
            FormBorderStyle = visible ? launcherBorderStyle : FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
        QueueGameViewFocus();
    }

    private void QueueGameViewFocus()
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || !isGameFullscreen) return;
            webView.Visible = true;
            webView.Focus();
            try
            {
                webViewController?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
            }
            catch (COMException)
            {
                // WebView2 can reject focus while its child window is recreating.
            }
            catch (InvalidOperationException)
            {
                // The controller may be between navigation and HWND creation.
            }
            webView.Focus();
        }));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11)
        {
            ToggleGameFullscreen();
            return true;
        }
        if (isGameFullscreen && keyData == Keys.F1)
        {
            ExitGameFullscreen();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void WebViewAcceleratorKeyPressed(
        object? sender,
        CoreWebView2AcceleratorKeyPressedEventArgs e)
    {
        if (e.KeyEventKind is not (CoreWebView2KeyEventKind.KeyDown or CoreWebView2KeyEventKind.SystemKeyDown) ||
            (e.KeyEventLParam & (1u << 30)) != 0)
        {
            return;
        }

        var virtualKey = e.VirtualKey;
        if (virtualKey == (uint)Keys.F11 ||
            (virtualKey == (uint)Keys.F1 && isGameFullscreen))
        {
            e.Handled = true;
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    if (virtualKey == (uint)Keys.F11)
                    {
                        ToggleGameFullscreen();
                    }
                    else if (virtualKey == (uint)Keys.F1 && isGameFullscreen)
                    {
                        ExitGameFullscreen();
                    }
                }));
            }
        }
    }

    private CoreWebView2Controller? GetWebViewController()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        return typeof(WebView2)
            .GetField("_coreWebView2Controller", flags)
            ?.GetValue(webView) as CoreWebView2Controller;
    }

    private void ToggleGameFullscreen()
    {
        if (isGameFullscreen)
        {
            SetGameWindowChromeVisible(!gameWindowChromeVisible);
        }
        else if (!isBusy && synchronizer?.HasRequiredCache() == true)
        {
            playButton.PerformClick();
        }
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            gamePageReady = false;
            gamePageReadySignal?.TrySetException(
                new InvalidOperationException($"Ошибка WebView2: {e.WebErrorStatus}"));
            headerDetail.Text = $"Ошибка WebView2: {e.WebErrorStatus}";
            return;
        }
        gamePageReady = true;
        gamePageReadySignal?.TrySetResult(true);
        if (isGameFullscreen)
        {
            QueueGameViewFocus();
        }
        else
        {
            headerDetail.Text = "Игра готова. Нажми «Играть», чтобы открыть её во весь экран";
        }
    }

    private void ShowLaunchError(string message, bool runtimeError)
    {
        webView.Visible = false;
        loadingOverlay.Visible = true;
        loadingTitle.Text = runtimeError ? "Не найден компонент запуска" : "Не удалось загрузить Logic Arrows";
        loadingStatus.Text = runtimeError ? "Нужен WebView2 Runtime" : "Ошибка загрузки";
        loadingStatus.ForeColor = Color.FromArgb(255, 145, 145);
        loadingFile.Text = "Перезапусти лаунчер после исправления причины";
        loadingCount.Text = "Код в памяти не получен";
        loadingProgress.Value = 0;
        loadingError.Text = message.Length > 620 ? message[..620] + "…" : message;
        loadingError.Visible = true;
        playButton.Visible = false;
        playButton.Enabled = false;
        importMapButton.Visible = false;
        importMapButton.Enabled = false;
        headerTitle.Text = "Logic Arrows Launcher";
        headerDetail.Text = "Запуск остановлен; подробность показана по центру";
        CenterLoadingCard();
    }

    private static string GetUpdatesDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogicArrowsLauncher",
            "updates",
            ResourceCatalog.CurrentVersion);
    }

    private void CenterLoadingCard()
    {
        if (loadingOverlay.ClientSize.Width <= 0 || loadingOverlay.ClientSize.Height <= 0) return;
        loadingCard.Left = Math.Max(12, (loadingOverlay.ClientSize.Width - loadingCard.Width) / 2);
        loadingCard.Top = Math.Max(12, (loadingOverlay.ClientSize.Height - loadingCard.Height) / 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (webViewController is not null)
            {
                webViewController.AcceleratorKeyPressed -= WebViewAcceleratorKeyPressed;
            }
            if (webView.CoreWebView2 is not null)
            {
                webView.CoreWebView2.WebMessageReceived -= WebMessageReceivedHandler;
            }
            synchronizer?.Dispose();
            webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
