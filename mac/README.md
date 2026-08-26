# macOS

В этом релизе для macOS доступен исходный код и структура проекта. Готового macOS-бинарника нет: текущий лаунчер использует WinForms и Microsoft WebView2, которые рассчитаны на Windows.

Кроссплатформенная часть синхронизации находится в [`../src/AssetSynchronizer.cs`](../src/AssetSynchronizer.cs), [`../src/UpdateStore.cs`](../src/UpdateStore.cs) и [`../src/ResourceCatalog.cs`](../src/ResourceCatalog.cs).

Для macOS-сборки полноценного GUI-порта потребуется заменить WinForms/WebView2 на Avalonia или WKWebView. README не предлагает фиктивный `.app` или `.dmg`, которого в проекте нет.

Исходники: [`../src/`](../src/).
