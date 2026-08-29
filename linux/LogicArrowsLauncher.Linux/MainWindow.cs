using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LogicArrowsLauncher.Linux.Cef;
using LogicArrowsLauncher.Linux.Platform;

namespace LogicArrowsLauncher.Linux;

/// <summary>
/// Linux-порт LauncherForm: хедер с кнопками, центральная карточка загрузки (те же координаты),
/// футер со ссылкой на обновления, OSR-вью игры в полноэкранном режиме.
/// </summary>
public sealed class MainWindow : Window
{
    private const string RepositoryUrl = "https://github.com/Nikita112jj/LogicArrowsLauncher";
    private const string ReleaseUrl = RepositoryUrl + "/releases/tag/v1.4.5";

    private readonly CefEngine engine;

    // Header
    private readonly TextBlock headerTitle = new();
    private readonly TextBlock headerSubtitle = new();
    private readonly Button updateButton = new();
    private readonly Button githubButton = new();
    private readonly Button changelogButton = new();

    // Center card
    private readonly Border centerCard = new();
    private readonly TextBlock cardTitle = new();
    private readonly TextBlock cardSubtitle = new();
    private readonly TextBlock loadingStatus = new();
    private readonly TextBlock loadingFile = new();
    private readonly ProgressBar loadingProgress = new();
    private readonly TextBlock loadingCount = new();
    private readonly Button playButton = new();

    // Update banner
    private readonly Border updateNoticePanel = new();
    private readonly TextBlock updateNoticeLabel = new();
    private readonly Button updateNoticeBtn = new();

    // Misc
    private readonly TextBlock errorLabel = new();
    private readonly Button checkUpdatesLink = new();
    private readonly OsrGameView gameView;
    private readonly Panel headerPanel;
    private readonly Panel footerPanel;

    private AssetSynchronizer? synchronizer;
    private ExtensionManager? extensions;
    private bool isBusy;
    private bool isGameFullscreen;
    private bool exportInProgress;
    private bool lobbyImportInProgress;
    private UpdateInfo? pendingUpdate;

    public MainWindow(CefEngine engine)
    {
        this.engine = engine;

        Title = "Logic Arrows Launcher";
        Width = 800;
        Height = 600;
        MinWidth = 720;
        MinHeight = 540;
        Background = new SolidColorBrush(LaTheme.WindowBack);
        FontFamily = new FontFamily("Segoe UI, Inter, Ubuntu, Cantarell, DejaVu Sans");

        gameView = new OsrGameView(engine) { IsVisible = false };

        BuildHeader(out headerPanel);
        BuildCenterCard();
        BuildFooter(out footerPanel);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(60, GridUnitType.Pixel));
        root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        root.RowDefinitions.Add(new RowDefinition(40, GridUnitType.Pixel));
        var body = BuildBody();
        Grid.SetRow(headerPanel, 0);
        Grid.SetRow(body, 1);
        Grid.SetRow(footerPanel, 2);
        root.Children.Add(headerPanel);
        root.Children.Add(body);
        root.Children.Add(footerPanel);
        Content = root;

        KeyDown += OnWindowKeyDown;

