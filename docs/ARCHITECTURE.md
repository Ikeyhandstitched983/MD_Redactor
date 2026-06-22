# Архитектура

## WPF-приложение

`src/MDRedactor.App` — нативный Windows shell на WPF и .NET 10. Главное окно содержит верхнюю панель с командами `Открыть` и `Сохранить`, индикатор состояния и WebView2, который загружает собранный `web/editor/dist/index.html`.

Если web-редактор не собран, приложение показывает русское диагностическое сообщение вместо аварийного завершения.

## Core library

`src/MDRedactor.Core` содержит базовые модели и сервисы для работы с Markdown-файлами. Сейчас реализован `MarkdownFileService`, который читает UTF-8 и имеет fallback для Windows-1251, а также сохраняет текст обратно в текущий файл.

## WebView2 editor

WebView2 используется как граница между нативным host-приложением и web-интерфейсом редактора. Обмен идет через `postMessage`.

Сообщения из web в host:

- `editor.ready`
- `editor.dirtyChanged`
- `editor.saveRequested`
- `editor.error`

Сообщения из host в web:

- `host.loadDocument`
- `host.requestMarkdown`
- `host.setTheme`

## web/editor

`web/editor` — Vite + TypeScript проект. На первом этапе это временный Markdown-редактор на `textarea` и правая панель `Правки`. ProseMirror будет подключен позже без изменения общей границы WPF ↔ web.

## Хранение правок

Все будущие правки должны храниться внутри Markdown-файла. База данных, sidecar-json и другие внешние хранилища правок запрещены, чтобы документ оставался единственным источником данных.

