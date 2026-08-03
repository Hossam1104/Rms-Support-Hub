import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

function isContainer(v: JsonValue): v is JsonValue[] | { [key: string]: JsonValue } {
  return v !== null && typeof v === 'object';
}

function containsMatch(value: JsonValue, term: string): boolean {
  if (!term) return false;
  if (value === null) return 'null'.includes(term);
  if (typeof value !== 'object') return String(value).toLowerCase().includes(term);
  if (Array.isArray(value)) return value.some(v => containsMatch(v, term));
  return Object.entries(value).some(([k, v]) => k.toLowerCase().includes(term) || containsMatch(v, term));
}

@Component({
  selector: 'app-json-tree-node',
  standalone: true,
  // Self-imported: Angular standalone components must list themselves to
  // use their own selector recursively in their own template.
  imports: [CommonModule, JsonTreeNodeComponent],
  template: `
    <div class="node">
      <div class="node-row" [class.is-container]="isContainer" (click)="isContainer && toggle()">
        <i *ngIf="isContainer" class="bi toggle-icon" [class.bi-chevron-down]="expanded()" [class.bi-chevron-right]="!expanded()"></i>
        <span *ngIf="!isContainer" class="toggle-spacer"></span>

        <span class="node-key" *ngIf="keyLabel !== null" (click)="copyPath($event)" [title]="'Copy path: ' + path">
          <span [innerHTML]="highlight(keyLabel)"></span>:
        </span>

        <span *ngIf="isContainer" class="node-summary">
          {{ isArray ? '[' : '{' }}{{ childCount }}{{ isArray ? ']' : '}' }}
        </span>

        <span *ngIf="!isContainer" class="node-value" [class]="'type-' + valueType">
          <span [innerHTML]="highlight(displayValue)"></span>
        </span>

        <i *ngIf="justCopied()" class="bi bi-check2 copied-flash"></i>
      </div>

      <div class="node-children" *ngIf="isContainer && expanded()">
        <app-json-tree-node
          *ngFor="let entry of entries"
          [value]="entry.value"
          [keyLabel]="entry.key"
          [path]="entry.path"
          [searchTerm]="searchTerm"
          [depth]="depth + 1">
        </app-json-tree-node>
      </div>
    </div>
  `,
  styles: [`
    .node-row {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 2px 0;
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.82rem;
      cursor: default;
    }
    .node-row.is-container { cursor: pointer; }
    .toggle-icon { width: 14px; font-size: 0.7rem; color: var(--text-muted); flex-shrink: 0; }
    .toggle-spacer { width: 14px; flex-shrink: 0; }
    .node-key { color: var(--accent-violet); cursor: pointer; }
    .node-key:hover { text-decoration: underline; }
    .node-summary { color: var(--text-muted); }
    .type-string { color: var(--code-string); }
    .type-number { color: var(--code-number); }
    .type-boolean { color: var(--code-boolean); }
    .type-null { color: var(--code-null); font-style: italic; }
    .node-children { margin-left: 18px; border-left: 1px dashed var(--border-subtle); padding-left: 10px; }
    .copied-flash { color: var(--code-string); font-size: 0.75rem; }
    :host ::ng-deep mark { background: var(--accent-amber); color: var(--on-amber); border-radius: 2px; padding: 0 1px; }
  `]
})
export class JsonTreeNodeComponent {
  @Input({ required: true }) value!: JsonValue;
  @Input() keyLabel: string | null = null;
  @Input() path: string = 'root';
  @Input() searchTerm: string = '';
  @Input() depth: number = 0;

  expandedState = signal<boolean | null>(null);
  justCopied = signal(false);

  get isContainer(): boolean { return isContainer(this.value); }
  get isArray(): boolean { return Array.isArray(this.value); }
  get childCount(): number {
    if (!isContainer(this.value)) return 0;
    return Array.isArray(this.value) ? this.value.length : Object.keys(this.value).length;
  }
  get valueType(): string {
    if (this.value === null) return 'null';
    return typeof this.value;
  }
  get displayValue(): string {
    if (this.value === null) return 'null';
    if (typeof this.value === 'string') return `"${this.value}"`;
    return String(this.value);
  }

  get entries(): { key: string, value: JsonValue, path: string }[] {
    if (!isContainer(this.value)) return [];
    if (Array.isArray(this.value)) {
      return this.value.map((v, i) => ({ key: String(i), value: v, path: `${this.path}[${i}]` }));
    }
    return Object.entries(this.value).map(([k, v]) => ({ key: k, value: v, path: `${this.path}.${k}` }));
  }

  expanded(): boolean {
    if (this.expandedState() !== null) return this.expandedState()!;
    if (this.searchTerm && containsMatch(this.value, this.searchTerm.toLowerCase())) return true;
    return this.depth < 1;
  }

  toggle() {
    this.expandedState.set(!this.expanded());
  }

  copyPath(event: Event) {
    event.stopPropagation();
    navigator.clipboard.writeText(this.path);
    this.justCopied.set(true);
    setTimeout(() => this.justCopied.set(false), 1200);
  }

  highlight(text: string): string {
    const escaped = text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    if (!this.searchTerm) return escaped;
    const term = this.searchTerm.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return escaped.replace(new RegExp(`(${term})`, 'ig'), '<mark>$1</mark>');
  }
}
