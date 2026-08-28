using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LogicArrowsLauncher;

public sealed class LauncherForm : Form
{
    private const int WM_ACTIVATE = 0x0006;
    private const int WM_SETFOCUS = 0x0007;
    private const int WM_ACTIVATEAPP = 0x001C;
    private const string RepositoryUrl = "https://github.com/Nikita112jj/LogicArrowsLauncher";
    private const string ReleaseUrl = RepositoryUrl + "/releases/tag/v1.3.0";

    // Header Controls
    private readonly Panel header = new();
    private readonly Label headerTitle = new();
    private readonly Label headerSubtitle = new();
    private readonly RoundedButton updateButton = new();
    private readonly RoundedButton githubButton = new();
    private readonly RoundedButton changelogButton = new();

    // Game WebView & Main Screen
    private readonly WebView2 webView = new();
    private readonly Panel loadingOverlay = new();
    private readonly RoundedPanel centerCard = new();

    // Game Card Content Controls
    private readonly Label cardTitle = new();
    private readonly Label cardSubtitle = new();
    private readonly Label loadingStatus = new();
    private readonly Label loadingFile = new();
    private readonly RoundedProgressBar loadingProgress = new();
    private readonly Label loadingCount = new();
    private readonly RoundedButton playButton = new();
    private readonly RoundedPanel updateNoticePanel = new();
    private readonly Label updateNoticeLabel = new();
    private readonly RoundedButton updateNoticeBtn = new();
    private readonly Label errorLabel = new();
    private readonly Label versionLabel = new();
    private readonly LinkLabel checkUpdatesLink = new();

    // State & Services
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
    private bool exportInProgress;
    private bool lobbyImportInProgress;
    private int focusRequestId;
    private bool appIsActive = true;

    private UpdateInfo? availableUpdate;
    private bool isUpdating;

    public LauncherForm()
    {
        Text = "Logic Arrows Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 600);
        ClientSize = new Size(1100, 720);
        BackColor = Color.FromArgb(13, 17, 23);
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
        BuildCenterCard();
        BuildLoadingOverlay();

        webView.Dock = DockStyle.Fill;
        webView.Visible = false;

        Controls.Add(webView);
        Controls.Add(loadingOverlay);
        Controls.Add(header);

        loadingOverlay.Resize += (_, _) => CenterLoadingCard();
        Resize += (_, _) => CenterLoadingCard();
        Activated += LauncherForm_Activated;
        Deactivate += LauncherForm_Deactivate;
        Shown += LauncherForm_Shown;
    }

