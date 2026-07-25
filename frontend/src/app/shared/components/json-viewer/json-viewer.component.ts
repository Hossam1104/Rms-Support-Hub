import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-json-viewer',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="json-box glass-card">
      <div class="json-header">
        <span class="json-title">{{ title || 'JSON Data' }}</span>
        <div class="json-actions">
          <button type="button" class="btn-icon" (click)="copyToClipboard()" title="Copy JSON">
            <i class="bi" [class.bi-clipboard]="!copied()" [class.bi-check2]="copied()"></i>
          </button>
          <button type="button" class="btn-icon" (click)="toggleCollapse()">
            <i class="bi" [class.bi-chevron-down]="!collapsed()" [class.bi-chevron-right]="collapsed()"></i>
          </button>
        </div>
      </div>
      @if (!collapsed()) {
        <pre class="json-content"><code>{{ formattedJson() }}</code></pre>
      }
    </div>
  `,
  styles: [`
    .json-box { padding: 12px; margin-top: 8px; }
    .json-header { display: flex; justify-content: space-between; align-items: center; }
    .json-title { font-weight: 600; font-size: 0.85rem; color: var(--text-secondary); }
    .json-actions { display: flex; gap: 8px; }
    .btn-icon { background: none; border: none; color: var(--text-secondary); cursor: pointer; font-size: 1rem; }
    .json-content { margin-top: 8px; padding: 12px; background: var(--bg-tertiary); border-radius: var(--radius-sm); max-height: 400px; overflow-y: auto; color: var(--primary-hover); font-size: 0.85rem; }
  `]
})
export class JsonViewerComponent {
  @Input() data: any;
  @Input() title?: string;

  collapsed = signal<boolean>(false);
  copied = signal<boolean>(false);

  formattedJson(): string {
    if (typeof this.data === 'string') {
      try {
        return JSON.stringify(JSON.parse(this.data), null, 2);
      } catch {
        return this.data;
      }
    }
    return JSON.stringify(this.data, null, 2);
  }

  toggleCollapse() {
    this.collapsed.update(c => !c);
  }

  copyToClipboard() {
    navigator.clipboard.writeText(this.formattedJson());
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 2000);
  }
}