        Opened += async (_, _) => await InitializeLauncherAsync();
    }

    // ——— Построение интерфейса ———

    private void BuildHeader(out Panel header)
    {
        var panel = new Grid
        {
            Background = new SolidColorBrush(LaTheme.PanelBack),
            Margin = new Thickness(0),
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        panel.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        headerTitle.Text = "Logic Arrows";
        headerTitle.FontSize = 15;
        headerTitle.FontWeight = FontWeight.Bold;
        headerTitle.Foreground = new SolidColorBrush(LaTheme.TextPrimary);
        headerTitle.Margin = new Thickness(20, 4, 0, 0);

        headerSubtitle.Text = "Лаунчер";
        headerSubtitle.FontSize = 11.5;
        headerSubtitle.Foreground = new SolidColorBrush(LaTheme.TextSecondary);
        headerSubtitle.Margin = new Thickness(20, 0, 0, 2);

        var titleBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleBox.Children.Add(headerTitle);
        titleBox.Children.Add(headerSubtitle);
        Grid.SetColumn(titleBox, 0);
        panel.Children.Add(titleBox);

        updateButton.Classes.Add("accent");
        updateButton.Content = "⚡ Обновить";
        updateButton.IsVisible = false;
        updateButton.Click += (_, _) => OpenExternal(ReleaseUrl);
        Grid.SetColumn(updateButton, 2);

        githubButton.Classes.Add("hdr");
        githubButton.Content = "GitHub";
        githubButton.Click += (_, _) => OpenExternal(RepositoryUrl);
        Grid.SetColumn(githubButton, 3);

        changelogButton.Classes.Add("hdr");
        changelogButton.Content = "Релизы";
        changelogButton.Click += (_, _) => OpenExternal(ReleaseUrl);
        Grid.SetColumn(changelogButton, 4);

        panel.Children.Add(updateButton);
        panel.Children.Add(githubButton);
        panel.Children.Add(changelogButton);

        var separator = new Avalonia.Controls.Shapes.Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(LaTheme.Border),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Grid.SetRow(separator, 0);
        panel.Children.Add(separator);

        header = panel;
    }

    private void BuildCenterCard()
    {
        centerCard.Width = 560;
        centerCard.Height = 420;
        centerCard.Background = new SolidColorBrush(LaTheme.PanelBack);
        centerCard.BorderBrush = new SolidColorBrush(LaTheme.Border);
        centerCard.BorderThickness = new Thickness(1);
        centerCard.CornerRadius = new CornerRadius(12);

        var canvas = new Canvas();

        PlaceText(canvas, cardTitle, "Logic Arrows", 18, FontWeight.Bold, LaTheme.TextPrimary, left: 20, top: 30, width: 520, height: 36, center: true);
        PlaceText(canvas, cardSubtitle, "Неофициальный лаунчер игры Logic Arrows", 12.5, FontWeight.Normal, LaTheme.TextSecondary, left: 20, top: 68, width: 520, height: 22, center: true);
        PlaceText(canvas, loadingStatus, "Проверка ресурсов...", 13.5, FontWeight.Bold, LaTheme.Accent, left: 20, top: 140, width: 520, height: 24, center: true);
        PlaceText(canvas, loadingFile, "Синхронизация с logic-arrows.io", 11.5, FontWeight.Normal, LaTheme.TextSecondary, left: 40, top: 166, width: 480, height: 20, center: true, ellipsis: true);

        loadingProgress.Minimum = 0;
        loadingProgress.Maximum = 100;
        loadingProgress.Value = 0;
        loadingProgress.Height = 6;
        loadingProgress.Width = 460;
        Canvas.SetLeft(loadingProgress, 50);
        Canvas.SetTop(loadingProgress, 196);
        loadingProgress.Background = new SolidColorBrush(Color.FromRgb(33, 38, 45));
        loadingProgress.Foreground = new SolidColorBrush(LaTheme.Success);
        loadingProgress.CornerRadius = new CornerRadius(3);
        canvas.Children.Add(loadingProgress);

        PlaceText(canvas, loadingCount, "0%", 11.5, FontWeight.Normal, LaTheme.TextSecondary, left: 20, top: 208, width: 520, height: 20, center: true);

        playButton.Classes.Add("play");
        playButton.Content = "ИГРАТЬ";
        playButton.Height = 48;
        playButton.Width = 460;
        Canvas.SetLeft(playButton, 50);
        Canvas.SetTop(playButton, 246);
        playButton.IsVisible = false;
        playButton.IsEnabled = false;
        playButton.Click += (_, _) => EnterGameFullscreen();
        canvas.Children.Add(playButton);

        updateNoticePanel.Width = 460;
        updateNoticePanel.Height = 48;
        updateNoticePanel.Background = new SolidColorBrush(Color.FromRgb(22, 27, 34));
        updateNoticePanel.BorderBrush = new SolidColorBrush(LaTheme.Border);
        updateNoticePanel.BorderThickness = new Thickness(1);
        updateNoticePanel.CornerRadius = new CornerRadius(8);
        updateNoticePanel.IsVisible = false;
        Canvas.SetLeft(updateNoticePanel, 50);
        Canvas.SetTop(updateNoticePanel, 304);

        var bannerGrid = new Grid();
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        bannerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        updateNoticeLabel.Text = "Доступно обновление лаунчера";
        updateNoticeLabel.FontSize = 12;
        updateNoticeLabel.Foreground = new SolidColorBrush(LaTheme.TextBright);
        updateNoticeLabel.VerticalAlignment = VerticalAlignment.Center;
        updateNoticeLabel.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(updateNoticeLabel, 0);
        bannerGrid.Children.Add(updateNoticeLabel);

        updateNoticeBtn.Classes.Add("accent");
        updateNoticeBtn.Content = "Обновить";
        updateNoticeBtn.Height = 32;
        updateNoticeBtn.Margin = new Thickness(0, 0, 8, 0);
        updateNoticeBtn.MinWidth = 118;
        updateNoticeBtn.Click += (_, _) => ApplyUpdate();
        Grid.SetColumn(updateNoticeBtn, 1);
        bannerGrid.Children.Add(updateNoticeBtn);

        updateNoticePanel.Child = bannerGrid;
        canvas.Children.Add(updateNoticePanel);

        PlaceText(canvas, errorLabel, string.Empty, 11.5, FontWeight.Normal, LaTheme.Error, left: 40, top: 356, width: 480, height: 40, center: true, ellipsis: true, wrap: true);

        centerCard.Child = canvas;
    }

    private static void PlaceText(Canvas canvas, TextBlock block, string text, double size, FontWeight weight,
        Color color, double left, double top, double width, double height,
        bool center = false, bool ellipsis = false, bool wrap = false)
    {
        block.Text = text;
        block.FontSize = size;
        block.FontWeight = weight;
        block.Foreground = new SolidColorBrush(color);
        block.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        block.TextTrimming = ellipsis ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        block.TextAlignment = center ? TextAlignment.Center : TextAlignment.Left;
        block.VerticalAlignment = VerticalAlignment.Center;
        block.Width = width;
        block.Height = height;
        Canvas.SetLeft(block, left);
        Canvas.SetTop(block, top);
        canvas.Children.Add(block);
    }

    private void BuildFooter(out Panel footer)
    {
        var panel = new Grid { Background = new SolidColorBrush(LaTheme.PanelBack) };
        panel.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        panel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        checkUpdatesLink.Classes.Add("link");
        checkUpdatesLink.Content = "Проверить обновления";
        checkUpdatesLink.FontSize = 11.5;
        checkUpdatesLink.VerticalAlignment = VerticalAlignment.Center;
        checkUpdatesLink.HorizontalAlignment = HorizontalAlignment.Center;
        checkUpdatesLink.Click += async (_, _) =>
        {
            await CheckForUpdatesAsync(manual: true);
        };
        Grid.SetColumn(checkUpdatesLink, 0);
        panel.Children.Add(checkUpdatesLink);

        var versionText = new TextBlock
        {
            Text = "v1.4.7 (Linux-порт)",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(LaTheme.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
        };
        Grid.SetColumn(versionText, 1);
        panel.Children.Add(versionText);

        var separator = new Avalonia.Controls.Shapes.Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(LaTheme.Border),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Grid.SetRow(separator, 0);
        panel.Children.Add(separator);

        footer = panel;
    }

    private Panel BuildBody()
    {
        var body = new Grid();

        var overlay = new Panel();
        Grid.SetColumnSpan(overlay, 2);
        overlay.Children.Add(centerCard);
        centerCard.HorizontalAlignment = HorizontalAlignment.Center;
        centerCard.VerticalAlignment = VerticalAlignment.Center;

        body.Children.Add(gameView);
        body.Children.Add(overlay);
        return body;
    }

    // ——— Поток запуска (порт InitializeLauncherAsync) ———

    private async Task InitializeLauncherAsync()
    {
        if (isBusy) return;
        isBusy = true;
        try
        {
            var updateStore = new UpdateStore(LinuxPaths.UpdatesDirectory);
            synchronizer = new AssetSynchronizer(updateStore);
            extensions = new ExtensionManager(Path.Combine(LinuxPaths.DataRoot, "extensions.json"));

            var progress = new Progress<SyncProgress>(ReportProgress);
            var summary = await synchronizer.SyncAsync(progress, CancellationToken.None);

            if (!synchronizer.HasRequiredCache())
            {
                ShowLaunchError("Не удалось подготовить локальный кэш игры.\nПроверьте соединение с сетью.");
                return;
            }

            loadingStatus.Text = "Подготовка движка...";
            loadingFile.Text = "Загрузка компонентов браузерного движка";
            loadingProgress.Value = 100;
            loadingCount.Text = "100%";

            engine.BindResourceHandler(synchronizer, OnBridgeMessage);
            engine.ResourceRequestHandler.Extensions = extensions;
            engine.MainFrameLoadEnd += OnGamePageLoadEnd;
            engine.Navigate(ResourceCatalog.Origin + "/");
            await engine.GamePageReady.Task;

            loadingStatus.Text = "Игра готова к запуску";
            loadingStatus.Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80));
            loadingFile.Text = summary.Downloaded > 0
                ? $"Загружено {summary.Downloaded} новых файлов"
                : "Все файлы игры проверены локально";

            playButton.IsVisible = true;
            playButton.IsEnabled = true;
            playButton.Focus();

            _ = CheckForUpdatesAsync(manual: false);
        }
        catch (Exception ex)
        {
            ShowLaunchError(ex.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private void ReportProgress(SyncProgress p)
    {
        var percent = p.Total > 0 ? p.Completed * 100 / p.Total : 0;
        loadingProgress.Value = percent;
        loadingCount.Text = $"{p.Completed} из {p.Total} ({percent}%)";
        loadingFile.Text = p.AssetPath;
        loadingStatus.Text = p.Status;
    }

    private void ShowLaunchError(string message, bool fatal = false)
    {
        loadingStatus.Text = "Ошибка загрузки";
        loadingStatus.Foreground = new SolidColorBrush(LaTheme.Error);
        loadingFile.Text = message;
        errorLabel.Text = fatal ? message : string.Empty;
    }

    private void OnGamePageLoadEnd(object? sender, EventArgs e)
    {
        // Навигация на главную страницу игры завершена — как NavigationCompleted в WebView2.
        engine.GamePageReady.TrySetResult(true);
    }

    // ——— Игровой полноэкранный режим ———

    private void EnterGameFullscreen()
    {
        if (isBusy || synchronizer?.HasRequiredCache() != true) return;
        isGameFullscreen = true;
        headerPanel.IsVisible = false;
        footerPanel.IsVisible = false;
        centerCard.IsVisible = false;
        gameView.IsVisible = true;
        WindowState = WindowState.Maximized;
        gameView.Focus();
        engine.SetFocus(true);
    }

    private void ExitGameFullscreen()
    {
        isGameFullscreen = false;
        gameView.IsVisible = false;
        headerPanel.IsVisible = true;
        footerPanel.IsVisible = true;
        centerCard.IsVisible = true;
        WindowState = WindowState.Normal;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (isGameFullscreen && e.Key == Key.Escape)
        {
            ExitGameFullscreen();
            e.Handled = true;
        }
    }

    // ——— Сообщения моста (порт WebMessageReceivedHandler) ———

    private void OnBridgeMessage(object? sender, JsonDocument doc)
    {
        try
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type)) return;
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
                OpenMapsFolder();
            }
            else if (typeStr == "extensions-add")
            {
                if (!isBusy) _ = HandleExtensionsAddAsync();
            }
            else if (typeStr == "extensions-set-active")
            {
                var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var enabled = root.TryGetProperty("enabled", out var enabledEl) && enabledEl.GetBoolean();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    extensions?.SetEnabled(name, enabled);
                    _ = SendExtensionsStateAsync();
                    if (enabled) engine.Reload();
                }
            }
            else if (typeStr == "extensions-remove")
            {
                var name = root.TryGetProperty("name", out var removeEl) ? removeEl.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var wasActive = extensions?.GetActive()?.Name == name;
                    extensions?.Remove(name);
                    _ = SendExtensionsStateAsync();
                    if (wasActive) engine.Reload();
                }
            }
            else if (typeStr == "extensions-list-request")
            {
                _ = SendExtensionsStateAsync();
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
        catch { }
    }

    private async Task ImportFromLobbyAsync(string text)
    {
        if (lobbyImportInProgress || !engine.IsReady) return;
        lobbyImportInProgress = true;
        try
        {
            var envelope = MapFileService.ReadText(text);
            var payload = JsonSerializer.Serialize(new { data = envelope.Data, name = envelope.MapName });
            var stageResponse = await engine.ExecuteScriptAsync(
                $"globalThis.__logicArrowsLauncherStageLobbyImport?.({payload}) ?? ({{}})");
            EnsureBridgeSuccess(stageResponse, "Не удалось подготовить импорт карты.");

            var openResponse = await engine.ExecuteScriptAsync(
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
        if (exportInProgress || !engine.IsReady) return;
        exportInProgress = true;
        try
        {
            var response = await engine.ExecuteScriptAsync(
                "globalThis.__logicArrowsLauncherExport?.() ?? ({error:'Функция экспорта недоступна.'})");
            if (string.IsNullOrEmpty(response) || response == "null")
            {
                throw new InvalidDataException("Движок не вернул данные карты.");
            }

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var errorProp))
            {
                throw new InvalidDataException(errorProp.GetString() ?? "Ошибка экспорта.");
            }

            var data = root.GetProperty("data").GetString()!;
            var mapId = root.TryGetProperty("mapId", out var idProp) ? idProp.GetString() : null;
            var mapName = root.TryGetProperty("mapName", out var nameProp) ? nameProp.GetString() : null;

            var defaultName = string.IsNullOrWhiteSpace(mapName) ? (mapId ?? "map") : mapName;
            foreach (var c in Path.GetInvalidFileNameChars()) defaultName = defaultName.Replace(c, '_');

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт карты Logic Arrows",
                SuggestedFileName = $"{defaultName}.map",
                DefaultExtension = "map",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Logic Arrows Map") { Patterns = new[] { "*.map" } },
                },
            });
            if (file is null) return;

            // MapFileService пишет в файл — пишем во временный и копируем в поток диалога.
            var tempPath = Path.Combine(Path.GetTempPath(), $"la-export-{Guid.NewGuid():N}.map");
            try
            {
                MapFileService.Write(tempPath, new MapFileEnvelope
                {
                    MapId = mapId,
                    MapName = mapName,
                    Data = data,
                });
                await using var source = File.OpenRead(tempPath);
                await using var target = await file.OpenWriteAsync();
                await source.CopyToAsync(target);
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
        catch (Exception ex)
        {
            headerSubtitle.Text = "Ошибка экспорта: " + ex.Message;
        }
        finally
        {
            exportInProgress = false;
        }
    }

    private void EnsureBridgeSuccess(string? response, string fallbackError)
    {
        if (string.IsNullOrEmpty(response) || response == "null")
        {
            throw new InvalidDataException(fallbackError);
        }
        using var doc = JsonDocument.Parse(response);
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            throw new InvalidDataException(error.GetString() ?? fallbackError);
        }
    }

    private async Task NotifyMapPageAsync(string message, bool isError)
    {
        var payload = JsonSerializer.Serialize(new { type = "map-import-failed", message, isError });
        await engine.ExecuteScriptAsync($"window.dispatchEvent(new MessageEvent('launcher-bridge', {{data: {payload}}}));");
    }

    private void OpenMapsFolder()
    {
        try
        {
            Directory.CreateDirectory(LinuxPaths.MapsDirectory);
            OpenExternal(LinuxPaths.MapsDirectory);
        }
        catch { }
    }

    private async Task HandleExtensionsAddAsync()
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Выберите папку расширения (в ней должны быть .js файлы)",
                AllowMultiple = false,
            });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;
            extensions?.Register(path);
            await SendExtensionsStateAsync();
            engine.Reload();
        }
        catch (Exception exception)
        {
            headerSubtitle.Text = "Не удалось добавить расширение: " + exception.Message;
        }
    }

    private async Task SendExtensionsStateAsync()
    {
        if (!engine.IsReady) return;
        var json = JsonSerializer.Serialize(extensions?.Entries ?? Array.Empty<ExtensionEntry>());
        await engine.ExecuteScriptAsync(
            $"window.dispatchEvent(new CustomEvent('la-extensions-state', {{detail: {json}}}));");
    }

    // ——— Обновления ———

    private async Task CheckForUpdatesAsync(bool manual)
    {
        try
        {
            var update = await LauncherUpdater.CheckForUpdatesAsync();
            if (update is null)
            {
                if (manual) headerSubtitle.Text = "У вас последняя версия";
                return;
            }
            pendingUpdate = update;
            updateNoticeLabel.Text = $"Доступна версия {update.TagName}";
            updateNoticePanel.IsVisible = true;
            updateButton.IsVisible = true;
        }
        catch
        {
            if (manual) headerSubtitle.Text = "Не удалось проверить обновления";
        }
    }

    private void ApplyUpdate()
    {
        // Автообновление self-update на Linux (замена запущенного файла) — TODO;
        // сейчас скачиваем готовый релиз в системном браузере.
        OpenExternal(ReleaseUrl);
    }

    private void OpenExternal(string urlOrPath)
    {
        try
        {
            System.Diagnostics.Process.Start("xdg-open", urlOrPath);
        }
        catch { }
    }
}
