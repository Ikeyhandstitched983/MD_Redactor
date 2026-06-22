import type { EditAnnotation, EditDiagnostic } from '../editor/types';
import { buildFragmentPreview, sanitizeCommentForHtmlComment, sortAnnotationsForPanel } from '../editor/reviewModel';

export type ReviewPanelOptions = {
  annotations: EditAnnotation[];
  diagnostics: EditDiagnostic[];
  activeId?: number;
  focusCommentId?: number;
  onSelect(id: number): void;
  onGoTo(id: number): void;
  onDelete(id: number): void;
  onCommentFocus(id: number): void;
  onCommentChange(id: number, comment: string): void;
  onUnsafeComment(): void;
};

export function renderReviewPanel(container: HTMLElement, options: ReviewPanelOptions): void {
  container.innerHTML = '';

  const header = document.createElement('div');
  header.className = 'review-header';

  const title = document.createElement('h2');
  title.textContent = 'Правки';

  const count = document.createElement('span');
  count.className = 'review-count';
  count.textContent = formatEditCount(options.annotations.length);

  header.append(title, count);
  container.append(header);

  if (options.diagnostics.length > 0) {
    const diagnostics = document.createElement('div');
    diagnostics.className = 'diagnostics';

    for (const diagnostic of options.diagnostics) {
      const item = document.createElement('div');
      item.className = diagnostic.severity === 'error' ? 'diagnostic diagnostic-error' : 'diagnostic diagnostic-warning';
      item.textContent = diagnostic.editId === undefined
        ? diagnostic.message
        : `#${diagnostic.editId}: ${diagnostic.message}`;
      diagnostics.append(item);
    }

    container.append(diagnostics);
  }

  if (options.annotations.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'review-empty';
    const emptyTitle = document.createElement('p');
    emptyTitle.textContent = 'В этом файле пока нет правок';
    const emptyHint = document.createElement('span');
    emptyHint.textContent = 'Выделите фрагмент текста и нажмите Enter';
    empty.append(emptyTitle, emptyHint);
    container.append(empty);
    return;
  }

  const list = document.createElement('div');
  list.className = 'review-list';
  let commentToFocus: HTMLTextAreaElement | undefined;

  for (const annotation of sortAnnotationsForPanel(options.annotations)) {
    const card = document.createElement('div');
    card.className = annotation.id === options.activeId ? 'review-card review-card-active' : 'review-card';
    card.tabIndex = 0;
    card.addEventListener('click', (event) => {
      if (event.target instanceof HTMLTextAreaElement) {
        return;
      }

      options.onSelect(annotation.id);
    });
    card.addEventListener('keydown', (event) => {
      if (event.target instanceof HTMLTextAreaElement) {
        return;
      }

      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        options.onSelect(annotation.id);
      }
    });

    const cardHeader = document.createElement('div');
    cardHeader.className = 'review-card-header';

    const id = document.createElement('strong');
    id.textContent = `#${annotation.id}`;

    const kind = document.createElement('span');
    kind.textContent = annotation.kind === 'inline' ? 'в строке' : 'блок';

    cardHeader.append(id, kind);

    const preview = document.createElement('p');
    preview.className = 'review-preview';
    preview.textContent = buildFragmentPreview(annotation);

    const comment = document.createElement('textarea');
    comment.className = 'review-comment';
    comment.value = annotation.comment;
    comment.placeholder = 'Что нужно исправить?';
    comment.setAttribute('aria-label', `Комментарий к правке #${annotation.id}`);

    const emptyComment = document.createElement('div');
    emptyComment.className = 'review-warning';
    emptyComment.textContent = 'Комментарий не заполнен';
    emptyComment.hidden = annotation.comment.trim().length > 0;

    comment.addEventListener('focus', () => options.onCommentFocus(annotation.id));
    comment.addEventListener('input', () => {
      const sanitized = sanitizeCommentForHtmlComment(comment.value);
      if (sanitized.changed) {
        const selectionStart = comment.selectionStart;
        comment.value = sanitized.value;
        comment.setSelectionRange(Math.max(0, selectionStart - 1), Math.max(0, selectionStart - 1));
        options.onUnsafeComment();
      }

      emptyComment.hidden = comment.value.trim().length > 0;
      options.onCommentChange(annotation.id, comment.value);
    });

    const actions = document.createElement('div');
    actions.className = 'review-actions';

    const goToButton = document.createElement('button');
    goToButton.type = 'button';
    goToButton.className = 'review-action-button';
    goToButton.textContent = 'Перейти';
    goToButton.addEventListener('click', (event) => {
      event.stopPropagation();
      options.onGoTo(annotation.id);
    });

    const deleteButton = document.createElement('button');
    deleteButton.type = 'button';
    deleteButton.className = 'review-action-button review-action-button-danger';
    deleteButton.textContent = 'Удалить';
    deleteButton.addEventListener('click', (event) => {
      event.stopPropagation();
      options.onDelete(annotation.id);
    });

    actions.append(goToButton, deleteButton);
    card.append(cardHeader, preview, comment, actions);

    card.append(emptyComment);

    if (annotation.warning) {
      const warning = document.createElement('div');
      warning.className = 'review-warning';
      warning.textContent = annotation.warning;
      card.append(warning);
    }

    list.append(card);

    if (annotation.id === options.focusCommentId) {
      commentToFocus = comment;
    }
  }

  container.append(list);

  if (commentToFocus) {
    window.requestAnimationFrame(() => {
      commentToFocus?.focus();
      commentToFocus?.select();
    });
  }
}

function formatEditCount(count: number): string {
  const mod10 = count % 10;
  const mod100 = count % 100;

  if (mod10 === 1 && mod100 !== 11) {
    return `${count} правка`;
  }

  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) {
    return `${count} правки`;
  }

  return `${count} правок`;
}
