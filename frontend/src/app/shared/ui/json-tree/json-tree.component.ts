import { Component, Input, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JsonTreeNodeComponent, JsonValue } from './json-tree-node.component';
import { CopyButtonComponent } from '../copy-button/copy-button.component';
import { sanitizeDownloadFilename } from '../../../core/utils/download-filename.util';

/**
 * Renders arbitrary JSON (or a raw JSON string, e.g. OrderRequestDetail's
 * requestJson/responseJson) as a collapsible, type-colored, searchable tree.
 * A string that fails to parse is shown as a danger banner with the raw
 * text instead of throwing -- see remediation_plan.md's note that the DB
 * occasionally holds non-JSON in these columns.
 */
@Component({
  selector: 'app-json-tree',
  standalone: true,
  imports: [CommonModule, FormsModule, JsonTreeNodeComponent, CopyButtonComponent],
  template: `
    <div class="json-tree-box">
      <div class="json-tree-toolbar">
        <span class="json-tree-title">{{ title }}</span>
        <div class="toolbar-actions">
          <input
            *ngIf="parsed() !== undefined"
            type="text"
            class="search-input"
            placeholder="Search keys/values..."
            aria-label="Search JSON keys and values"
            [(ngModel)]="searchTerm">
          <button type="button" class="toolbar-btn" [attr.aria-pressed]="rawMode()" (click)="rawMode.set(!rawMode())" *ngIf="parsed() !== undefined">
            {{ rawMode() ? 'Tree view' : 'Raw view' }}
          </button>
          <app-copy-button *ngIf="rawText() !== null" [value]="rawText()!" label="Copy"></app-copy-button>
          <button type="button" class="toolbar-btn" aria-label="Download JSON data" (click)="download()" *ngIf="rawText() !== null">
            <i class="bi bi-download"></i>
          </button>
        </div>
      </div>

      <div class="danger-banner" *ngIf="parseError() as err">
        <strong>Not valid JSON:</strong> {{ err }}
        <pre class="raw-fallback">{{ rawText() }}</pre>
      </div>

      <ng-container *ngIf="parsed() !== undefined && !parseError()">
        <pre class="raw-view" *ngIf="rawMode()">{{ formattedJson() }}</pre>
        <div class="tree-view" *ngIf="!rawMode()">
          <app-json-tree-node [value]="parsed()!" [searchTerm]="searchTerm" [depth]="0"></app-json-tree-node>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    .json-tree-box {
      background: var(--surface-raised);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-md);
      padding: 12px;
      margin-top: 8px;
    }
    .json-tree-toolbar { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; margin-bottom: 8px; }
    .json-tree-title { font-weight: 600; font-size: 0.85rem; color: var(--text-secondary); }
    .toolbar-actions { display: flex; align-items: center; gap: 8px; }
    .search-input { background: var(--input-bg); border: 1px solid var(--input-border); border-radius: var(--radius-sm); padding: 3px 8px; font-size: 0.78rem; color: var(--text-primary); }
    .toolbar-btn { background: var(--surface-interactive); border: 1px solid var(--border-strong); border-radius: var(--radius-sm); color: var(--text-secondary); font-size: 0.75rem; padding: 3px 8px; cursor: pointer; }
    .toolbar-btn:hover { color: var(--text-primary); background: var(--surface-hover); }
    .tree-view, .raw-view { max-height: 420px; overflow: auto; }
    .raw-view { margin: 0; font-family: 'JetBrains Mono', monospace; font-size: 0.8rem; color: var(--text-primary); }
    .danger-banner { background: var(--state-danger-bg); color: var(--state-danger-fg); padding: 10px 14px; border-radius: var(--radius-sm); font-size: 0.85rem; }
    .raw-fallback { margin: 8px 0 0; white-space: pre-wrap; word-break: break-word; font-size: 0.78rem; opacity: 0.85; }
  `]
})
export class JsonTreeComponent {
  @Input() title: string = 'JSON';
  @Input() set data(value: unknown) { this._data.set(value); }

  private _data = signal<unknown>(null);
  searchTerm = '';
  rawMode = signal(false);

  /** undefined = no data at all; null/JsonValue = successfully resolved. */
  parsed = computed<JsonValue | undefined>(() => {
    const raw = this._data();
    if (raw === null || raw === undefined) return undefined;
    if (typeof raw === 'string') {
      try {
        return JSON.parse(raw) as JsonValue;
      } catch {
        return undefined; // parseError() below carries the failure reason
      }
    }
    return raw as JsonValue;
  });

  parseError = computed<string | null>(() => {
    const raw = this._data();
    if (typeof raw !== 'string') return null;
    try {
      JSON.parse(raw);
      return null;
    } catch (e) {
      return e instanceof Error ? e.message : 'Invalid JSON';
    }
  });

  rawText = computed<string | null>(() => {
    const raw = this._data();
    if (raw === null || raw === undefined) return null;
    return typeof raw === 'string' ? raw : JSON.stringify(raw, null, 2);
  });

  formattedJson(): string {
    const value = this.parsed();
    return value === undefined ? '' : JSON.stringify(value, null, 2);
  }

  download() {
    const text = this.rawText();
    if (!text) return;
    const blob = new Blob([text], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${sanitizeDownloadFilename(this.title, 'json').toLowerCase()}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
