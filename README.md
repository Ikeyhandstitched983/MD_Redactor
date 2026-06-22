# MD Redactor

MD Redactor — нативное Windows 10 desktop-приложение для редактирования Markdown-файлов. На первом этапе приложение открывает `.md`, показывает текст во встроенном web-редакторе и сохраняет изменения обратно в тот же файл.

## Стек

- .NET 10 и WPF.
- WebView2 для встроенного web-интерфейса.
- CommunityToolkit.Mvvm для базовой MVVM-инфраструктуры.
- TypeScript + Vite для `web/editor`.
- xUnit для тестов `MDRedactor.Core`.

## Сборка

```powershell
.\scripts\build.ps1
```

Скрипт проверяет git, .NET 10 SDK, Node.js, npm и WebView2 Runtime, собирает web-редактор, затем выполняет restore/build/test для .NET solution.

## Запуск

```powershell
dotnet run --project .\src\MDRedactor.App\MDRedactor.App.csproj
```

Перед запуском нужно собрать web-часть через `.\scripts\build.ps1` или вручную выполнить `npm run build` в `web\editor`.

## Хранение правок

Правки будут храниться только внутри Markdown-файлов. Проект не использует базу данных, sidecar-json или отдельное хранилище изменений рядом с документом.

