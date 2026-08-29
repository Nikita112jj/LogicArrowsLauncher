# Logic Arrows Launcher — Linux-порт

Avalonia + CEF-порт Windows-лаунчера (WinForms + WebView2). Цель — поведение 1-в-1:
тот же интерфейс, тот же локальный снапшот игры, тот же мост (вкладка «Превью и Схемы»,
импорт/экспорт карт, оптимизатор).

## Готовая сборка

В [релизе v1.4.5](https://github.com/Nikita112jj/LogicArrowsLauncher/releases/tag/v1.4.5) лежит
экспериментальный тарбол **LogicArrowsLauncher-v1.4.5-linux-x64.tar.gz** (422 МБ): внутри
самодостаточный .NET runtime, runtime CEF и `run.sh`. Запуск:

```bash
tar -xzf LogicArrowsLauncher-v1.4.5-linux-x64.tar.gz
cd LogicArrowsLauncher-linux-x64
bash run.sh   # нужны libgtk-3, libnss3, libx11-xcb, libxkbcommon, libgbm, libasound2
```

Сборка ни разу не запускалась на живом Linux — о проблемах сообщайте в Issues.

## Статус (честно)

**Готово (компилируется, публикуется):**
- Полный проект `LogicArrowsLauncher.Linux` (net8.0, Avalonia 11.3, CefNet/CEF 105) собирается
  под linux-x64 — и с Windows-машины, и на Linux.
- UI повторяет LauncherForm: хедер (GitHub/Релизы/Обновить), карточка загрузки с теми же
  координатами, прогресс синхронизации, кнопка ИГРАТЬ, нейтральный баннер обновления,
  футер с «Проверить обновления», подзаголовок «Неофициальный лаунчер игры Logic Arrows».
- Перехват запросов `https://logic-arrows.io/*` через `CefResourceRequestHandler` —
  локальный снапшот, заглушка `/sw.js`, тот же Content-Type/CORS, что в `LocalResourceInterceptor.cs`.
- Кроссплатформенная логика переиспользуется из `src/` без копирования: AssetSynchronizer,
  UpdateStore, ResourceCatalog, CacheModels, MapFileService, MapData, MapOptimizer,
  MapBridgeScript (JS-мост 1-в-1), LauncherUpdater (частично, см. ниже).
- Мост: shim `chrome.webview.postMessage` + инъекция в каждый документ через `OnLoadStart`,
  `ExecuteScriptAsync` с возвратом JSON — через перехват `/__la_bridge` (без MessageRouter).
- Обработка сообщений моста: export-request, import-request, open-maps-folder, bridge-error.
- Данные: XDG-пути (`~/.local/share/LogicArrowsLauncher`: updates/, profile/, maps/).

**Требует проверки/доработки на реальной Linux-машине** (на Windows GUI-часть не запустишь):
- Запуск: CEF 105.3 linux64 раскладывается рядом с `LogicArrowsLauncher` (см. setup-скрипт в
  `../../linux-tools/setup.sh`).
- OSR-рендер: blit BGRA-буфера в Avalonia WriteableBitmap, масштабирование, курсор,
  всплывающие подсказки/контекстные меню (OnPopup* пока пустые), IME.
- Клавиатура: таблица Avalonia Key → Win32 VK (буквы/цифры/F1-F12/навигация) — сверить на живой игре.
- Автообновление: `ApplyUpdateAndRestart` на Linux выбрасывает `PlatformNotSupportedException`
  (обновление через страницу Releases); детект новых версий работает.
- Проект никогда не запускался: ожидаемы мелкие правки запуска (GPU/SwiftShader, Wayland/X11).

## Архитектура

```
LogicArrowsLauncher.Linux/
├── Program.cs                 # бутстрап: субпроцессы CEF → CefApi.ExecuteProcess; затем Avalonia
├── App.cs                     # палитра и стили (LaTheme — те же цвета, что в LauncherForm)
├── MainWindow.cs              # порт LauncherForm: хедер/карточка/футер/полноэкранный режим/мост
├── Cef/
│   ├── CefEngine.cs           # Initialize/ExternalMessagePump/Navigate/ExecuteScriptAsync/ввод
│   ├── LaApp.cs               # CefApp (флаги Chromium)
│   ├── LaRequestHandler.cs    # подключает перехват к origin игры
│   ├── LaResourceRequestHandler.cs   # снапшот + /sw.js + /__la_bridge (порт LocalResourceInterceptor)
│   ├── LaResourceHandler.cs   # ответ из памяти (аналог CreateWebResourceResponse)
│   └── OsrGameView.cs         # Avalonia-контрол: OnPaint → WriteableBitmap, ввод → CefBrowserHost
└── Platform/
    ├── LinuxPaths.cs          # XDG_DATA_HOME вместо %LocalAppData%
    └── SystemDrawingShim.cs   # структуры Point/Size/Rectangle для MapData/MapOptimizer
```

Ключевое решение: WebKitGTK (Photino, официальный Avalonia WebView) не умеет подменять ответы
на запросы (сигнал `resource-load-started` только наблюдательный) — а без перехвата нет локального
снапшота. Поэтому CEF (Chromium, как WebView2) через CefNet: перехват `GetResourceHandler`,
инъекция скриптов, OSR-рендер в Avalonia.

## Сборка

Нужен .NET SDK 8 (подойдёт распакованный из `linux-tools/dotnet-sdk-linux-x64.tar.gz`):

```bash
cd linux/LogicArrowsLauncher.Linux
dotnet publish -c Release            # самодостаточный linux-x64
```

Runtime CEF в папку publish (один раз) и запуск:

```bash
CEFR=$PWD/bin/Release/net8.0/linux-x64/publish
cp -a <cef_binary_105.3.39_linux64>/Release/. "$CEFR"/
cp -a <cef_binary_105.3.39_linux64>/Resources/. "$CEFR"/
"$CEFR/LogicArrowsLauncher"
```

Скрипт, который делает всё это на Linux-машине (SDK и CEF лежат рядом): `../../linux-tools/setup.sh`.

Системные зависимости (Debian/Ubuntu): `libgtk-3-0 libnss3 libx11-xcb1 libxkbcommon0 libgbm1 libasound2`.
