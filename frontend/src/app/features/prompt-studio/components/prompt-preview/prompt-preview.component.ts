import { Component, inject, input } from '@angular/core';
import { EmptyStateComponent, UiButtonComponent } from '../../../../shared/ui';
import { sanitizeDownloadFilename } from '../../../../core/utils/download-filename.util';
import { ClipboardService } from '../../services/clipboard.service';

let nextPreviewId = 0;

@Component({
  selector: 'app-prompt-preview',
  standalone: true,
  imports: [EmptyStateComponent, UiButtonComponent],
  template: `
    <section class="prompt-preview" [attr.aria-labelledby]="headingId">
      <header class="prompt-preview__header">
        <div>
          <h2 [id]="headingId">Generated Prompt</h2>
          <p>{{ subtitle() }}</p>
        </div>
        <span class="prompt-preview__shortcut">Ctrl / Cmd + Enter</span>
      </header>

      @if (prompt()) {
        <pre class="prompt-preview__output" tabindex="0">{{ prompt() }}</pre>
        <div class="prompt-preview__actions">
          <ui-button variant="secondary" icon="bi-clipboard" ariaLabel="Copy generated prompt" (pressed)="copy()">Copy Prompt</ui-button>
          <ui-button variant="ghost" icon="bi-download" ariaLabel="Download generated prompt as Markdown" (pressed)="download('md')">Download .md</ui-button>
          <ui-button variant="ghost" icon="bi-file-earmark-text" ariaLabel="Download generated prompt as plain text" (pressed)="download('txt')">Download .txt</ui-button>
        </div>
      } @else {
        <app-empty-state
          icon="bi-terminal"
          title="Prompt Output"
          description="Complete the form and generate a prompt to preview it here.">
        </app-empty-state>
      }
    </section>
  `,
  styles: [`
    :host { display: block; min-width: 0; height: 100%; }
    .prompt-preview { display: flex; min-width: 0; min-height: 100%; flex-direction: column; gap: var(--space-4); padding: var(--space-5); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); box-shadow: var(--shadow-sm); }
    .prompt-preview__header { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-3); padding-bottom: var(--space-4); border-bottom: 1px solid var(--divider); }
    .prompt-preview__header h2 { margin: 0 0 4px; font-size: var(--text-lg); line-height: var(--leading-tight); }
    .prompt-preview__header p { margin: 0; color: var(--text-muted); font-size: var(--text-xs); }
    .prompt-preview__shortcut { flex-shrink: 0; padding: 5px 10px; border: 1px solid var(--border-subtle); border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: var(--text-xs); font-weight: var(--weight-semibold); }
    .prompt-preview__output { flex: 1; min-height: 360px; margin: 0; padding: var(--space-4); overflow: auto; border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-muted); color: var(--text-primary); font-family: var(--font-mono); font-size: var(--text-xs); line-height: 1.65; white-space: pre-wrap; overflow-wrap: anywhere; }
    .prompt-preview__output:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .prompt-preview__actions { display: flex; flex-wrap: wrap; gap: var(--space-3); }
    @media (max-width: 680px) { .prompt-preview { padding: var(--space-4); } .prompt-preview__header { flex-direction: column; } .prompt-preview__output { min-height: 300px; } }
  `]
})
export class PromptPreviewComponent {
  readonly prompt = input('');
  readonly subtitle = input('Copy and paste this prompt into your approved AI workflow.');
  readonly filename = input('prompt');
  readonly headingId = `prompt-preview-title-${nextPreviewId++}`;

  private readonly clipboard = inject(ClipboardService);

  copy(): void {
    void this.clipboard.copy(this.prompt());
  }

  download(format: 'md' | 'txt' = 'md'): void {
    const mimeType = format === 'md' ? 'text/markdown' : 'text/plain';
    const blob = new Blob([this.prompt()], { type: `${mimeType};charset=utf-8` });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${sanitizeDownloadFilename(this.filename(), 'prompt')}.${format}`;
    link.click();
    URL.revokeObjectURL(url);
  }
}
