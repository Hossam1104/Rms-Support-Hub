import { Component, Input, OnChanges, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

/** Depth up to which containers auto-expand when a JSON tree first renders:
 * root (0), first-level (1), and one further useful level (2). Deeper
 * containers stay collapsed until toggled or reached via Expand all/search. */
export const DEFAULT_AUTO_EXPAND_DEPTH = 2;

export interface JsonTreeExpandCommand {
  token: number;
  expand: boolean;
}

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

interface HighlightPart {
  text: string;
  match: boolean;
}

@Component({
  selector: 'app-json-tree-node',
  standalone: true,
  // Self-imported: Angular standalone components must list themselves to
  // use their own selector recursively in their own template.
  imports: [CommonModule, JsonTreeNodeComponent],
  template: `
    <div class="node">
      <div class="node-row" [class.is-container]="isContainer">
        <button *ngIf="isContainer" type="button" class="toggle-button"
          [attr.aria-expanded]="expanded()"
          [attr.aria-label]="(expanded() ? 'Collapse ' : 'Expand ') + (keyLabel ?? 'root')"
          (click)="toggle()">
          <i class="bi toggle-icon" [class.bi-chevron-down]="expanded()" [class.bi-chevron-right]="!expanded()" aria-hidden="true"></i>
        </button>
        <span *ngIf="!isContainer" class="toggle-spacer"></span>

        <button type="button" class="node-key" *ngIf="keyLabel !== null" (click)="copyPath($event)" [title]="'Copy path: ' + path">
          @for (part of highlightParts(keyLabel ?? ''); track $index) {
            @if (part.match) {
              <mark>{{ part.text }}</mark>
            } @else {
              {{ part.text }}
            }
          }
        </button>
        <span class="node-key-suffix" *ngIf="keyLabel !== null">:</span>

        <span *ngIf="isContainer" class="node-summary" [class.is-array]="isArray">
          {{ isArray ? 'Array' : 'Object' }} &middot; {{ childCount }}
        </span>

        <span *ngIf="!isContainer" class="node-value" [class]="'type-' + valueType">
          @for (part of highlightParts(displayValue); track $index) {
            @if (part.match) {
              <mark>{{ part.text }}</mark>
            } @else {
              {{ part.text }}
            }
          }
        </span>

        <i *ngIf="justCopied()" class="bi bi-check2 copied-flash"></i>
      </div>

      <div class="node-children" *ngIf="isContainer && expanded()">
        <app-json-tree-node
          *ngFor="let entry of entries; trackBy: trackByPath"
          [value]="entry.value"
          [keyLabel]="entry.key"
          [path]="entry.path"
          [searchTerm]="searchTerm"
          [depth]="depth + 1"
          [expandCommand]="expandCommand">
        </app-json-tree-node>
      </div>
    </div>
  `,
  styles: [`
    .node-row {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: 3px 4px;
      border-radius: var(--radius-sm);
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.82rem;
      cursor: default;
      transition: background var(--transition-fast, .12s ease);
    }
    .node-row.is-container:hover { background: var(--surface-hover); }
    .toggle-button { display: grid; flex: 0 0 22px; place-items: center; width: 22px; height: 24px; padding: 0; border: 0; border-radius: var(--radius-sm); background: transparent; color: var(--text-muted); cursor: pointer; }
    .toggle-button:hover { background: var(--surface-selected); color: var(--text-primary); }
    .toggle-button:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .toggle-icon { width: 14px; font-size: 0.75rem; flex-shrink: 0; }
    .toggle-spacer { width: 18px; flex-shrink: 0; }
    .node-key { padding: 0; border: 0; background: transparent; color: var(--accent-violet); cursor: pointer; font: inherit; font-weight: 650; text-align: left; }
    .node-key:hover { text-decoration: underline; }
    .node-key:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }
    .node-key-suffix { color: var(--text-primary); }
    .node-summary { color: var(--text-muted); font-size: 0.74rem; font-weight: 650; letter-spacing: .01em; }
    .node-summary.is-array { color: var(--code-number); }
    .type-string { color: var(--code-string); }
    .type-number { color: var(--code-number); }
    .type-boolean { color: var(--code-boolean); }
    .type-null { color: var(--code-null); font-style: italic; }
    .node-children { margin-left: 20px; border-left: 1px dashed var(--border-subtle); padding-left: 11px; }
    .copied-flash { color: var(--code-string); font-size: 0.75rem; }
    :host ::ng-deep mark { background: var(--accent-amber); color: var(--on-amber); border-radius: 2px; padding: 0 1px; }
  `]
})
export class JsonTreeNodeComponent implements OnChanges {
  @Input({ required: true }) value!: JsonValue;
  @Input() keyLabel: string | null = null;
  @Input() path: string = 'root';
  @Input() searchTerm: string = '';
  @Input() depth: number = 0;
  @Input() expandCommand: JsonTreeExpandCommand | null = null;

  expandedState = signal<boolean | null>(null);
  justCopied = signal(false);
  private appliedCommandToken = -1;

  ngOnChanges(changes: SimpleChanges) {
    if (!changes['expandCommand'] || !this.expandCommand || !this.isContainer) return;
    if (this.expandCommand.token === this.appliedCommandToken) return;
    this.appliedCommandToken = this.expandCommand.token;
    this.expandedState.set(this.expandCommand.expand);
  }

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
    // An active search match always wins so a manually collapsed ancestor
    // can never permanently hide a matching descendant.
    if (this.searchTerm && containsMatch(this.value, this.searchTerm.toLowerCase())) return true;
    if (this.expandedState() !== null) return this.expandedState()!;
    return this.depth <= DEFAULT_AUTO_EXPAND_DEPTH;
  }

  toggle() {
    this.expandedState.set(!this.expanded());
  }

  trackByPath(_index: number, entry: { path: string }): string {
    return entry.path;
  }

  copyPath(event: Event) {
    event.stopPropagation();
    void navigator.clipboard.writeText(this.path).catch(() => undefined);
    this.justCopied.set(true);
    setTimeout(() => this.justCopied.set(false), 1200);
  }

  highlightParts(text: string): HighlightPart[] {
    const term = this.searchTerm.trim().toLocaleLowerCase();
    if (!term) return [{ text, match: false }];

    const normalizedText = text.toLocaleLowerCase();
    const parts: HighlightPart[] = [];
    let cursor = 0;
    let matchIndex = normalizedText.indexOf(term, cursor);

    while (matchIndex >= 0) {
      if (matchIndex > cursor) parts.push({ text: text.slice(cursor, matchIndex), match: false });
      parts.push({ text: text.slice(matchIndex, matchIndex + term.length), match: true });
      cursor = matchIndex + term.length;
      matchIndex = normalizedText.indexOf(term, cursor);
    }

    if (cursor < text.length) parts.push({ text: text.slice(cursor), match: false });
    return parts.length ? parts : [{ text, match: false }];
  }
}
