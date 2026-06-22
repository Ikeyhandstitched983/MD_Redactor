using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using MDRedactor.App.ViewModels;
using MDRedactor.Core.Documents;
using MDRedactor.Core.Services;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace MDRedactor.App;

public partial class MainWindow : Window
{
    private readonly IMarkdownFileService _fileService = new MarkdownFileService();
    private readonly MainWindowViewModel _viewModel;
    private MarkdownDocument? _currentDocument;
    private TaskCompletionSource<string?>? _pendingMarkdownRequest;
    private bool _editorReady;
    private bool _isSaving;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(OpenFileAsync, SaveFileAsync);
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await InitializeEditorAsync();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (EditorWebView.CoreWebView2 is not null)
        {
            EditorWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }
    }

    private async Task InitializeEditorAsync()
    {
        var indexPath = FindEditorIndexPath();
        if (indexPath is null)
        {
            ShowStartupError(
                "Web-редактор не собран. Ожидается файл:\n" +
                $"{GetExpectedEditorIndexPath()}\n\n" +
                "Запустите scripts\\build.ps1 и откройте приложение снова.");
            return;
        }

        try
        {
            await EditorWebView.EnsureCoreWebView2Async();
            EditorWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            EditorWebView.Source = new Uri(indexPath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or WebView2RuntimeNotFoundException)
        {
            ShowStartupError($"Не удалось запустить WebView2. Установите WebView2 Runtime и повторите запуск.\n\n{ex.Message}");
        }
    }

    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Открыть Markdown",
            Filter = "Markdown (*.md)|*.md|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var document = await _fileService.ReadAsync(dialog.FileName);
            _currentDocument = document;
            _viewModel.CurrentFileTitle = document.FileName;
            _viewModel.HasUnsavedChanges = false;
            _viewModel.StatusText = "Сохранено";
            SendDocumentToEditor(document);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            SetError($"Ошибка открытия: {ex.Message}");
        }
    }

    private async Task SaveFileAsync()
    {
        if (_currentDocument is null)
        {
            _viewModel.StatusText = "Нет файла";
            return;
        }

        var markdown = await RequestMarkdownFromEditorAsync();
        if (markdown is null)
        {
            SetError("Ошибка: редактор не вернул текст для сохранения.");
            return;
        }

        await SaveMarkdownAsync(markdown);
    }

    private async Task SaveMarkdownAsync(string markdown)
    {
        if (_currentDocument is null)
        {
            _viewModel.StatusText = "Нет файла";
            return;
        }

        if (_isSaving)
        {
            return;
        }

        try
        {
            _isSaving = true;
            _currentDocument = _currentDocument with { Markdown = markdown };
            await _fileService.WriteAsync(_currentDocument);
            _viewModel.HasUnsavedChanges = false;
            _viewModel.StatusText = "Сохранено";
            SendDocumentToEditor(_currentDocument);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetError($"Ошибка сохранения: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task<string?> RequestMarkdownFromEditorAsync()
    {
        if (!_editorReady || EditorWebView.CoreWebView2 is null)
        {
            return null;
        }

        var request = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingMarkdownRequest = request;

        try
        {
            PostToEditor(new { type = "host.requestMarkdown" });

            var completed = await Task.WhenAny(request.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            return completed == request.Task
                ? await request.Task
                : null;
        }
        finally
        {
            if (ReferenceEquals(_pendingMarkdownRequest, request))
            {
                _pendingMarkdownRequest = null;
            }
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            switch (typeElement.GetString())
            {
                case "editor.ready":
                    _editorReady = true;
                    PostToEditor(new { type = "host.setTheme", theme = "light" });
                    if (_currentDocument is not null)
                    {
                        SendDocumentToEditor(_currentDocument);
                    }

                    break;

                case "editor.dirtyChanged":
                    var hasUnsavedChanges = root.TryGetProperty("isDirty", out var dirtyElement)
                        && dirtyElement.ValueKind == JsonValueKind.True;
                    _viewModel.HasUnsavedChanges = hasUnsavedChanges;
                    _viewModel.StatusText = hasUnsavedChanges
                        ? "Есть несохраненные изменения"
                        : _currentDocument is null ? "Нет файла" : "Сохранено";
                    break;

                case "editor.saveRequested":
                    var markdown = root.TryGetProperty("markdown", out var markdownElement)
                        ? markdownElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (_pendingMarkdownRequest is not null)
                    {
                        _pendingMarkdownRequest.TrySetResult(markdown);
                    }
                    else
                    {
                        _ = SaveMarkdownAsync(markdown);
                    }

                    break;

                case "editor.error":
                    var message = root.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : "неизвестная ошибка";
                    SetError($"Ошибка редактора: {message}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            SetError($"Ошибка протокола редактора: {ex.Message}");
        }
    }

    private void SendDocumentToEditor(MarkdownDocument document)
    {
        if (!_editorReady || EditorWebView.CoreWebView2 is null)
        {
            return;
        }

        PostToEditor(new
        {
            type = "host.loadDocument",
            filePath = document.FilePath,
            fileName = document.FileName,
            markdown = document.Markdown,
            encodingName = document.EncodingName
        });
    }

    private void PostToEditor(object message)
    {
        EditorWebView.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(message));
    }

    private void ShowStartupError(string message)
    {
        StartupErrorText.Text = message;
        StartupErrorPanel.Visibility = Visibility.Visible;
        EditorWebView.Visibility = Visibility.Collapsed;
        SetError("Ошибка");
    }

    private void SetError(string message)
    {
        _viewModel.StatusText = message.StartsWith("Ошибка", StringComparison.Ordinal)
            ? message
            : $"Ошибка: {message}";
    }

    private static string? FindEditorIndexPath()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "web", "editor", "dist", "index.html");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static string GetExpectedEditorIndexPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MDRedactor.sln")))
            {
                return Path.Combine(directory.FullName, "web", "editor", "dist", "index.html");
            }

            directory = directory.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "web", "editor", "dist", "index.html");
    }
}

