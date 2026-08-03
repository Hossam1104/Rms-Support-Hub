import { Component, Input, Output, EventEmitter, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="pagination">
      <span class="pagination-summary">
        Showing {{ rangeStart() }}–{{ rangeEnd() }} of {{ total }}
      </span>

      <div class="pagination-controls">
        <select class="page-size-select" [ngModel]="pageSize" (ngModelChange)="pageSizeChange.emit($event)">
          <option *ngFor="let size of pageSizeOptions" [value]="size">{{ size }} / page</option>
        </select>

        <button type="button" class="page-btn" [disabled]="page <= 1" (click)="pageChange.emit(page - 1)">
          <i class="bi bi-chevron-left"></i>
        </button>
        <span class="page-current">{{ page }} / {{ totalPages() || 1 }}</span>
        <button type="button" class="page-btn" [disabled]="page >= totalPages()" (click)="pageChange.emit(page + 1)">
          <i class="bi bi-chevron-right"></i>
        </button>
      </div>
    </div>
  `,
  styles: [`
    .pagination { display: flex; justify-content: space-between; align-items: center; gap: 16px; flex-wrap: wrap; padding: 16px 4px; }
    .pagination-summary { font-size: 0.85rem; color: var(--text-muted); }
    .pagination-controls { display: flex; align-items: center; gap: 10px; }
    .page-size-select {
      background: var(--surface-interactive);
      color: var(--text-primary);
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-sm);
      padding: 4px 8px;
      font-size: 0.8rem;
    }
    .page-btn {
      width: 32px; height: 32px;
      display: flex; align-items: center; justify-content: center;
      border-radius: var(--radius-sm);
      border: 1px solid var(--border-subtle);
      background: var(--surface-interactive);
      color: var(--text-primary);
      cursor: pointer;
      transition: background var(--transition-fast), transform var(--transition-fast);
    }
    .page-btn:hover:not(:disabled) { background: var(--surface-hover); transform: translateY(-1px); }
    .page-btn:disabled { opacity: 0.4; cursor: not-allowed; }
    .page-current { font-size: 0.85rem; font-weight: 600; color: var(--text-primary); min-width: 60px; text-align: center; }
  `]
})
export class PaginationComponent {
  @Input() page: number = 1;
  @Input() pageSize: number = 25;
  @Input() total: number = 0;
  @Input() pageSizeOptions: number[] = [25, 50, 100, 200];

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  totalPages = computed(() => Math.max(1, Math.ceil(this.total / (this.pageSize || 1))));
  rangeStart = () => this.total === 0 ? 0 : (this.page - 1) * this.pageSize + 1;
  rangeEnd = () => Math.min(this.total, this.page * this.pageSize);
}
