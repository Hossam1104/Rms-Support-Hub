import { Component, Input, ContentChild, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrollingModule } from '@angular/cdk/scrolling';

export interface DataTableColumn {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
}

/**
 * Generic virtual-scrolled table. Renders `row[column.key]` as text by
 * default; project a <ng-template #cell let-row let-column="column"> to
 * override cell rendering (e.g. a status-pill or riyal amount) per column.
 * Rows stagger in on first render via CSS animation-delay, capped so a
 * long list doesn't produce a multi-second cascade.
 */
@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [CommonModule, ScrollingModule],
  template: `
    <div class="data-table" [style.height]="height">
      <div class="table-head" [style.gridTemplateColumns]="gridTemplate()">
        <div *ngFor="let col of columns" class="head-cell" [class]="'align-' + (col.align || 'left')">{{ col.label }}</div>
      </div>

      <cdk-virtual-scroll-viewport [itemSize]="rowHeight" class="table-body">
        <div
          *cdkVirtualFor="let row of rows; let i = index"
          class="table-row"
          [style.gridTemplateColumns]="gridTemplate()"
          [style.animationDelay.ms]="rowDelay(i)">
          <div *ngFor="let col of columns" class="body-cell" [class]="'align-' + (col.align || 'left')">
            <ng-container *ngIf="cellTemplate; else plainCell" [ngTemplateOutlet]="cellTemplate" [ngTemplateOutletContext]="{ $implicit: row, column: col }"></ng-container>
            <ng-template #plainCell>{{ asDisplay(row[col.key]) }}</ng-template>
          </div>
        </div>
      </cdk-virtual-scroll-viewport>
    </div>
  `,
  styles: [`
    .data-table { display: flex; flex-direction: column; border: 1px solid var(--glass-border); border-radius: var(--radius-lg); overflow: hidden; }
    .table-head, .table-row { display: grid; align-items: center; }
    .table-head { background: var(--bg-tertiary); border-bottom: 1px solid var(--glass-border); }
    .head-cell { padding: 12px 16px; font-size: 0.78rem; font-weight: 700; color: var(--text-secondary); text-transform: uppercase; letter-spacing: .02em; }
    .table-body { flex: 1; }
    .table-row {
      border-bottom: 1px solid var(--glass-border);
      transition: background var(--transition-fast);
      animation: rowStaggerIn var(--d-slow) var(--ease-spring) backwards;
    }
    .table-row:hover { background: var(--glass-hover-bg); }
    .body-cell { padding: 12px 16px; font-size: 0.88rem; color: var(--text-primary); }
    .align-right { text-align: right; }
    .align-center { text-align: center; }
    @keyframes rowStaggerIn {
      from { opacity: 0; transform: translateY(10px) scale(.99); }
      to { opacity: 1; transform: translateY(0) scale(1); }
    }
  `]
})
export class DataTableComponent<T extends Record<string, unknown> = Record<string, unknown>> {
  @Input({ required: true }) columns: DataTableColumn[] = [];
  @Input({ required: true }) rows: T[] = [];
  @Input() rowHeight: number = 52;
  @Input() height: string = '480px';

  @ContentChild('cell') cellTemplate?: TemplateRef<{ $implicit: T; column: DataTableColumn }>;

  gridTemplate(): string {
    return this.columns.map(() => '1fr').join(' ');
  }

  /** Staggers only the first ~20 visible rows -- beyond that the delay
   * would just make late rows sit invisible for no visual benefit. */
  rowDelay(index: number): number {
    return Math.min(index, 20) * 35;
  }

  asDisplay(value: unknown): string {
    if (value === null || value === undefined) return '';
    return String(value);
  }
}
