# Linux

В этом релизе для Linux доступен исходный код и структура проекта. Готового Linux-бинарника нет: текущий лаунчер использует WinForms и Microsoft WebView2, которые рассчитаны на Windows.

Основная кроссплатформенная логика синхронизации находится в [`../src/AssetSynchronizer.cs`](../src/AssetSynchronizer.cs), [`../src/UpdateStore.cs`](../src/UpdateStore.cs) и [`../src/ResourceCatalog.cs`](../src/ResourceCatalog.cs).

Для Linux-сборки полноценного GUI-порта потребуется заменить слой WinForms/WebView2 на GTK, Avalonia или Qt WebEngine. Поэтому README не предлагает несуществующий `LogicArrowsLauncher.AppImage`.

Исходники: [`../src/`](../src/).
