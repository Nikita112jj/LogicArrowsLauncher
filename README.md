# Logic Arrows Launcher

<p align="center">
  <img src="assets/logic-arrows-favicon.png" width="144" alt="Официальная иконка Logic Arrows">
</p>

<p align="center">
  Автономный Windows x64 лаунчер для <a href="https://logic-arrows.io/">Logic Arrows</a>.
  Он загружает официальный код игры, хранит snapshot локально и запускает игру в WebView2.
</p>

<p align="center">
  <a href="https://github.com/Nikita112jj/LogicArrowsLauncher/releases/latest"><img src="https://img.shields.io/badge/Скачать-актуальный%20релиз-2f8f4e?style=for-the-badge" alt="Скачать последний релиз"></a>
  <img src="https://img.shields.io/badge/Windows-x64-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Windows x64">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8">
</p>

> Один EXE. Без отдельной установки .NET. Первый запуск скачивает ресурсы игры, следующие запуски быстро проверяют версию и используют сохранённую копию.

## Что умеет лаунчер

- Загружает **141 официальный ресурс** Logic Arrows только по HTTPS с `logic-arrows.io`.
- На первом запуске создаёт локальный snapshot в `%LOCALAPPDATA%\\LogicArrowsLauncher\\updates\\1_4`.
- На следующем запуске сначала проверяет только **5 ключевых файлов**, а не весь каталог.
- Скачивает только изменившиеся ресурсы и не перезаписывает файлы, которыми уже пользуется WebView2.
- Оставляет игру на исходном origin `https://logic-arrows.io`, поэтому официальные API, IndexedDB и локальные карты работают в контексте игры.
- Не читает cookies обычного Edge и не копирует токены.
- Показывает кнопку **«Играть»** после подготовки и не запускает игру автоматически.
- Поддерживает fullscreen через WebView2: **F11** показывает или скрывает стандартную Windows-рамку окна, **Esc** выходит из fullscreen.
- Использует официальную favicon Logic Arrows в EXE, заголовке окна и на панели задач.

## Скачать

Открой [последний GitHub Release](https://github.com/Nikita112jj/LogicArrowsLauncher/releases/latest) и скачай `LogicArrowsLauncher.exe`.

EXE собран как **self-contained single-file win-x64**. Отдельный .NET Desktop Runtime не нужен. Для отображения игры требуется установленный **Microsoft Edge WebView2 Runtime**. На Windows 10/11 он обычно уже есть вместе с Edge.

### Первый запуск

Первый запуск может занять время: лаунчер скачивает полный набор из 141 ресурса и сохраняет его в профиле пользователя. Это ожидаемое поведение.

После этого при неизменившейся версии появится сообщение **«Версия не изменилась»**, а лаунчер проверит только пять sentinel-файлов и сразу использует локальный snapshot.

## Управление

| Действие | Результат |
|---|---|
| **Играть** | Открывает подготовленную Logic Arrows в fullscreen |
| **F11** | Показывает или скрывает стандартную верхнюю Windows-панель с кнопками свернуть, развернуть и закрыть, не уменьшая игру |
| **Esc** | Выходит из fullscreen и возвращает обычное окно лаунчера |

## Исходный код

Основной код находится в [`src/`](src/): синхронизация ресурсов, immutable snapshot-хранилище, WebView2-интерцептор и WinForms UI.

| Путь | Назначение |
|---|---|
| [`src/LauncherForm.cs`](src/LauncherForm.cs) | Окно лаунчера, подготовка игры и fullscreen |
| [`src/AssetSynchronizer.cs`](src/AssetSynchronizer.cs) | Быстрый version-check и conditional GET |
| [`src/UpdateStore.cs`](src/UpdateStore.cs) | Версионируемое локальное хранилище snapshot |
| [`src/LocalResourceInterceptor.cs`](src/LocalResourceInterceptor.cs) | Подача сохранённых статических ресурсов в WebView2 |
| [`src/ResourceCatalog.cs`](src/ResourceCatalog.cs) | Allowlist официальных ресурсов Logic Arrows |
| [`smoke/Program.cs`](smoke/Program.cs) | Автоматическая проверка первого и повторного запуска |
| [`assets/logic-arrows.ico`](assets/logic-arrows.ico) | Многоразмерная Windows-иконка приложения |

## Собрать самостоятельно

Нужен .NET 8 SDK. В корне проекта выполни:

```bash
dotnet restore src/LogicArrowsLauncher.csproj
dotnet build src/LogicArrowsLauncher.csproj -c Release --no-restore
dotnet publish src/LogicArrowsLauncher.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=None \
  -p:StripSymbols=true \
  -o artifacts/publish-win-x64
```

Проверить update-path можно так:

```bash
dotnet run --project smoke/LogicArrowsLauncher.Smoke.csproj -c Release
```

Ожидаемый результат smoke-теста: первый запуск скачивает 141 ресурс, повторный проверяет 5 sentinel-файлов, скачивает 0 файлов и сохраняет одну активную версию.

## Иконка и права

В проекте используется официальная favicon Logic Arrows: [`favicon512.png`](https://logic-arrows.io/res/favicon512.png). В README она показана выше, а в Releases доступны PNG и ICO-версии.

Logic Arrows, её название, код игры и графические материалы принадлежат соответствующим правообладателям. Этот репозиторий содержит лаунчер и не заявляет права на игру. Лаунчер намеренно не встраивает изменяемый код Logic Arrows навсегда и получает ресурсы с официального сайта.

## Проверки

Последний smoke-тест подтвердил:

- `141` ресурс скачивается на чистом запуске;
- `5` sentinel-файлов проверяются на повторном запуске;
- `0` файлов скачивается при неизменившейся версии;
- сохраняется `1` активная версия snapshot;
- EXE публикуется как один self-contained файл.

Подробности находятся в [`docs/QA.md`](docs/QA.md).

## Официальные ссылки

- [Logic Arrows](https://logic-arrows.io/)
- [Официальная favicon Logic Arrows](https://logic-arrows.io/res/favicon512.png)
- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- [Параметры globalization в .NET](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization)