    private void BuildHeader()
    {
        header.Dock = DockStyle.Top;
        header.Height = 60;
        header.BackColor = Color.FromArgb(22, 27, 34);
        header.Padding = new Padding(20, 10, 20, 10);
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Title & Subtitle
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Spacer
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Update
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // GitHub
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Changelog
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleBox = new Panel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 0, 0),
        };

        headerTitle.AutoSize = true;
        headerTitle.ForeColor = Color.FromArgb(240, 246, 252);
        headerTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        headerTitle.Location = new Point(0, 0);
        headerTitle.Text = "Logic Arrows";

        headerSubtitle.AutoSize = true;
        headerSubtitle.ForeColor = Color.FromArgb(139, 148, 158);
        headerSubtitle.Font = new Font("Segoe UI", 8.5F);
        headerSubtitle.Location = new Point(0, 20);
        headerSubtitle.Text = "Лаунчер";

        titleBox.Controls.Add(headerTitle);
        titleBox.Controls.Add(headerSubtitle);

        ConfigureHeaderButton(updateButton, "⚡ Обновить", new Size(135, 34), Color.FromArgb(31, 111, 235));
        updateButton.BorderColor = Color.FromArgb(56, 139, 253);
        updateButton.ForeColor = Color.White;
        updateButton.Visible = false;
        updateButton.Click += (_, _) => TriggerAutoUpdate();

        ConfigureHeaderButton(githubButton, "GitHub", new Size(90, 34), Color.FromArgb(33, 38, 45));
        githubButton.Image = LoadEmbeddedImage("LogicArrowsLauncher.github-invertocat-white.png");
        githubButton.Click += (_, _) => OpenExternalUrl(RepositoryUrl);

        ConfigureHeaderButton(changelogButton, "Релизы", new Size(85, 34), Color.FromArgb(33, 38, 45));
        changelogButton.Click += (_, _) => OpenExternalUrl(ReleaseUrl);

        layout.Controls.Add(titleBox, 0, 0);
        layout.Controls.Add(new Panel { BackColor = Color.Transparent }, 1, 0);
        layout.Controls.Add(updateButton, 2, 0);
        layout.Controls.Add(githubButton, 3, 0);
        layout.Controls.Add(changelogButton, 4, 0);
        header.Controls.Add(layout);
    }

    private static void ConfigureHeaderButton(RoundedButton button, string text, Size size, Color color)
    {
        button.Text = text;
        button.Size = size;
        button.Margin = new Padding(6, 2, 0, 2);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = Color.FromArgb(201, 209, 217);
        button.BackColor = color;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        button.CornerRadius = 6;
        button.BorderColor = Color.FromArgb(48, 54, 61);
        button.BorderThickness = 1;
        button.HoverBackColor = Color.FromArgb(48, 54, 61);
        button.Cursor = Cursors.Hand;
    }

    private void BuildCenterCard()
    {
        centerCard.Size = new Size(560, 420);
        centerCard.BackColor = Color.FromArgb(22, 27, 34);
        centerCard.BorderColor = Color.FromArgb(48, 54, 61);
        centerCard.BorderThickness = 1;
        centerCard.CornerRadius = 12;

        cardTitle.AutoSize = false;
        cardTitle.ForeColor = Color.FromArgb(240, 246, 252);
        cardTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        cardTitle.TextAlign = ContentAlignment.MiddleCenter;
        cardTitle.Bounds = new Rectangle(20, 30, 520, 36);
        cardTitle.Text = "Logic Arrows";

        cardSubtitle.AutoSize = false;
        cardSubtitle.ForeColor = Color.FromArgb(139, 148, 158);
        cardSubtitle.Font = new Font("Segoe UI", 9.5F);
        cardSubtitle.TextAlign = ContentAlignment.MiddleCenter;
        cardSubtitle.Bounds = new Rectangle(20, 68, 520, 22);
        cardSubtitle.Text = "Официальный клиент игры";

        // Progress Section
        loadingStatus.AutoSize = false;
        loadingStatus.ForeColor = Color.FromArgb(88, 166, 255);
        loadingStatus.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        loadingStatus.TextAlign = ContentAlignment.MiddleCenter;
        loadingStatus.Bounds = new Rectangle(20, 140, 520, 24);
        loadingStatus.Text = "Проверка ресурсов...";

        loadingFile.AutoSize = false;
        loadingFile.ForeColor = Color.FromArgb(139, 148, 158);
        loadingFile.Font = new Font("Segoe UI", 8.5F);
        loadingFile.TextAlign = ContentAlignment.MiddleCenter;
        loadingFile.AutoEllipsis = true;
        loadingFile.Bounds = new Rectangle(40, 166, 480, 20);
        loadingFile.Text = "Синхронизация с logic-arrows.io";

        loadingProgress.Progress = 0;
        loadingProgress.TrackColor = Color.FromArgb(33, 38, 45);
        loadingProgress.ProgressColor = Color.FromArgb(35, 134, 54);
        loadingProgress.Bounds = new Rectangle(50, 196, 460, 6);

        loadingCount.AutoSize = false;
        loadingCount.ForeColor = Color.FromArgb(139, 148, 158);
        loadingCount.Font = new Font("Segoe UI", 8.5F);
        loadingCount.TextAlign = ContentAlignment.MiddleCenter;
        loadingCount.Bounds = new Rectangle(20, 208, 520, 20);
        loadingCount.Text = "0%";

        // Play Button
        playButton.Text = "ИГРАТЬ";
        playButton.Bounds = new Rectangle(50, 246, 460, 48);
        playButton.FlatStyle = FlatStyle.Flat;
        playButton.FlatAppearance.BorderSize = 0;
        playButton.BackColor = Color.FromArgb(35, 134, 54);
        playButton.HoverBackColor = Color.FromArgb(46, 160, 67);
        playButton.PressedBackColor = Color.FromArgb(29, 110, 44);
        playButton.ForeColor = Color.White;
        playButton.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        playButton.CornerRadius = 8;
        playButton.BorderThickness = 0;
        playButton.Cursor = Cursors.Hand;
        playButton.Visible = false;
        playButton.Enabled = false;
        playButton.Click += PlayButton_Click;

        // Update notice banner
        updateNoticePanel.Bounds = new Rectangle(50, 304, 460, 48);
        updateNoticePanel.BackColor = Color.FromArgb(13, 27, 44);
        updateNoticePanel.BorderColor = Color.FromArgb(31, 111, 235);
        updateNoticePanel.BorderThickness = 1;
        updateNoticePanel.CornerRadius = 8;
        updateNoticePanel.Visible = false;

        updateNoticeLabel.AutoSize = false;
        updateNoticeLabel.ForeColor = Color.FromArgb(88, 166, 255);
        updateNoticeLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        updateNoticeLabel.TextAlign = ContentAlignment.MiddleLeft;
        updateNoticeLabel.Location = new Point(12, 0);
        updateNoticeLabel.Size = new Size(310, 48);
        updateNoticeLabel.Text = "Доступно обновление лаунчера";

        updateNoticeBtn.Text = "Обновить";
        updateNoticeBtn.Bounds = new Rectangle(330, 8, 118, 32);
        updateNoticeBtn.FlatStyle = FlatStyle.Flat;
        updateNoticeBtn.FlatAppearance.BorderSize = 0;
        updateNoticeBtn.BackColor = Color.FromArgb(31, 111, 235);
        updateNoticeBtn.HoverBackColor = Color.FromArgb(56, 139, 253);
        updateNoticeBtn.ForeColor = Color.White;
        updateNoticeBtn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        updateNoticeBtn.CornerRadius = 6;
        updateNoticeBtn.Cursor = Cursors.Hand;
        updateNoticeBtn.Click += (_, _) => TriggerAutoUpdate();

        updateNoticePanel.Controls.Add(updateNoticeLabel);
        updateNoticePanel.Controls.Add(updateNoticeBtn);

        // Error message
        errorLabel.AutoSize = false;
        errorLabel.ForeColor = Color.FromArgb(248, 81, 73);
        errorLabel.BackColor = Color.FromArgb(33, 16, 20);
        errorLabel.Font = new Font("Segoe UI", 8.5F);
        errorLabel.TextAlign = ContentAlignment.MiddleCenter;
        errorLabel.AutoEllipsis = true;
        errorLabel.Bounds = new Rectangle(50, 246, 460, 60);
        errorLabel.Text = "";
        errorLabel.Visible = false;

        // Footer version & check updates
        var footerPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };

        versionLabel.AutoSize = true;
        versionLabel.ForeColor = Color.FromArgb(139, 148, 158);
        versionLabel.Font = new Font("Segoe UI", 8.5F);
        versionLabel.Text = $"v{LauncherUpdater.CurrentVersion.Major}.{LauncherUpdater.CurrentVersion.Minor}.{LauncherUpdater.CurrentVersion.Build}  •  Windows x64  •  ";

        checkUpdatesLink.AutoSize = true;
        checkUpdatesLink.LinkColor = Color.FromArgb(88, 166, 255);
        checkUpdatesLink.ActiveLinkColor = Color.FromArgb(121, 192, 255);
        checkUpdatesLink.VisitedLinkColor = Color.FromArgb(88, 166, 255);
        checkUpdatesLink.Font = new Font("Segoe UI", 8.5F);
        checkUpdatesLink.Text = "Проверить обновления";
        checkUpdatesLink.LinkClicked += async (_, _) => await ManualCheckUpdatesAsync();

        footerPanel.Controls.Add(versionLabel);
        footerPanel.Controls.Add(checkUpdatesLink);

        centerCard.Controls.Add(cardTitle);
        centerCard.Controls.Add(cardSubtitle);
        centerCard.Controls.Add(loadingStatus);
        centerCard.Controls.Add(loadingFile);
        centerCard.Controls.Add(loadingProgress);
        centerCard.Controls.Add(loadingCount);
        centerCard.Controls.Add(playButton);
        centerCard.Controls.Add(updateNoticePanel);
        centerCard.Controls.Add(errorLabel);

        footerPanel.Location = new Point(135, 375);
        centerCard.Controls.Add(footerPanel);
    }

    private void BuildLoadingOverlay()
    {
        loadingOverlay.Dock = DockStyle.Fill;
        loadingOverlay.BackColor = Color.FromArgb(13, 17, 23);
        loadingOverlay.Visible = true;
        loadingOverlay.Controls.Add(centerCard);
        CenterLoadingCard();
    }

    private static void OpenMapsFolderInExplorer()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogicArrowsLauncher");

        if (!Directory.Exists(appDataFolder))
        {
            Directory.CreateDirectory(appDataFolder);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{appDataFolder}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CenterLoadingCard()
    {
        if (loadingOverlay.ClientSize.Width <= 0 || loadingOverlay.ClientSize.Height <= 0) return;
        var x = Math.Max(12, (loadingOverlay.ClientSize.Width - centerCard.Width) / 2);
        var y = Math.Max(12, (loadingOverlay.ClientSize.Height - centerCard.Height) / 2);
        centerCard.Location = new Point(x, y);
    }

    private async void LauncherForm_Shown(object? sender, EventArgs e)
    {
        if (initialized) return;
        initialized = true;
        CenterLoadingCard();
        _ = CheckUpdatesInBackgroundAsync();
        await InitializeLauncherAsync();
    }

    private async Task CheckUpdatesInBackgroundAsync()
    {
        try
        {
            var update = await LauncherUpdater.CheckForUpdatesAsync();
            if (update is not null && !IsDisposed)
            {
                availableUpdate = update;
                BeginInvoke(new Action(() => ShowAvailableUpdate(update)));
            }
        }
        catch { }
    }

    private async Task ManualCheckUpdatesAsync()
    {
        checkUpdatesLink.Text = "Проверяю...";
        checkUpdatesLink.Enabled = false;
        try
        {
            var update = await LauncherUpdater.CheckForUpdatesAsync();
            if (update is not null)
            {
                availableUpdate = update;
                ShowAvailableUpdate(update);
                string title = update.IsPatch ? "Мини-патч" : "Обновление лаунчера";
                MessageBox.Show(
                    $"Найдено обновление {update.TagName}!\r\n\r\n{update.ReleaseName}",
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"У вас установлена последняя версия лаунчера (v{LauncherUpdater.CurrentVersion.Major}.{LauncherUpdater.CurrentVersion.Minor}.{LauncherUpdater.CurrentVersion.Build}).",
                    "Обновлений нет",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось проверить обновления: {ex.Message}",
                "Ошибка проверки",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            checkUpdatesLink.Text = "Проверить обновления";
            checkUpdatesLink.Enabled = true;
        }
    }

    private void ShowAvailableUpdate(UpdateInfo update)
    {
        if (update.IsPatch)
        {
            updateButton.Text = $"⚡ Патч {update.TagName}";
            updateNoticeLabel.Text = $"Доступен мини-патч {update.TagName}";
            updateNoticeBtn.Text = "Обновить патч";
        }
        else
        {
            updateButton.Text = $"⚡ {update.TagName}";
            updateNoticeLabel.Text = $"Доступна версия {update.TagName}";
            updateNoticeBtn.Text = "Обновить";
        }
        updateButton.Visible = true;
        updateNoticePanel.Visible = true;
    }

    private async void TriggerAutoUpdate()
    {
        if (isUpdating || availableUpdate is null) return;
        isUpdating = true;
        updateButton.Enabled = false;
        updateNoticeBtn.Enabled = false;
        playButton.Enabled = false;

        string label = availableUpdate.IsPatch ? "патча" : "обновления";
        loadingStatus.Text = $"Скачивание {label} {availableUpdate.TagName}...";
        loadingFile.Text = "Обновление лаунчера с GitHub";
        loadingProgress.Progress = 0;

        var progress = new Progress<int>(percent =>
        {
            loadingProgress.Progress = percent;
            loadingCount.Text = $"{percent}%";
        });

        try
        {
            var tempExe = await LauncherUpdater.DownloadUpdateAsync(availableUpdate, progress);
            loadingStatus.Text = "Перезапуск...";
            await Task.Delay(300);
            LauncherUpdater.ApplyUpdateAndRestart(tempExe);
        }
        catch (Exception ex)
        {
            isUpdating = false;
            updateButton.Enabled = true;
            updateNoticeBtn.Enabled = true;
            playButton.Enabled = true;
            loadingStatus.Text = "Ошибка загрузки";
            loadingFile.Text = ex.Message;
            MessageBox.Show(
                $"Не удалось загрузить обновление: {ex.Message}",
                "Ошибка обновления",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task InitializeLauncherAsync()
    {
        isBusy = true;
        try
        {
            var updatesDirectory = GetUpdatesDirectory();
            var updateStore = new UpdateStore(updatesDirectory);
            synchronizer = new AssetSynchronizer(updateStore);
            interceptor = new LocalResourceInterceptor(synchronizer);

            var progress = new Progress<SyncProgress>(ReportProgress);
            var summary = await synchronizer.SyncAsync(progress, CancellationToken.None);

            if (!synchronizer.HasRequiredCache())
            {
                ShowLaunchError("Не удалось подготовить локальный кэш игры.\r\nПроверьте соединение с сетью.", false);
                return;
            }

            loadingStatus.Text = "Подготовка движка...";
            loadingFile.Text = "Загрузка компонентов WebView2";
            loadingProgress.Progress = 100;
            loadingCount.Text = "100%";

            await InitializeWebViewAsync();

            loadingStatus.Text = "Игра готова к запуску";
            loadingStatus.ForeColor = Color.FromArgb(63, 185, 80);
            loadingFile.Text = summary.Downloaded > 0
                ? $"Загружено {summary.Downloaded} новых файлов"
                : "Все 141 файл проверены локально";

            playButton.Visible = true;
            playButton.Enabled = true;
            playButton.Focus();
        }
        catch (Exception ex)
        {
            ShowLaunchError(ex.Message, false);
        }
        finally
        {
            isBusy = false;
        }
    }

    private void ReportProgress(SyncProgress p)
    {
        var percent = p.Total > 0 ? (int)((p.Completed * 100) / p.Total) : 0;
        loadingProgress.Progress = percent;
        loadingCount.Text = $"{p.Completed} из {p.Total} ({percent}%)";
        loadingFile.Text = p.AssetPath;
        loadingStatus.Text = p.Status;
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogicArrowsLauncher",
            "profile");

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await webView.EnsureCoreWebView2Async(env);

        webViewController = GetWebViewController();
        if (webViewController is not null)
        {
            webViewController.AcceleratorKeyPressed += WebViewAcceleratorKeyPressed;
        }

        webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

        interceptor?.Attach(webView.CoreWebView2);
        webView.CoreWebView2.NavigationCompleted += NavigationCompleted;
        webView.CoreWebView2.WebMessageReceived += WebMessageReceivedHandler;

        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(MapBridgeScript.Source);

        gamePageReadySignal = new TaskCompletionSource<bool>();
        webView.CoreWebView2.Navigate("https://logic-arrows.io/");
        await gamePageReadySignal.Task;
    }

    private void PlayButton_Click(object? sender, EventArgs e)
    {
        if (isBusy || !synchronizer?.HasRequiredCache() == true) return;
        EnterGameFullscreen();
    }

    private void EnterGameFullscreen()
    {
        isGameFullscreen = true;
        header.Visible = false;
        loadingOverlay.Visible = false;
        webView.Visible = true;

        launcherWindowState = WindowState;
        launcherBounds = Bounds;
        launcherBorderStyle = FormBorderStyle;

        SetGameWindowChromeVisible(false);
        QueueGameViewFocus();
    }

    private void ExitGameFullscreen()
    {
        isGameFullscreen = false;
        webView.Visible = false;
        header.Visible = true;
        loadingOverlay.Visible = true;

        FormBorderStyle = launcherBorderStyle;
        WindowState = launcherWindowState;
        Bounds = launcherBounds;
        CenterLoadingCard();
    }

    private void SetGameWindowChromeVisible(bool visible)
    {
        gameWindowChromeVisible = visible;
        if (!visible)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
            Bounds = launcherBounds;
        }
    }

    private void WebMessageReceivedHandler(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var type))
            {
                var typeStr = type.GetString();
                if (typeStr == "export-request")
                {
                    if (isGameFullscreen && !exportInProgress) _ = ExportCurrentMapAsync();
                }
                else if (typeStr == "import-request")
                {
                    var text = root.TryGetProperty("text", out var textElement) ? textElement.GetString() : null;
                    if (isGameFullscreen && !lobbyImportInProgress && text is not null)
                    {
                        _ = ImportFromLobbyAsync(text);
                    }
                }
                else if (typeStr == "open-maps-folder")
                {
                    OpenMapsFolderInExplorer();
                }
                else if (typeStr == "bridge-error")
                {
                    var message = root.TryGetProperty("message", out var val) ? val.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        headerSubtitle.Text = message;
                    }
                }
            }
        }
        catch { }
    }

    private async Task ImportFromLobbyAsync(string text)
    {
        if (lobbyImportInProgress || webView.CoreWebView2 is null) return;
        lobbyImportInProgress = true;
        try
        {
            var envelope = MapFileService.ReadText(text);
            var payload = JsonSerializer.Serialize(new { data = envelope.Data, name = envelope.MapName });
            var stageResponse = await webView.CoreWebView2.ExecuteScriptAsync(
                $"globalThis.__logicArrowsLauncherStageLobbyImport?.({payload}) ?? ({{}})");
            EnsureBridgeSuccess(stageResponse, "Не удалось подготовить импорт карты.");

            var openResponse = await webView.CoreWebView2.ExecuteScriptAsync(
                "globalThis.__logicArrowsLauncherOpenNewMap?.() ?? ({error:'Кнопка новой карты недоступна.'})");
            EnsureBridgeSuccess(openResponse, "Не удалось открыть новую карту.");
        }
        catch (Exception exception)
        {
            try { await NotifyMapPageAsync(exception.Message, true); } catch { }
        }
        finally
        {
            lobbyImportInProgress = false;
        }
    }

    private async Task ExportCurrentMapAsync()
    {
        if (exportInProgress || webView.CoreWebView2 is null) return;
        exportInProgress = true;
        try
        {
            var response = await webView.CoreWebView2.ExecuteScriptAsync(
                "globalThis.__logicArrowsLauncherExport?.() ?? ({error:'Функция экспорта недоступна.'})");

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errorProp))
            {
                throw new InvalidDataException(errorProp.GetString() ?? "Ошибка экспорта.");
            }

            var data = root.GetProperty("data").GetString()!;
            var mapId = root.TryGetProperty("mapId", out var idProp) ? idProp.GetString() : null;
            var mapName = root.TryGetProperty("mapName", out var nameProp) ? nameProp.GetString() : null;

            var envelope = new MapFileEnvelope
            {
                MapId = mapId,
                MapName = mapName,
                Data = data,
            };

            var defaultName = string.IsNullOrWhiteSpace(mapName) ? (mapId ?? "map") : mapName;
            foreach (var c in Path.GetInvalidFileNameChars()) defaultName = defaultName.Replace(c, '_');

            using var dialog = new SaveFileDialog
            {
                Title = "Экспорт карты Logic Arrows",
                Filter = "Logic Arrows Map (*.map)|*.map",
                FileName = $"{defaultName}.map",
                DefaultExt = "map",
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                MapFileService.Write(dialog.FileName, envelope);
                await NotifyMapPageAsync("Карта сохранена в .map");
            }
            else
            {
                await NotifyMapPageAsync("Экспорт отменён");
            }
        }
        catch (Exception ex)
        {
            try { await NotifyMapPageAsync(ex.Message, true); } catch { }
        }
        finally
        {
            exportInProgress = false;
        }
    }

    private async Task NotifyMapPageAsync(string message, bool isError = false)
    {
        if (webView.CoreWebView2 is null) return;
        var encoded = JsonSerializer.Serialize(message);
        var errStr = isError ? "true" : "false";
        await webView.CoreWebView2.ExecuteScriptAsync(
            $"globalThis.__logicArrowsLauncherNotify?.({encoded}, {errStr});");
    }

    private static void EnsureBridgeSuccess(string response, string fallback)
    {
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err))
        {
            throw new InvalidDataException(err.GetString() ?? fallback);
        }
        if (!root.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        {
            throw new InvalidDataException(fallback);
        }
    }

    private void LauncherForm_Activated(object? sender, EventArgs e)
    {
        appIsActive = true;
        if (isGameFullscreen) QueueGameViewFocus();
    }

    private void LauncherForm_Deactivate(object? sender, EventArgs e)
    {
        appIsActive = false;
        focusRequestId++;
    }

    private async void QueueGameViewFocus()
    {
        if (IsDisposed || !IsHandleCreated) return;
        var req = ++focusRequestId;
        for (var i = 0; i < 5; i++)
        {
            if (i > 0) await Task.Delay(80);
            if (IsDisposed || !isGameFullscreen || !appIsActive || req != focusRequestId) return;
            webView.Visible = true;
            webView.Focus();
            try { webViewController?.MoveFocus(CoreWebView2MoveFocusReason.Programmatic); } catch { }
            try
            {
                if (webView.CoreWebView2 is not null)
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(
                        "globalThis.__logicArrowsLauncherRecoverInput?.(); globalThis.focus?.(); document.querySelector('canvas')?.focus?.({preventScroll:true});");
                }
            }
            catch { }
        }
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

    private void WebViewAcceleratorKeyPressed(object? sender, CoreWebView2AcceleratorKeyPressedEventArgs e)
    {
        if (e.KeyEventKind is not (CoreWebView2KeyEventKind.KeyDown or CoreWebView2KeyEventKind.SystemKeyDown) ||
            (e.KeyEventLParam & (1u << 30)) != 0) return;

        var key = e.VirtualKey;
        if (key == (uint)Keys.F11 || (key == (uint)Keys.F1 && isGameFullscreen))
        {
            e.Handled = true;
            if (!IsDisposed && IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    if (key == (uint)Keys.F11) ToggleGameFullscreen();
                    else if (key == (uint)Keys.F1 && isGameFullscreen) ExitGameFullscreen();
                }));
            }
        }
    }

    private CoreWebView2Controller? GetWebViewController()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        return typeof(WebView2).GetField("_coreWebView2Controller", flags)?.GetValue(webView) as CoreWebView2Controller;
    }

    private void ToggleGameFullscreen()
    {
        if (isGameFullscreen) SetGameWindowChromeVisible(!gameWindowChromeVisible);
        else if (!isBusy && synchronizer?.HasRequiredCache() == true) playButton.PerformClick();
    }

    private void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            gamePageReadySignal?.TrySetException(new InvalidOperationException($"Ошибка WebView2: {e.WebErrorStatus}"));
            loadingStatus.Text = $"Ошибка WebView2: {e.WebErrorStatus}";
            return;
        }
        gamePageReadySignal?.TrySetResult(true);
        if (isGameFullscreen) QueueGameViewFocus();
    }

    private void ShowLaunchError(string message, bool runtimeError)
    {
        webView.Visible = false;
        loadingOverlay.Visible = true;
        loadingStatus.Text = runtimeError ? "Нужен Microsoft WebView2" : "Ошибка сетевой синхронизации";
        loadingStatus.ForeColor = Color.FromArgb(248, 81, 73);
        loadingFile.Text = "Проверьте подключение к сети и перезапустите лаунчер.";
        loadingProgress.Progress = 0;
        errorLabel.Text = message;
        errorLabel.Visible = true;
        playButton.Visible = false;
        CenterLoadingCard();
    }

    private static Image? LoadEmbeddedImage(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null) return null;
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private void OpenExternalUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
    }

    private static string GetUpdatesDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LogicArrowsLauncher",
            "updates",
            ResourceCatalog.CurrentVersion);
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
            githubButton.Image?.Dispose();
            webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
