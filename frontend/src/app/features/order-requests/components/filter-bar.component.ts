import { Component, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OrderRequestsStore, QuickRange } from '../order-requests.store';
import { FilterChipComponent, SearchableSelectComponent, UiToolbarComponent } from '../../../shared/ui';

const STATUS_CHIPS = [
  { value: 1, label: 'New' }, { value: 2, label: 'Confirmed' }, { value: 3, label: 'Ready' },
  { value: 4, label: 'With Delegate' }, { value: 5, label: 'Rejected' }, { value: 6, label: 'Canceled (Client)' },
  { value: 7, label: 'Canceled (Admin)' }, { value: 8, label: 'Processing' }, { value: 9, label: 'Done' }
];

/**
 * Sticky filter bar -- search (debounced 300ms), phone, branch picker
 * (from GET .../modules/{key}/branches), 9 multi-select status chips, an
 * outcome segmented control, date range + quick ranges, and Clear all.
 * Reads/writes OrderRequestsStore directly rather than @Input/@Output
 * plumbing -- this component only ever exists inside the store's provider
 * scope (see order-requests.component.ts).
 */
@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [CommonModule, FormsModule, FilterChipComponent, SearchableSelectComponent, UiToolbarComponent],
  template: `
    <ui-toolbar class="filter-bar" role="region" ariaLabel="Order request filters">
      <div class="filter-row">
        <div class="filter-group filter-group--search">
          <label for="order-request-search">
            <span>Search order number</span>
            <span class="label-note">Exact match</span>
          </label>
          <div class="input-with-icon">
            <i class="bi bi-search" aria-hidden="true"></i>
            <input id="order-request-search" type="search" autocomplete="off" [ngModel]="searchInput()" (ngModelChange)="onSearchInput($event)" (keydown.enter)="submitSearch($event)" placeholder="Paste the full order number" />
            <button type="button" class="search-submit" aria-label="Search order requests" [disabled]="!searchInput().trim()" (click)="submitSearch()">
              <i class="bi bi-arrow-up-right" aria-hidden="true"></i>
            </button>
          </div>
        </div>

        <div class="filter-group filter-group--phone">
          <label for="order-request-phone">
            <span>Phone</span>
            <span class="label-note">Last 9 digits</span>
          </label>
          <input id="order-request-phone" type="tel" autocomplete="tel" [ngModel]="phoneInput()" (ngModelChange)="onPhoneInput($event)" placeholder="05xxxxxxxx" />
        </div>

        <div class="filter-group filter-group--branch">
          <app-searchable-select
            label="Branch"
            inputId="order-request-branch"
            placeholder="All branches"
            [options]="store.branches()"
            [value]="store.filters().branchCode"
            [loading]="store.branchStatus() === 'loading'"
            [error]="store.branchError()"
            (valueChange)="store.setFilters({ branchCode: $event })"
            (refresh)="store.loadBranches(true)">
          </app-searchable-select>
        </div>

        <div class="filter-group filter-group--outcome">
          <label>Outcome</label>
          <div class="segmented">
            <button type="button" [class.active]="store.filters().outcome === 'all'" (click)="store.setFilters({ outcome: 'all' })">All</button>
            <button type="button" [class.active]="store.filters().outcome === 'succeeded'" (click)="store.setFilters({ outcome: 'succeeded' })">Succeeded</button>
            <button type="button" [class.active]="store.filters().outcome === 'failed'" (click)="store.setFilters({ outcome: 'failed' })">Failed</button>
          </div>
        </div>

        <div class="filter-group filter-group--dates">
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

      <div class="query-status" *ngIf="store.status() === 'loading'" role="status" aria-live="polite">
        <i class="bi bi-arrow-repeat spin" aria-hidden="true"></i>
        Updating results…
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
    </ui-toolbar>
  `,
  styles: [`
    .filter-bar {
      position: sticky; top: calc(var(--navbar-height) + 12px); z-index: 50;
      background: linear-gradient(135deg, var(--surface-panel), var(--surface-raised));
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-lg);
      padding: 18px 20px;
      margin-bottom: 20px;
      box-shadow: var(--shadow-md);
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .filter-row { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); align-items: end; gap: 14px; }
    .filter-group { display: flex; flex-direction: column; gap: 6px; }
    .filter-group--search { grid-column: span 3; }
    .filter-group--phone { grid-column: span 2; }
    .filter-group--branch { grid-column: span 2; }
    .filter-group--outcome { grid-column: span 2; }
    .filter-group--dates { grid-column: span 2; }
    .filter-group label { display: flex; align-items: center; justify-content: space-between; gap: 8px; min-height: 17px; color: var(--text-secondary); font-size: 0.76rem; font-weight: 750; letter-spacing: .02em; }
    .label-note { color: var(--text-muted); font-size: .67rem; font-weight: 600; letter-spacing: 0; white-space: nowrap; }
    .filter-group input, .filter-group select {
      width: 100%; min-width: 0; min-height: 44px; box-sizing: border-box;
      background: var(--input-bg); border: 1px solid var(--input-border); border-radius: var(--radius-md);
      box-shadow: inset 0 1px 0 var(--input-highlight);
      color: var(--text-primary); padding: 0 13px; font: inherit; font-size: 0.84rem;
      transition: border-color var(--transition-fast), box-shadow var(--transition-fast), background var(--transition-fast);
    }
    .filter-group input::placeholder { color: var(--text-muted); }
    .filter-group input:focus, .filter-group select:focus { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .input-with-icon { position: relative; display: flex; align-items: center; }
    .input-with-icon > i { position: absolute; left: 13px; z-index: 1; color: var(--text-muted); font-size: 0.85rem; pointer-events: none; }
    .input-with-icon input { padding-left: 38px; padding-right: 44px; }
    .search-submit { position: absolute; right: 7px; display: grid; width: 30px; height: 30px; place-items: center; padding: 0; border: 0; border-radius: var(--radius-sm); background: var(--accent-soft); color: var(--text-accent); cursor: pointer; transition: background var(--transition-fast), color var(--transition-fast), transform var(--transition-fast); }
    .search-submit:hover:not(:disabled) { background: var(--accent); color: var(--text-inverse); transform: translateX(1px); }
    .search-submit:disabled { opacity: .45; cursor: not-allowed; }
    .search-submit:focus-visible, .segmented button:focus-visible, .quick-ranges button:focus-visible, .status-chip:focus-visible, .btn-clear:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .segmented { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 3px; min-height: 44px; padding: 3px; border: 1px solid var(--input-border); border-radius: var(--radius-md); background: var(--surface-interactive); }
    .segmented button { min-width: 0; border: 0; border-radius: var(--radius-sm); background: transparent; color: var(--text-secondary); padding: 0 7px; font: inherit; font-size: 0.75rem; cursor: pointer; transition: background var(--transition-fast), color var(--transition-fast), box-shadow var(--transition-fast); }
    .segmented button:hover:not(.active) { background: var(--surface-hover); color: var(--text-primary); }
    .segmented button.active { background: var(--grad-brand); box-shadow: var(--shadow-sm); color: var(--on-gradient); font-weight: 750; }
    .date-range { display: grid; grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr); align-items: center; gap: 6px; }
    .date-range input { min-width: 0; padding-inline: 9px; font-size: 0.75rem; }
    .date-range > span { color: var(--text-muted); font-size: .75rem; }
    .quick-ranges { display: flex; gap: 6px; margin-top: 2px; }
    .quick-ranges button {
      background: transparent; border: 1px solid var(--border-subtle); border-radius: var(--radius-pill);
      color: var(--text-secondary); font-size: 0.7rem; padding: 3px 9px; cursor: pointer; transition: background var(--transition-fast), border-color var(--transition-fast), color var(--transition-fast);
    }
    .quick-ranges button:hover { color: var(--text-accent); background: var(--surface-selected); border-color: var(--border-focus); }
    .btn-clear {
      display: inline-flex; align-items: center; justify-content: center; gap: 6px; min-height: 44px; align-self: end;
      background: var(--surface-interactive); border: 1px solid var(--border-strong); border-radius: var(--radius-md);
      color: var(--text-secondary); padding: 0 13px; font: inherit; font-size: 0.78rem; cursor: pointer; transition: background var(--transition-fast), color var(--transition-fast), border-color var(--transition-fast);
    }
    .btn-clear:hover:not(:disabled) { background: var(--surface-hover); color: var(--text-primary); border-color: var(--border-focus); }
    .btn-clear:disabled { opacity: 0.4; cursor: not-allowed; }
    .query-status { display: inline-flex; align-items: center; gap: 7px; color: var(--text-accent); font-size: .73rem; font-weight: 700; }
    .status-chip-row { display: flex; flex-wrap: wrap; gap: 8px; overflow-x: auto; padding-bottom: 1px; }
    .status-chip {
      flex: 0 0 auto; border: 1px solid transparent; cursor: pointer; padding: 5px 12px; border-radius: var(--radius-pill);
      font-size: 0.72rem; font-weight: 700; color: var(--on-gradient);
      opacity: 0.45; transition: opacity var(--transition-fast), transform var(--d) var(--ease-spring), box-shadow var(--transition-fast), filter var(--transition-fast);
    }
    .status-chip:hover { opacity: .82; filter: saturate(1.12); }
    .status-chip.selected { opacity: 1; transform: translateY(-1px); box-shadow: var(--shadow-sm), 0 0 0 2px var(--surface-panel); }
    .active-chips { display: flex; flex-wrap: wrap; gap: 8px; }
    @media (max-width: 1080px) {
      .filter-group--search, .filter-group--phone, .filter-group--branch, .filter-group--outcome, .filter-group--dates, .btn-clear { grid-column: span 3; }
    }
    @media (max-width: 640px) {
      .filter-bar { top: calc(var(--navbar-height) + 8px); padding: 15px; }
      .filter-row { grid-template-columns: 1fr; gap: 12px; }
      .filter-group--search, .filter-group--phone, .filter-group--branch, .filter-group--outcome, .filter-group--dates, .btn-clear { grid-column: 1; }
      .btn-clear { width: 100%; }
    }
  `]
})
export class FilterBarComponent {
  store = inject(OrderRequestsStore);
  statusChips = STATUS_CHIPS;

