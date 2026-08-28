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
    private const string ReleaseUrl = RepositoryUrl + "/releases/tag/v1.3.0";

    // Header Controls
    private readonly Panel header = new();
    private readonly Label headerTitle = new();
    private readonly Label headerSubtitle = new();
    private readonly RoundedButton tabGameBtn = new();
    private readonly RoundedButton tabPreviewBtn = new();
    private readonly RoundedButton updateButton = new();
    private readonly RoundedButton githubButton = new();
    private readonly RoundedButton changelogButton = new();

    // Tab 1: Game WebView & Main Screen
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

    // Tab 2: Preview & Optimization Studio
    private readonly Panel previewPanel = new();
    private readonly Panel previewTopBar = new();
    private readonly TextBox mapInputBox = new();
    private readonly RoundedButton pasteBtn = new();
    private readonly RoundedButton openFileBtn = new();
    private readonly RoundedButton optimizeBtn = new();
    private readonly RoundedButton exportMapBtn = new();
    private readonly RoundedButton openFolderBtn = new();
    private readonly RoundedButton centerViewBtn = new();
    private readonly MapPreviewControl previewControl = new();

    // Preview Sidebar
    private readonly Panel previewSidebar = new();
    private readonly RoundedPanel statsCard = new();
    private readonly Label statsTitle = new();
    private readonly Label statsInfo = new();

    private readonly RoundedPanel optCard = new();
    private readonly Label optTitle = new();
    private readonly Label optInfo = new();
    private readonly RoundedButton copyOptBtn = new();

    private readonly RoundedPanel guideCard = new();
    private readonly Label guideTitle = new();
    private readonly Label guideText = new();
    private readonly RoundedButton guideFolderBtn = new();

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
    private int currentTab = 0; // 0: Game, 1: Preview

    private UpdateInfo? availableUpdate;
    private bool isUpdating;

    private MapBlueprint? currentBlueprint;
    private OptimizationResult? lastOptResult;

    public LauncherForm()
    {
        Text = "Logic Arrows Launcher";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 680);
        ClientSize = new Size(1180, 760);
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
        BuildPreviewStudio();

        webView.Dock = DockStyle.Fill;
        webView.Visible = false;

        // Docking order
        Controls.Add(webView);
        Controls.Add(loadingOverlay);
        Controls.Add(previewPanel);
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
        header.Padding = new Padding(16, 10, 16, 10);
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Title & Subtitle
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Tab: Game
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Tab: Preview & Optimizer
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Spacer
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Update
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // GitHub
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Changelog
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var titleBox = new Panel
        {
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 20, 0),
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

        // Tab Game Button
        ConfigureTabButton(tabGameBtn, "🎮  Игра", true);
        tabGameBtn.Click += (_, _) => SwitchTab(0);

        // Tab Preview & Optimizer Button (with Lucide Eye glyph)
        ConfigureTabButton(tabPreviewBtn, "👁  Превью и Схемы", false);
        tabPreviewBtn.Click += (_, _) => SwitchTab(1);

        ConfigureHeaderButton(updateButton, "⚡ Обновить", new Size(125, 34), Color.FromArgb(31, 111, 235));
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
        layout.Controls.Add(tabGameBtn, 1, 0);
        layout.Controls.Add(tabPreviewBtn, 2, 0);
        layout.Controls.Add(new Panel { BackColor = Color.Transparent }, 3, 0);
        layout.Controls.Add(updateButton, 4, 0);
        layout.Controls.Add(githubButton, 5, 0);
        layout.Controls.Add(changelogButton, 6, 0);
        header.Controls.Add(layout);
    }

    private static void ConfigureTabButton(RoundedButton button, string text, bool active)
    {
        button.Text = text;
        button.Size = new Size(160, 36);
        button.Margin = new Padding(4, 2, 4, 2);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI", 9.5F, active ? FontStyle.Bold : FontStyle.Regular);
        button.CornerRadius = 6;
        button.BorderThickness = 1;
        button.Cursor = Cursors.Hand;
        UpdateTabButtonStyle(button, active);
    }

    private static void UpdateTabButtonStyle(RoundedButton button, bool active)
    {
        if (active)
        {
            button.BackColor = Color.FromArgb(31, 111, 235);
            button.BorderColor = Color.FromArgb(56, 139, 253);
            button.ForeColor = Color.White;
            button.HoverBackColor = Color.FromArgb(56, 139, 253);
        }
        else
        {
            button.BackColor = Color.FromArgb(22, 27, 34);
            button.BorderColor = Color.FromArgb(48, 54, 61);
            button.ForeColor = Color.FromArgb(201, 209, 217);
            button.HoverBackColor = Color.FromArgb(33, 38, 45);
        }
    }

    private static void ConfigureHeaderButton(RoundedButton button, string text, Size size, Color color)
    {
        button.Text = text;
        button.Size = size;
        button.Margin = new Padding(4, 2, 0, 2);
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

    private void SwitchTab(int tabIndex)
    {
        if (isGameFullscreen) return;
        currentTab = tabIndex;
        UpdateTabButtonStyle(tabGameBtn, currentTab == 0);
        UpdateTabButtonStyle(tabPreviewBtn, currentTab == 1);

        if (currentTab == 0)
        {
            previewPanel.Visible = false;
            loadingOverlay.Visible = true;
            CenterLoadingCard();
        }
        else
        {
            loadingOverlay.Visible = false;
            previewPanel.Visible = true;
            previewControl.ResetView();
            if (currentBlueprint is null && !string.IsNullOrWhiteSpace(mapInputBox.Text))
            {
                LoadMapFromInput();
            }
        }
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

    private void BuildPreviewStudio()
    {
        previewPanel.Dock = DockStyle.Fill;
        previewPanel.BackColor = Color.FromArgb(13, 17, 23);
        previewPanel.Visible = false;

        // 1. Top Bar
        previewTopBar.Dock = DockStyle.Top;
        previewTopBar.Height = 52;
        previewTopBar.BackColor = Color.FromArgb(22, 27, 34);
        previewTopBar.Padding = new Padding(12, 8, 12, 8);
        previewTopBar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawLine(pen, 0, previewTopBar.Height - 1, previewTopBar.Width, previewTopBar.Height - 1);
        };

        mapInputBox.Dock = DockStyle.Fill;
        mapInputBox.BackColor = Color.FromArgb(13, 17, 23);
        mapInputBox.ForeColor = Color.FromArgb(240, 246, 252);
        mapInputBox.BorderStyle = BorderStyle.FixedSingle;
        mapInputBox.Font = new Font("Consolas", 10F);
        mapInputBox.PlaceholderText = "Вставьте код карты Base64 (AAAB...) или JSON...";
        mapInputBox.TextChanged += (_, _) => LoadMapFromInput();

        ConfigureHeaderButton(pasteBtn, "📋 Вставить", new Size(100, 34), Color.FromArgb(33, 38, 45));
        pasteBtn.Click += (_, _) =>
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    mapInputBox.Text = Clipboard.GetText().Trim();
                }
            }
            catch { }
        };

        ConfigureHeaderButton(openFileBtn, "📂 Открыть .map", new Size(125, 34), Color.FromArgb(33, 38, 45));
        openFileBtn.Click += OpenMapFileDialog;

        ConfigureHeaderButton(optimizeBtn, "⚡ Оптимизировать", new Size(150, 34), Color.FromArgb(35, 134, 54));
        optimizeBtn.BorderColor = Color.FromArgb(46, 160, 67);
        optimizeBtn.ForeColor = Color.White;
        optimizeBtn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        optimizeBtn.Click += OptimizeCurrentBlueprint;

        ConfigureHeaderButton(exportMapBtn, "💾 Сохранить .map", new Size(140, 34), Color.FromArgb(33, 38, 45));
        exportMapBtn.Click += SaveMapFileDialog;

        ConfigureHeaderButton(centerViewBtn, "🎯 По центру", new Size(105, 34), Color.FromArgb(33, 38, 45));
        centerViewBtn.Click += (_, _) => previewControl.ResetView();

        var topBarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // TextBox
        topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Paste
        topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Open
        topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Optimize
        topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Export
        topBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Center
        topBarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        topBarLayout.Controls.Add(mapInputBox, 0, 0);
        topBarLayout.Controls.Add(pasteBtn, 1, 0);
        topBarLayout.Controls.Add(openFileBtn, 2, 0);
        topBarLayout.Controls.Add(optimizeBtn, 3, 0);
        topBarLayout.Controls.Add(exportMapBtn, 4, 0);
        topBarLayout.Controls.Add(centerViewBtn, 5, 0);
        previewTopBar.Controls.Add(topBarLayout);

        // 2. Sidebar (Width: 330)
        previewSidebar.Dock = DockStyle.Right;
        previewSidebar.Width = 330;
        previewSidebar.BackColor = Color.FromArgb(18, 22, 29);
        previewSidebar.Padding = new Padding(12);
        previewSidebar.AutoScroll = true;
        previewSidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(48, 54, 61), 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, previewSidebar.Height);
        };

        BuildPreviewSidebarCards();

        // 3. Preview Control (Canvas)
        previewControl.Dock = DockStyle.Fill;

        previewPanel.Controls.Add(previewControl);
        previewPanel.Controls.Add(previewSidebar);
        previewPanel.Controls.Add(previewTopBar);
    }

    private void BuildPreviewSidebarCards()
    {
        // 1. Stats Card
        statsCard.Size = new Size(300, 130);
        statsCard.Location = new Point(12, 12);
        statsCard.BackColor = Color.FromArgb(22, 27, 34);
        statsCard.BorderColor = Color.FromArgb(48, 54, 61);
        statsCard.BorderThickness = 1;
        statsCard.CornerRadius = 10;
        statsCard.Padding = new Padding(12);

        statsTitle.AutoSize = false;
        statsTitle.Size = new Size(276, 22);
        statsTitle.Location = new Point(12, 10);
        statsTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        statsTitle.ForeColor = Color.FromArgb(240, 246, 252);
        statsTitle.Text = "📊  Информация о схеме";

        statsInfo.AutoSize = false;
        statsInfo.Size = new Size(276, 85);
        statsInfo.Location = new Point(12, 34);
        statsInfo.Font = new Font("Segoe UI", 8.8F);
        statsInfo.ForeColor = Color.FromArgb(139, 148, 158);
        statsInfo.Text = "Вставьте код карты или откройте файл .map, чтобы увидеть визуализацию и размеры.";

        statsCard.Controls.Add(statsTitle);
        statsCard.Controls.Add(statsInfo);

        // 2. Optimization Card
        optCard.Size = new Size(300, 165);
        optCard.Location = new Point(12, 152);
        optCard.BackColor = Color.FromArgb(22, 27, 34);
        optCard.BorderColor = Color.FromArgb(48, 54, 61);
        optCard.BorderThickness = 1;
        optCard.CornerRadius = 10;
        optCard.Padding = new Padding(12);

        optTitle.AutoSize = false;
        optTitle.Size = new Size(276, 22);
        optTitle.Location = new Point(12, 10);
        optTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        optTitle.ForeColor = Color.FromArgb(63, 185, 80);
        optTitle.Text = "⚡  Оптимизация схемы";

        optInfo.AutoSize = false;
        optInfo.Size = new Size(276, 80);
        optInfo.Location = new Point(12, 34);
        optInfo.Font = new Font("Segoe UI", 8.8F);
        optInfo.ForeColor = Color.FromArgb(139, 148, 158);
        optInfo.Text = "Нажмите «⚡ Оптимизировать», чтобы алгоритм уплотнил расстояния между блоками и сместил схему в начало координат.";

        copyOptBtn.Text = "📋 Скопировать оптимизированный код";
        copyOptBtn.Size = new Size(276, 32);
        copyOptBtn.Location = new Point(12, 120);
        copyOptBtn.FlatStyle = FlatStyle.Flat;
        copyOptBtn.FlatAppearance.BorderSize = 0;
        copyOptBtn.BackColor = Color.FromArgb(31, 111, 235);
        copyOptBtn.HoverBackColor = Color.FromArgb(56, 139, 253);
        copyOptBtn.ForeColor = Color.White;
        copyOptBtn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        copyOptBtn.CornerRadius = 6;
        copyOptBtn.Cursor = Cursors.Hand;
        copyOptBtn.Visible = false;
        copyOptBtn.Click += (_, _) =>
        {
            if (lastOptResult is not null)
            {
                try
                {
                    Clipboard.SetText(lastOptResult.OptimizedBase64);
                    copyOptBtn.Text = "Скопировано в буфер! ✅";
                    Task.Delay(1800).ContinueWith(_ =>
                    {
                        if (!IsDisposed && copyOptBtn.IsHandleCreated)
                        {
                            BeginInvoke(new Action(() => copyOptBtn.Text = "📋 Скопировать код"));
                        }
                    });
                }
                catch { }
            }
        };

        optCard.Controls.Add(optTitle);
        optCard.Controls.Add(optInfo);
        optCard.Controls.Add(copyOptBtn);

        // 3. Explorer Guide Card
        guideCard.Size = new Size(300, 180);
        guideCard.Location = new Point(12, 327);
        guideCard.BackColor = Color.FromArgb(22, 27, 34);
        guideCard.BorderColor = Color.FromArgb(48, 54, 61);
        guideCard.BorderThickness = 1;
        guideCard.CornerRadius = 10;
        guideCard.Padding = new Padding(12);

        guideTitle.AutoSize = false;
        guideTitle.Size = new Size(276, 22);
        guideTitle.Location = new Point(12, 10);
        guideTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        guideTitle.ForeColor = Color.FromArgb(88, 166, 255);
        guideTitle.Text = "📁  Куда поместить карту";

        guideText.AutoSize = false;
        guideText.Size = new Size(276, 95);
        guideText.Location = new Point(12, 34);
        guideText.Font = new Font("Segoe UI", 8.5F);
        guideText.ForeColor = Color.FromArgb(139, 148, 158);
        guideText.Text = "1. Скопируйте код или сохраните .map файл.\r\n2. В лобби игры нажмите «Карты» ➔ «Импорт» и вставьте код (или выберите файл).\r\n3. Все карты сохраняются в профиле лаунчера:";

        guideFolderBtn.Text = "📁 Открыть папку в Проводнике";
        guideFolderBtn.Size = new Size(276, 32);
        guideFolderBtn.Location = new Point(12, 135);
        guideFolderBtn.FlatStyle = FlatStyle.Flat;
        guideFolderBtn.FlatAppearance.BorderSize = 0;
        guideFolderBtn.BackColor = Color.FromArgb(33, 38, 45);
        guideFolderBtn.HoverBackColor = Color.FromArgb(48, 54, 61);
        guideFolderBtn.ForeColor = Color.FromArgb(201, 209, 217);
        guideFolderBtn.BorderColor = Color.FromArgb(48, 54, 61);
        guideFolderBtn.BorderThickness = 1;
        guideFolderBtn.Font = new Font("Segoe UI", 8.5F);
        guideFolderBtn.CornerRadius = 6;
        guideFolderBtn.Cursor = Cursors.Hand;
        guideFolderBtn.Click += (_, _) => OpenMapsFolderInExplorer();

        guideCard.Controls.Add(guideTitle);
        guideCard.Controls.Add(guideText);
        guideCard.Controls.Add(guideFolderBtn);

        previewSidebar.Controls.Add(statsCard);
        previewSidebar.Controls.Add(optCard);
        previewSidebar.Controls.Add(guideCard);
    }

    private void LoadMapFromInput()
    {
        var text = mapInputBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            currentBlueprint = null;
            previewControl.Blueprint = null;
            statsInfo.Text = "Вставьте код карты или откройте файл .map, чтобы увидеть визуализацию и размеры.";
            optInfo.Text = "Нажмите «⚡ Оптимизировать», чтобы алгоритм уплотнил расстояния между блоками.";
            copyOptBtn.Visible = false;
            return;
        }

        try
        {
            currentBlueprint = MapCodec.Decode(text);
            previewControl.Blueprint = currentBlueprint;
            lastOptResult = null;
            copyOptBtn.Visible = false;

            var bbox = currentBlueprint.BoundingBox;
            statsInfo.Text =
                $"• Размер схемы:  {bbox.Width} × {bbox.Height} клеток\r\n" +
                $"• Всего элементов:  {currentBlueprint.CellCount} блоков\r\n" +
                $"• Занято чанков (16×16):  {currentBlueprint.ChunkCount}\r\n" +
                $"• Координаты: X:[{bbox.Left}..{bbox.Right - 1}], Y:[{bbox.Top}..{bbox.Bottom - 1}]";
            statsTitle.ForeColor = Color.FromArgb(240, 246, 252);
        }
        catch (Exception ex)
        {
            statsTitle.ForeColor = Color.FromArgb(248, 81, 73);
            statsInfo.Text = $"Ошибка чтения схемы: {ex.Message}";
        }
    }

    private void OptimizeCurrentBlueprint(object? sender, EventArgs e)
    {
        if (currentBlueprint is null || currentBlueprint.Cells.Count == 0)
        {
            MessageBox.Show("Сначала загрузите или вставьте код схемы для оптимизации.", "Схема не загружена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        lastOptResult = MapOptimizer.Optimize(currentBlueprint);
        previewControl.Blueprint = lastOptResult.OptimizedBlueprint;
        mapInputBox.Text = lastOptResult.OptimizedBase64;

        var st = lastOptResult.Stats;
        optInfo.Text =
            $"• Исходный размер: {st.OriginalWidth}×{st.OriginalHeight} ({st.OriginalWidth * st.OriginalHeight} кл.)\r\n" +
            $"• Оптимизированный: {st.OptimizedWidth}×{st.OptimizedHeight} ({st.OptimizedWidth * st.OptimizedHeight} кл.)\r\n" +
            $"• Сокращение площади: -{st.AreaReductionPercent}%\r\n" +
            $"• Блоков: {st.OptimizedCells} (смещение в 0,0)";

        copyOptBtn.Visible = true;
        copyOptBtn.Focus();
    }

    private void OpenMapFileDialog(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Открыть карту Logic Arrows (.map)",
            Filter = "Карты Logic Arrows (*.map;*.json;*.txt)|*.map;*.json;*.txt|Все файлы (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var content = File.ReadAllText(dialog.FileName);
                mapInputBox.Text = content.Trim();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveMapFileDialog(object? sender, EventArgs e)
    {
        if (currentBlueprint is null || currentBlueprint.Cells.Count == 0)
        {
            MessageBox.Show("Нет загруженной схемы для сохранения.", "Схема пуста", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить карту Logic Arrows (.map)",
            Filter = "Logic Arrows Map (*.map)|*.map",
            FileName = "optimized_mechanism.map",
            DefaultExt = "map"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var data = MapCodec.Encode(currentBlueprint);
                var envelope = new MapFileEnvelope
                {
                    MapName = Path.GetFileNameWithoutExtension(dialog.FileName),
                    Data = data
                };
                MapFileService.Write(dialog.FileName, envelope);
                MessageBox.Show(
                    $"Карта успешно сохранена в:\r\n{dialog.FileName}\r\n\r\nТеперь вы можете импортировать её в лобби игры!",
                    "Карта сохранена",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
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
                MessageBox.Show(
                    $"Найдено обновление {update.TagName}!\r\n\r\n{update.ReleaseName}",
                    "Обновление лаунчера",
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

        loadingStatus.Text = $"Скачивание {availableUpdate.TagName}...";
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
        previewPanel.Visible = false;
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

        if (currentTab == 0)
        {
            loadingOverlay.Visible = true;
            previewPanel.Visible = false;
        }
        else
        {
            loadingOverlay.Visible = false;
            previewPanel.Visible = true;
        }

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
