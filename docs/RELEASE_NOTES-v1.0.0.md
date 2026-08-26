# Logic Arrows Launcher v1.0.0

Первый публичный пакет автономного Windows x64 лаунчера для Logic Arrows.

## В релизе

- Self-contained `LogicArrowsLauncher.exe`. Отдельный .NET Runtime не нужен.
- Первый запуск скачивает официальный набор из 141 ресурса Logic Arrows.
- Следующие запуски проверяют пять ключевых файлов и используют сохранённый snapshot, если версия не изменилась.
- Игра запускается только после нажатия «Играть».
- F11 переключает стандартную Windows-рамку окна, а Esc выходит из fullscreen.
- Официальная иконка Logic Arrows встроена в EXE и доступна в PNG/ICO среди release assets.
- В runtimeconfig явно включены обычные Windows-культуры, чтобы избежать `CultureNotFoundException` во время синхронизации.

## Assets

| Файл | Назначение |
|---|---|
| `LogicArrowsLauncher.exe` | Готовый автономный лаунчер для Windows x64 |
| `logic-arrows.ico` | Иконка приложения в Windows-формате |
| `logic-arrows-favicon.png` | Оригинальная официальная favicon 512×512 |

## Требования

Нужен Windows 10/11 и установленный Microsoft Edge WebView2 Runtime. WebView2 Runtime обычно уже установлен вместе с Edge.

## Атрибуция

Официальная favicon получена с `https://logic-arrows.io/res/favicon512.png`. Logic Arrows и игровые материалы принадлежат соответствующим правообладателям.
