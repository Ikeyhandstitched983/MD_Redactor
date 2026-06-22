import './styles.css';
import { onHostMessage, postEditorMessage } from './bridge';
import type { HostToEditorMessage } from './bridge';

const appElement = document.querySelector<HTMLDivElement>('#app');

if (!appElement) {
  throw new Error('Корневой элемент редактора не найден.');
}

const app: HTMLDivElement = appElement;

app.innerHTML = `
  <main class="editor-shell">
    <section class="editor-pane" aria-label="Markdown-редактор">
      <div class="document-bar">
        <div>
          <div class="document-title" id="document-title">Файл не открыт</div>
          <div class="document-meta" id="document-meta">Кодировка: utf-8</div>
        </div>
      </div>
      <textarea id="markdown-editor" spellcheck="true" placeholder="Откройте Markdown-файл в верхней панели приложения."></textarea>
    </section>
    <aside class="changes-pane" aria-label="Правки">
      <h2>Правки</h2>
      <p>Панель будет подключена на следующих этапах.</p>
    </aside>
  </main>
`;

function requireElement<TElement extends Element>(selector: string): TElement {
  const element = app.querySelector<TElement>(selector);
  if (!element) {
    throw new Error(`Элемент редактора не найден: ${selector}`);
  }

  return element;
}

const editor = requireElement<HTMLTextAreaElement>('#markdown-editor');
const documentTitle = requireElement<HTMLDivElement>('#document-title');
const documentMeta = requireElement<HTMLDivElement>('#document-meta');

let isDirty = false;
let currentFileName = 'Файл не открыт';
let currentEncodingName = 'utf-8';

function updateDocumentInfo(): void {
  documentTitle.textContent = currentFileName;
  documentMeta.textContent = `Кодировка: ${currentEncodingName}`;
}

function setDirty(nextValue: boolean): void {
  if (isDirty === nextValue) {
    return;
  }

  isDirty = nextValue;
  postEditorMessage({ type: 'editor.dirtyChanged', isDirty });
}

function requestSave(): void {
  postEditorMessage({ type: 'editor.saveRequested', markdown: editor.value });
}

function loadDocument(message: Extract<HostToEditorMessage, { type: 'host.loadDocument' }>): void {
  currentFileName = message.fileName || 'Без имени';
  currentEncodingName = message.encodingName || 'utf-8';
  editor.value = message.markdown ?? '';
  updateDocumentInfo();
  setDirty(false);
  editor.focus();
}

editor.addEventListener('input', () => setDirty(true));

window.addEventListener('keydown', (event) => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
    event.preventDefault();
    requestSave();
  }
});

onHostMessage((message) => {
  try {
    switch (message.type) {
      case 'host.loadDocument':
        loadDocument(message);
        break;
      case 'host.requestMarkdown':
        requestSave();
        break;
      case 'host.setTheme':
        document.documentElement.dataset.theme = message.theme || 'light';
        break;
    }
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    postEditorMessage({ type: 'editor.error', message: errorMessage });
  }
});

updateDocumentInfo();
postEditorMessage({ type: 'editor.ready' });
