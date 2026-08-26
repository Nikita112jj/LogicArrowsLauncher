# Windows

Готовый Windows x64 файл находится в [GitHub Release v1.0.1](https://github.com/Nikita112jj/LogicArrowsLauncher/releases/tag/v1.0.1): `LogicArrowsLauncher.exe`.

Исходники лаунчера находятся в [`../src/`](../src/), smoke-тест — в [`../smoke/`](../smoke/).

Сборка:

```powershell
dotnet restore ../src/LogicArrowsLauncher.csproj
dotnet publish ../src/LogicArrowsLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ../artifacts/publish-win-x64
```

Для запуска требуется Microsoft Edge WebView2 Runtime. Отдельный .NET Runtime не нужен.
