using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private const string ReleaseUrl = RepositoryUrl + "/releases/tag/v1.1.5";

    private readonly RoundedPanel header = new();
    private readonly Label headerLogo = new();
    private readonly Label headerSubtitle = new();
    private readonly PillBadge statusBadge = new();
    private readonly RoundedButton updateButton = new();
    private readonly RoundedButton githubButton = new();
    private readonly RoundedButton changelogButton = new();

    private readonly WebView2 webView = new();
    private readonly Panel loadingOverlay = new();
    private readonly RoundedPanel loadingCard = new();

    private readonly Label heroTitle = new();
    private readonly Label heroSubtitle = new();
    private readonly Panel badgesRow = new();

    private readonly Label loadingStatus = new();
    private readonly Label loadingFile = new();
    private readonly RoundedProgressBar loadingProgress = new();
    private readonly Label loadingCount = new();
    private readonly RoundedButton playButton = new();

    private readonly RoundedPanel updateBanner = new();
    private readonly Label updateBannerTitle = new();
    private readonly Label updateBannerNotes = new();
    private readonly RoundedButton updateBannerBtn = new();

    private readonly Label loadingError = new();
    private readonly Label loadingVersion = new();
    private readonly LinkLabel checkUpdatesLink = new();

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
        MinimumSize = new Size(960, 680);
        ClientSize = new Size(1280, 820);
        BackColor = Color.FromArgb(11, 14, 20);
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
        Activated += LauncherForm_Activated;
        Deactivate += LauncherForm_Deactivate;
        Shown += LauncherForm_Shown;
    }

    private void BuildHeader()
    {
        header.Dock = DockStyle.Top;
        header.Height = 72;
        header.BackColor = Color.FromArgb(18, 24, 36);
        header.BorderColor = Color.FromArgb(36, 48, 72);
        header.BorderThickness = 1;
        header.CornerRadius = 0;
        header.Padding = new Padding(24, 12, 20, 12);

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Logo + Title
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Pill Badge
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Spacer
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Update button
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // GitHub
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Changelog
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var logoBox = new Panel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 4, 16, 0),
        };

        headerLogo.AutoSize = true;
        headerLogo.ForeColor = Color.FromArgb(240, 246, 255);
        headerLogo.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        headerLogo.Location = new Point(0, 0);
        headerLogo.Text = "⚡ LOGIC ARROWS";

        headerSubtitle.AutoSize = true;
        headerSubtitle.ForeColor = Color.FromArgb(100, 116, 145);
        headerSubtitle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        headerSubtitle.Location = new Point(2, 22);
        headerSubtitle.Text = "DESKTOP LAUNCHER";

        logoBox.Controls.Add(headerLogo);
        logoBox.Controls.Add(headerSubtitle);

        statusBadge.Text = "Актуальная версия";
        statusBadge.BadgeColor = Color.FromArgb(16, 185, 129);
        statusBadge.BackgroundColor = Color.FromArgb(12, 38, 28);
        statusBadge.BorderColor = Color.FromArgb(22, 101, 52);
        statusBadge.Margin = new Padding(0, 10, 0, 0);

        ConfigureHeaderButton(updateButton, "⚡ Обновить", new Size(130, 38), Color.FromArgb(14, 116, 144));
        updateButton.GradientEndColor = Color.FromArgb(2, 132, 199);
        updateButton.ForeColor = Color.White;
        updateButton.Visible = false;
        updateButton.Click += (_, _) => TriggerAutoUpdate();

        ConfigureHeaderButton(githubButton, "GitHub", new Size(110, 38), Color.FromArgb(28, 36, 52));
        githubButton.Image = LoadEmbeddedImage("LogicArrowsLauncher.github-invertocat-white.png");
        githubButton.Click += (_, _) => OpenExternalUrl(RepositoryUrl);

        ConfigureHeaderButton(changelogButton, "Релизы", new Size(100, 38), Color.FromArgb(28, 36, 52));
        changelogButton.Click += (_, _) => OpenExternalUrl(ReleaseUrl);

        headerLayout.Controls.Add(logoBox, 0, 0);
        headerLayout.Controls.Add(statusBadge, 1, 0);
        headerLayout.Controls.Add(new Panel { BackColor = Color.Transparent }, 2, 0);
        headerLayout.Controls.Add(updateButton, 3, 0);
        headerLayout.Controls.Add(githubButton, 4, 0);
        headerLayout.Controls.Add(changelogButton, 5, 0);
        header.Controls.Add(headerLayout);
    }

    private static void ConfigureHeaderButton(RoundedButton button, string text, Size size, Color color)
    {
        button.Text = text;
        button.Size = size;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(6, 4, 0, 4);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = Color.FromArgb(225, 235, 250);
        button.BackColor = color;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.CornerRadius = 10;
        button.BorderColor = Color.FromArgb(48, 62, 90);
        button.BorderThickness = 1;
        button.Cursor = Cursors.Hand;
    }

    private void BuildLoadingOverlay()
    {
        loadingOverlay.Dock = DockStyle.Fill;
        loadingOverlay.BackColor = Color.FromArgb(11, 14, 20);
        loadingOverlay.Visible = true;
        loadingOverlay.Padding = new Padding(24);

        loadingCard.Size = new Size(720, 540);
        loadingCard.BackColor = Color.FromArgb(19, 25, 38);
        loadingCard.GradientEndColor = Color.FromArgb(14, 18, 28);
        loadingCard.BorderColor = Color.FromArgb(42, 56, 82);
        loadingCard.BorderThickness = 1;
        loadingCard.CornerRadius = 22;
        loadingOverlay.Controls.Add(loadingCard);

        heroTitle.AutoSize = false;
        heroTitle.ForeColor = Color.White;
        heroTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        heroTitle.TextAlign = ContentAlignment.MiddleCenter;
        heroTitle.Bounds = new Rectangle(30, 26, 660, 44);
        heroTitle.Text = "LOGIC ARROWS";

        heroSubtitle.AutoSize = false;
        heroSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
        heroSubtitle.Font = new Font("Segoe UI", 9.5F);
        heroSubtitle.TextAlign = ContentAlignment.MiddleCenter;
        heroSubtitle.Bounds = new Rectangle(30, 72, 660, 24);
        heroSubtitle.Text = "Песочница клеточных автоматов и логических схем";

        badgesRow.Bounds = new Rectangle(120, 104, 480, 28);
        badgesRow.BackColor = Color.Transparent;
        var tag1 = CreateTag("60+ FPS ENGINE", Color.FromArgb(56, 189, 248), 0);
        var tag2 = CreateTag("ОФЛАЙН SNAPSHOT", Color.FromArgb(16, 185, 129), 160);
        var tag3 = CreateTag("КАРТЫ & BASE64", Color.FromArgb(245, 158, 11), 320);
        badgesRow.Controls.Add(tag1);
        badgesRow.Controls.Add(tag2);
        badgesRow.Controls.Add(tag3);

        loadingStatus.AutoSize = false;
        loadingStatus.ForeColor = Color.FromArgb(125, 211, 252);
        loadingStatus.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        loadingStatus.TextAlign = ContentAlignment.MiddleCenter;
        loadingStatus.Bounds = new Rectangle(30, 160, 660, 28);
        loadingStatus.Text = "Проверка игровых ресурсов...";

        loadingFile.AutoSize = false;
        loadingFile.ForeColor = Color.FromArgb(174, 187, 211);
        loadingFile.Font = new Font("Segoe UI", 9F);
        loadingFile.TextAlign = ContentAlignment.MiddleCenter;
        loadingFile.AutoEllipsis = true;
        loadingFile.Bounds = new Rectangle(30, 190, 660, 24);
        loadingFile.Text = "Синхронизация с logic-arrows.io";

        loadingProgress.Progress = 0;
        loadingProgress.TrackColor = Color.FromArgb(30, 38, 56);
        loadingProgress.ProgressColor = Color.FromArgb(56, 189, 248);
        loadingProgress.GradientEndColor = Color.FromArgb(16, 185, 129);
        loadingProgress.Bounds = new Rectangle(50, 224, 620, 12);

        loadingCount.AutoSize = false;
        loadingCount.ForeColor = Color.FromArgb(148, 163, 184);
        loadingCount.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        loadingCount.TextAlign = ContentAlignment.MiddleCenter;
        loadingCount.Bounds = new Rectangle(30, 242, 660, 22);
        loadingCount.Text = "0%  •  0 / 141 проверено";

        playButton.Text = "▶   ИГРАТЬ";
        playButton.Bounds = new Rectangle(70, 286, 580, 56);
        playButton.FlatStyle = FlatStyle.Flat;
        playButton.FlatAppearance.BorderSize = 0;
        playButton.BackColor = Color.FromArgb(16, 185, 129);
        playButton.GradientEndColor = Color.FromArgb(5, 150, 105);
        playButton.ForeColor = Color.White;
        playButton.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        playButton.CornerRadius = 14;
        playButton.BorderColor = Color.FromArgb(52, 211, 153);
        playButton.BorderThickness = 1;
        playButton.Cursor = Cursors.Hand;
        playButton.Visible = false;
        playButton.Enabled = false;
        playButton.Click += PlayButton_Click;

        BuildUpdateBanner();

        loadingError.AutoSize = false;
        loadingError.ForeColor = Color.FromArgb(252, 165, 165);
        loadingError.BackColor = Color.FromArgb(40, 20, 24);
        loadingError.Font = new Font("Segoe UI", 9F);
        loadingError.TextAlign = ContentAlignment.MiddleCenter;
        loadingError.AutoEllipsis = true;
        loadingError.Bounds = new Rectangle(70, 286, 580, 80);
        loadingError.Text = "";
        loadingError.Visible = false;

        loadingVersion.AutoSize = false;
        loadingVersion.ForeColor = Color.FromArgb(100, 116, 145);
        loadingVersion.Font = new Font("Segoe UI", 8.5F);
        loadingVersion.TextAlign = ContentAlignment.MiddleCenter;
        loadingVersion.Bounds = new Rectangle(30, 484, 660, 22);
        loadingVersion.Text = "v1.1.5  •  Windows x64  •  Self-contained  •  ";

        checkUpdatesLink.AutoSize = true;
        checkUpdatesLink.LinkColor = Color.FromArgb(56, 189, 248);
        checkUpdatesLink.ActiveLinkColor = Color.FromArgb(125, 211, 252);
        checkUpdatesLink.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        checkUpdatesLink.Text = "Проверить обновления";
        checkUpdatesLink.Location = new Point(475, 484);
        checkUpdatesLink.LinkClicked += async (_, _) => await ManualCheckUpdatesAsync();

        loadingCard.Controls.Add(heroTitle);
        loadingCard.Controls.Add(heroSubtitle);
        loadingCard.Controls.Add(badgesRow);
        loadingCard.Controls.Add(loadingStatus);
        loadingCard.Controls.Add(loadingFile);
        loadingCard.Controls.Add(loadingProgress);
        loadingCard.Controls.Add(loadingCount);
        loadingCard.Controls.Add(playButton);
        loadingCard.Controls.Add(updateBanner);
        loadingCard.Controls.Add(loadingError);
        loadingCard.Controls.Add(checkUpdatesLink);
        loadingCard.Controls.Add(loadingVersion);

        CenterLoadingCard();
    }

    private void BuildUpdateBanner()
    {
        updateBanner.Bounds = new Rectangle(70, 360, 580, 94);
        updateBanner.BackColor = Color.FromArgb(14, 32, 54);
        updateBanner.BorderColor = Color.FromArgb(2, 132, 199);
        updateBanner.BorderThickness = 1;
        updateBanner.CornerRadius = 14;
        updateBanner.Visible = false;

        updateBannerTitle.AutoSize = false;
        updateBannerTitle.ForeColor = Color.FromArgb(56, 189, 248);
        updateBannerTitle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        updateBannerTitle.Location = new Point(16, 14);
        updateBannerTitle.Size = new Size(370, 26);
        updateBannerTitle.Text = "🚀 Доступно обновление!";

        updateBannerNotes.AutoSize = false;
        updateBannerNotes.ForeColor = Color.FromArgb(186, 230, 253);
        updateBannerNotes.Font = new Font("Segoe UI", 8.5F);
        updateBannerNotes.Location = new Point(16, 42);
        updateBannerNotes.Size = new Size(370, 40);
        updateBannerNotes.AutoEllipsis = true;
        updateBannerNotes.Text = "Нажмите кнопку справа для автоматического обновления.";

        updateBannerBtn.Text = "Обновить";
        updateBannerBtn.Bounds = new Rectangle(400, 22, 160, 48);
        updateBannerBtn.FlatStyle = FlatStyle.Flat;
        updateBannerBtn.FlatAppearance.BorderSize = 0;
        updateBannerBtn.BackColor = Color.FromArgb(2, 132, 199);
        updateBannerBtn.GradientEndColor = Color.FromArgb(37, 99, 235);
        updateBannerBtn.ForeColor = Color.White;
        updateBannerBtn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        updateBannerBtn.CornerRadius = 10;
        updateBannerBtn.Cursor = Cursors.Hand;
        updateBannerBtn.Click += (_, _) => TriggerAutoUpdate();

        updateBanner.Controls.Add(updateBannerTitle);
        updateBanner.Controls.Add(updateBannerNotes);
        updateBanner.Controls.Add(updateBannerBtn);
    }

    private static Label CreateTag(string text, Color color, int x)
    {
        return new Label
        {
            Text = text,
            ForeColor = color,
            BackColor = Color.FromArgb(25, color.R, color.G, color.B),
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(x, 0, 150, 24),
        };
    }

    private async void LauncherForm_Shown(object? sender, EventArgs e)
    {
        if (initialized) return;
        initialized = true;
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
                MessageBox.Show(
                    $"Найдено обновление {update.TagName}!\r\n\r\n{update.ReleaseName}",
                    "Обновление лаунчера",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                statusBadge.Text = "Актуальная версия";
                statusBadge.BadgeColor = Color.FromArgb(16, 185, 129);
                statusBadge.BackgroundColor = Color.FromArgb(12, 38, 28);
                statusBadge.BorderColor = Color.FromArgb(22, 101, 52);
                MessageBox.Show(
                    "У вас установлена самая последняя версия лаунчера (v1.1.5).",
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
        statusBadge.Text = $"Доступно {update.TagName}";
        statusBadge.BadgeColor = Color.FromArgb(56, 189, 248);
        statusBadge.BackgroundColor = Color.FromArgb(14, 38, 58);
        statusBadge.BorderColor = Color.FromArgb(2, 132, 199);
        statusBadge.Width = 160;

        updateButton.Text = $"⚡ {update.TagName}";
        updateButton.Visible = true;

        updateBannerTitle.Text = $"🚀 Доступна версия {update.TagName}!";
        updateBannerNotes.Text = !string.IsNullOrWhiteSpace(update.ReleaseName) ? update.ReleaseName : "Нажмите для автоматического скачивания.";
        updateBanner.Visible = true;
    }

    private async void TriggerAutoUpdate()
    {
        if (isUpdating || availableUpdate is null) return;
        isUpdating = true;
        updateButton.Enabled = false;
        updateBannerBtn.Enabled = false;
        playButton.Enabled = false;

        loadingStatus.Text = $"Скачивание {availableUpdate.TagName} с GitHub...";
        loadingFile.Text = "Пожалуйста, подождите. Лаунчер обновится автоматически.";
        loadingProgress.Progress = 0;

        var progress = new Progress<int>(percent =>
        {
            loadingProgress.Progress = percent;
            loadingCount.Text = $"{percent}% скачано";
        });

        try
        {
            var tempExe = await LauncherUpdater.DownloadUpdateAsync(availableUpdate, progress);
            loadingStatus.Text = "Обновление готово. Перезапуск...";
            await Task.Delay(400);
            LauncherUpdater.ApplyUpdateAndRestart(tempExe);
        }
        catch (Exception ex)
        {
            isUpdating = false;
            updateButton.Enabled = true;
            updateBannerBtn.Enabled = true;
            playButton.Enabled = true;
            loadingStatus.Text = "Ошибка загрузки обновления";
            loadingFile.Text = ex.Message;
            MessageBox.Show(
                $"Не удалось загрузить обновление: {ex.Message}\r\n\r\nВы можете скачать его вручную со страницы релизов.",
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
                ShowLaunchError(
                    "Не удалось подготовить локальный кэш игры.\r\nПроверьте интернет-соединение и запустите лаунчер снова.",
                    false);
                return;
            }

            loadingStatus.Text = "Инициализация движка...";
            loadingFile.Text = "Подготовка WebView2";
            loadingProgress.Progress = 100;
            loadingCount.Text = "100%  •  141 из 141 готово";

            await InitializeWebViewAsync();

            loadingStatus.Text = "Logic Arrows готова!";
            loadingFile.Text = summary.Downloaded > 0
                ? $"Загружено {summary.Downloaded} новых ресурсов"
                : "Все ресурсы проверены и загружены из локального кэша";

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
        loadingCount.Text = $"{percent}%  •  {p.Completed} из {p.Total} файлов";
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
        loadingOverlay.Visible = true;
        header.Visible = true;

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
            gamePageReady = false;
            gamePageReadySignal?.TrySetException(new InvalidOperationException($"Ошибка WebView2: {e.WebErrorStatus}"));
            loadingStatus.Text = $"Ошибка WebView2: {e.WebErrorStatus}";
            return;
        }
        gamePageReady = true;
        gamePageReadySignal?.TrySetResult(true);
        if (isGameFullscreen) QueueGameViewFocus();
    }

    private void ShowLaunchError(string message, bool runtimeError)
    {
        webView.Visible = false;
        loadingOverlay.Visible = true;
        heroTitle.Text = "ОШИБКА ЗАПУСКА";
        heroSubtitle.Text = runtimeError ? "Требуется WebView2 Runtime" : "Не удалось загрузить игру";
        loadingStatus.Text = runtimeError ? "Нужен Microsoft WebView2" : "Ошибка сетевой синхронизации";
        loadingStatus.ForeColor = Color.FromArgb(248, 113, 113);
        loadingFile.Text = "Проверьте подключение к сети и перезапустите лаунчер.";
        loadingProgress.Progress = 0;
        loadingError.Text = message;
        loadingError.Visible = true;
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
            githubButton.Image?.Dispose();
            webView.Dispose();
        }
        base.Dispose(disposing);
    }
}
