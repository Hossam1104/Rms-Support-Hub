import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OrderRequestsStore, QuickRange } from '../order-requests.store';
import { FilterChipComponent } from '../../../shared/ui';

const STATUS_CHIPS = [
  { value: 1, label: 'New' }, { value: 2, label: 'Confirmed' }, { value: 3, label: 'Ready' },
  { value: 4, label: 'With Delegate' }, { value: 5, label: 'Rejected' }, { value: 6, label: 'Canceled (Client)' },
  { value: 7, label: 'Canceled (Admin)' }, { value: 8, label: 'Processing' }, { value: 9, label: 'Done' }
];

/**
 * Sticky filter bar -- search (debounced 300ms), phone, branch dropdown
 * (from GET .../order-requests/branches), 9 multi-select status chips, an
 * outcome segmented control, date range + quick ranges, and Clear all.
 * Reads/writes OrderRequestsStore directly rather than @Input/@Output
 * plumbing -- this component only ever exists inside the store's provider
 * scope (see order-requests.component.ts).
 */
@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [CommonModule, FormsModule, FilterChipComponent],
  template: `
    <div class="filter-bar">
      <div class="filter-row">
        <div class="filter-group flex-grow">
          <label>Search order number</label>
          <div class="input-with-icon">
            <i class="bi bi-search"></i>
            <input type="text" [ngModel]="searchInput()" (ngModelChange)="onSearchInput($event)" placeholder="e.g. UPC-998822" />
          </div>
        </div>

        <div class="filter-group">
          <label>Phone</label>
          <input type="text" [ngModel]="store.filters().phone" (ngModelChange)="store.setFilters({ phone: $event })" placeholder="05xxxxxxxx" />
        </div>

        <div class="filter-group">
          <label>Branch</label>
          <select [ngModel]="store.filters().branchCode" (ngModelChange)="store.setFilters({ branchCode: $event || null })">
            <option [ngValue]="null">All branches</option>
            @for (b of store.branches(); track b.branchCode) {
              <option [ngValue]="b.branchCode">{{ b.branchCode }} ({{ b.count }})</option>
            }
          </select>
        </div>

        <div class="filter-group">
          <label>Outcome</label>
          <div class="segmented">
            <button type="button" [class.active]="store.filters().outcome === 'all'" (click)="store.setFilters({ outcome: 'all' })">All</button>
            <button type="button" [class.active]="store.filters().outcome === 'succeeded'" (click)="store.setFilters({ outcome: 'succeeded' })">Succeeded</button>
            <button type="button" [class.active]="store.filters().outcome === 'failed'" (click)="store.setFilters({ outcome: 'failed' })">Failed</button>
          </div>
        </div>

        <div class="filter-group">
          <label>Date range</label>
          <div class="date-range">
            <input type="date" [ngModel]="store.filters().dateFrom" (ngModelChange)="store.setFilters({ dateFrom: $event || null })" />
            <span>&ndash;</span>
            <input type="date" [ngModel]="store.filters().dateTo" (ngModelChange)="store.setFilters({ dateTo: $event || null })" />
          </div>
          <div class="quick-ranges">
            <button type="button" (click)="applyQuickRange('today')">Today</button>
            <button type="button" (click)="applyQuickRange('7d')">7d</button>
            <button type="button" (click)="applyQuickRange('30d')">30d</button>
          </div>
        </div>

        <button type="button" class="btn-clear" (click)="clearAll()" [disabled]="!store.hasActiveFilters()">
          <i class="bi bi-arrow-counterclockwise"></i> Clear all
        </button>
      </div>

      <div class="status-chip-row">
        @for (s of statusChips; track s.value) {
          <button
            type="button"
            class="status-chip"
            [class]="'status-pill--' + s.value"
            [class.selected]="isStatusSelected(s.value)"
            (click)="toggleStatus(s.value)">
            {{ s.value }} &middot; {{ s.label }}
          </button>
        }
      </div>

      <div class="active-chips" *ngIf="store.activeFilterChips().length > 0">
        @for (chip of store.activeFilterChips(); track chip.key) {
          <app-filter-chip [label]="chip.label" (remove)="store.removeFilterChip(chip.key)"></app-filter-chip>
        }
      </div>
    </div>
  `,
  styles: [`
    .filter-bar {
      position: sticky; top: 0; z-index: 50;
      background: var(--glass-bg);
      backdrop-filter: blur(14px);
      -webkit-backdrop-filter: blur(14px);
      border: 1px solid var(--glass-border);
      border-radius: var(--radius-lg);
      padding: 18px 20px;
      margin-bottom: 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .filter-row { display: flex; align-items: flex-end; gap: 16px; flex-wrap: wrap; }
    .filter-group { display: flex; flex-direction: column; gap: 6px; }
    .flex-grow { flex: 1; min-width: 220px; }
    .filter-group label { font-size: 0.78rem; font-weight: 600; color: var(--text-secondary); }
    .filter-group input, .filter-group select {
      background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-sm);
      color: var(--text-primary); padding: 7px 10px; font-size: 0.85rem;
    }
    .input-with-icon { position: relative; display: flex; align-items: center; }
    .input-with-icon i { position: absolute; left: 10px; color: var(--text-muted); font-size: 0.85rem; }
    .input-with-icon input { padding-left: 30px; width: 100%; }
    .segmented { display: flex; border: 1px solid var(--glass-border); border-radius: var(--radius-sm); overflow: hidden; }
    .segmented button { background: var(--bg-tertiary); border: none; color: var(--text-secondary); padding: 7px 12px; font-size: 0.8rem; cursor: pointer; }
    .segmented button.active { background: var(--grad-brand); color: var(--on-gradient); font-weight: 600; }
    .date-range { display: flex; align-items: center; gap: 6px; }
    .date-range input { font-size: 0.78rem; padding: 6px 8px; }
    .quick-ranges { display: flex; gap: 6px; margin-top: 2px; }
    .quick-ranges button {
      background: none; border: 1px solid var(--glass-border); border-radius: var(--radius-sm);
      color: var(--text-secondary); font-size: 0.72rem; padding: 2px 8px; cursor: pointer;
    }
    .quick-ranges button:hover { color: var(--text-primary); background: var(--glass-hover-bg); }
    .btn-clear {
      display: flex; align-items: center; gap: 6px;
      background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-sm);
      color: var(--text-secondary); padding: 7px 14px; font-size: 0.8rem; cursor: pointer; height: 34px;
    }
    .btn-clear:disabled { opacity: 0.4; cursor: not-allowed; }
    .status-chip-row { display: flex; flex-wrap: wrap; gap: 8px; }
    .status-chip {
      border: none; cursor: pointer; padding: 5px 12px; border-radius: var(--radius-pill);
      font-size: 0.72rem; font-weight: 700; color: var(--on-gradient);
      opacity: 0.45; transition: opacity var(--transition-fast), transform var(--d) var(--ease-spring);
    }
    .status-chip.selected { opacity: 1; transform: scale(1.05); box-shadow: var(--shadow-sm); }
    .active-chips { display: flex; flex-wrap: wrap; gap: 8px; }
  `]
})
export class FilterBarComponent {
  store = inject(OrderRequestsStore);
  statusChips = STATUS_CHIPS;

  searchInput = signal(this.store.filters().search);
  private searchInput$ = new Subject<string>();

  constructor() {
    this.searchInput$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(value => this.store.setFilters({ search: value }));
  }

  onSearchInput(value: string) {
    this.searchInput.set(value);
    this.searchInput$.next(value);
  }

  isStatusSelected(status: number): boolean {
    return this.store.filters().statuses.includes(status);
  }

  toggleStatus(status: number) {
    const current = this.store.filters().statuses;
    const next = current.includes(status) ? current.filter(s => s !== status) : [...current, status];
    this.store.setFilters({ statuses: next });
  }

  applyQuickRange(range: QuickRange) {
    this.store.applyQuickRange(range);
  }

  clearAll() {
    this.searchInput.set('');
    this.store.clearFilters();
  }
}
