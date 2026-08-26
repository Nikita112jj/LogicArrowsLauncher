# QA

## Последняя проверка

| Проверка | Результат |
|---|---|
| Первый запуск на чистом хранилище | PASS: скачано 141 из 141 ресурсов |
| Повторный запуск | PASS: проверено 5 sentinel-файлов, скачано 0 |
| Пропуск полного каталога | PASS: остальные 136 ресурсов не запрашиваются при неизменившейся версии |
| Сохранение snapshot | PASS: одна активная версия в `updates/versions` |
| Self-contained publish | PASS: один EXE, .NET Runtime не нужен |
| Официальная favicon | PASS: PNG 512×512, ICO 16/24/32/48/64/128/256 |
| Globalization | PASS: runtimeconfig задаёт `Invariant=false` и `PredefinedCulturesOnly=false` |
| Культура 1033 под invariant-окружением | PASS в отдельном .NET 8 culture-smoke |
| F1/F11 внутри WebView2 | Собрано по `AcceleratorKeyPressed`; ручной Windows UI-тест нужно повторить на целевой машине |
| Esc внутри игры | Не перехватывается лаунчером и должен открывать меню Logic Arrows |
| `.map` round-trip | PASS: envelope записывается и читается с сохранением Base64 |
| Неверный `.map` | PASS: чужой format отвергается до WebView2 |
| JS bridge syntax | PASS: встроенный bridge проходит `node --check` |
| Импорт из лобби | Собрано; ручной Windows UI-тест выбора файла и запуска нужен на целевой машине |
| Экспорт из настроек карты | Собрано; ручной Windows WebView2 UI-тест SaveFileDialog нужен на целевой машине |

## Поведение `.map`

Файл `.map` — это JSON-конверт формата `logic-arrows-map` версии 1. В поле `data` хранится Base64, полученный от официального `game.save(map)` внутри страницы Logic Arrows v1.4. Лаунчер не разбирает неизвестную бинарную структуру сам и не вызывает облачные `/api` маршруты.

Импорт начинается в лобби кнопкой **«Импорт .map»**. OpenFileDialog проверяет расширение и содержимое, затем после готовности WebView2 передаёт только валидированные данные в официальный `game.load(map, bytes)`.

Экспорт начинается из штатного окна настроек карты. Bridge добавляет туда кнопку **«Экспорт .map»**, а SaveFileDialog сохраняет конверт локально.

## Поведение обновлений

На чистом запуске лаунчер загружает официальный каталог Logic Arrows. На последующих запусках сначала выполняются conditional GET для `index.html`, `bundle-shell.js`, основного `bundle.js`, `style.css` и `manifest.json`. При пяти ответах 304 сохранённый snapshot сразу подаётся в память. При повторном нажатии «Играть» готовая страница не перезагружается, чтобы не терять игровые бинды; после смены рамки окно повторно передаёт фокус WebView2.

Если sentinel изменился, неполный или повреждён, запускается полный conditional sync. Изменившиеся тела сохраняются в новую immutable-версию; работающие файлы старой версии не перезаписываются.

## Исправление позднего CultureNotFoundException

Ранее публикация включала `InvariantGlobalization=true`. Во время синхронизации код .NET/WebView2 мог запросить культуру 1033 и завершить процесс с сообщением `Only the invariant culture is supported in globalization-invariant mode`.

Сейчас проект задаёт оба runtime-параметра явно:

```xml
<RuntimeHostConfigurationOption Include="System.Globalization.Invariant" Value="false" />
<RuntimeHostConfigurationOption Include="System.Globalization.PredefinedCulturesOnly" Value="false" />
```

Это записывается в `runtimeconfig` внутри публикации. Результат подтверждён тестом, который запускается с `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` и успешно создаёт `CultureInfo.GetCultureInfo(1033)`.

## Ручная проверка на Windows

На Windows 10/11 нужно запустить EXE после полной замены старого файла. На первом запуске ожидается длительная загрузка 141 ресурса. После завершения следует перезапустить лаунчер: он должен показать быструю проверку пяти файлов, а не полный счётчик 141.

После нажатия «Играть» проверь Esc, F1 и F11. Esc должен передаваться Logic Arrows и открывать меню игры. F1 возвращает в экран лаунчера. F11 переключает стандартную Windows-рамку окна, не закрывая игру. Также нужно проверить, что WebView2 Runtime установлен.

Затем проверь импорт: в лобби нажми **«Импорт .map»**, выбери файл, нажми **«Играть»** и убедись, что карта появилась. Для экспорта открой меню карты, нажми **«Экспорт .map»**, выбери путь сохранения и затем импортируй полученный файл обратно.

## Источники

[1] [Параметры конфигурации глобализации .NET](https://learn.microsoft.com/ru-ru/dotnet/core/runtime-config/globalization)

[2] [.NET environment variables](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables)

[3] [WebView2 AcceleratorKeyPressed](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.acceleratorkeypressed?view=webview2-dotnet-1.0.4129.50)

[4] [Официальная favicon Logic Arrows](https://logic-arrows.io/res/favicon512.png)

[5] [WebView2 WebMessageReceived](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.webmessagereceived?view=webview2-dotnet-1.0.4129.50)

[6] [Формат `.map`](MAP-FORMAT.md)