  searchInput = signal(this.store.filters().search);
  phoneInput = signal(this.store.filters().phone);
  private searchInput$ = new Subject<string>();
  private phoneInput$ = new Subject<string>();
  private searchDirty = false;
  private phoneDirty = false;

  constructor() {
    effect(() => {
      const filters = this.store.filters();
      if (!this.searchDirty) this.searchInput.set(filters.search);
      if (!this.phoneDirty) this.phoneInput.set(filters.phone);
    });

    this.searchInput$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(value => this.applySearch(value));

    this.phoneInput$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe(value => this.applyPhone(value));
  }

  onSearchInput(value: string) {
    this.searchDirty = true;
    this.searchInput.set(value);
    this.searchInput$.next(value);
  }

  onPhoneInput(value: string) {
    this.phoneDirty = true;
    this.phoneInput.set(value);
    this.phoneInput$.next(value);
  }

  submitSearch(event?: Event) {
    event?.preventDefault();
    const value = this.searchInput().trim();
    this.searchInput.set(value);
    this.applySearch(value);
  }

  private applySearch(value: string) {
    const normalized = value.trim();
    this.searchDirty = false;
    this.searchInput.set(normalized);
    if (this.store.filters().search !== normalized) this.store.setFilters({ search: normalized });
  }

  private applyPhone(value: string) {
    const normalized = value.trim();
    this.phoneDirty = false;
    this.phoneInput.set(normalized);
    if (this.store.filters().phone !== normalized) this.store.setFilters({ phone: normalized });
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
    this.searchDirty = false;
    this.phoneDirty = false;
    this.searchInput.set('');
    this.phoneInput.set('');
    this.searchInput$.next('');
    this.phoneInput$.next('');
    this.store.clearFilters();
  }
}
