# Changelog

Все заметные изменения Logic Arrows Launcher собраны в этом файле.

## [1.0.1] — patch

### Исправлено

- **Esc** больше не закрывает fullscreen лаунчера и не возвращает в его лобби. Клавиша передаётся игре, поэтому Logic Arrows может открыть собственное меню.
- **F1** добавлен как отдельная клавиша возврата из игры в лаунчер.
- Повторное нажатие «Играть» больше не делает принудительный `Reload()` уже загруженной страницы. Это уменьшает шанс потерять игровые бинды.
- Перед входом в fullscreen лаунчер ждёт завершения навигации WebView2 и повторно передаёт фокус игровому контролу после смены рамки окна.
- В README добавлены отдельные разделы Windows, Linux и macOS. Для Linux/macOS указано, что полноценного бинарника пока нет.

### Управление

| Клавиша | Действие |
|---|---|
| **Esc** | Меню внутри Logic Arrows |
| **F1** | Вернуться в лаунчер |
| **F11** | Показать или скрыть стандартную Windows-рамку, не уменьшая игру |

## [1.0.0]

- Первый self-contained Windows x64 EXE.
- Быстрый version-check по пяти sentinel-файлам.
- Immutable snapshot-хранилище ресурсов Logic Arrows.
- Официальная favicon Logic Arrows в EXE, README и Releases.
- F11/Esc fullscreen-логика предыдущего выпуска.

[1.0.1]: https://github.com/Nikita112jj/LogicArrowsLauncher/releases/tag/v1.0.1
[1.0.0]: https://github.com/Nikita112jj/LogicArrowsLauncher/releases/tag/v1.0.0
