import { CommonModule } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { createDefaultOrderRequestFilters, OrderRequestsFilterState, OrderRequestsStore } from '../order-requests.store';
import { ORDER_REQUEST_STATUSES } from '../../../core/models';
import { DateRangePickerComponent, DateRangeSelection } from './date-range-picker.component';
import {
  FilterChipComponent,
  SearchableSelectComponent,
  UiButtonComponent,
  UiCardComponent,
  UiFieldComponent,
  UiInputComponent
} from '../../../shared/ui';

/**
 * Explicit-apply filter workbench for the Order Requests grid. Keeping a
 * local draft prevents every keystroke from starting an expensive database
 * request, while Enter still provides a fast path for order-number searches.
 */
@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [
    CommonModule,
    FilterChipComponent,
    SearchableSelectComponent,
    UiButtonComponent,
    UiCardComponent,
    UiFieldComponent,
    UiInputComponent,
    DateRangePickerComponent
  ],
  template: `
    <ui-card variant="raised" class="filter-card order-request-filter">
      <section class="filter-workbench" role="region" aria-label="Order request search and filters">
        <header class="workbench-header">
          <div class="workbench-heading">
            <span class="eyebrow">Search &amp; filters</span>
            <div class="heading-line">
              <h2>Find and monitor order requests</h2>
              <span class="filter-count" aria-live="polite">{{ store.activeFilterChips().length }} active</span>
            </div>
            <p>Combine filters to narrow the operational queue.</p>
          </div>

          <div class="workbench-actions">
            <span class="environment-badge" [class.environment-badge--production]="isProduction()">
              <i class="bi bi-shield-check" aria-hidden="true"></i>
              {{ store.envKey() || 'Default environment' }}
            </span>
            <ui-button
              variant="secondary"
              size="sm"
              icon="bi-arrow-clockwise"
              [loading]="store.status() === 'loading'"
              loadingLabel="Refreshing"
              ariaLabel="Refresh order requests"
              (pressed)="store.refresh()">
              Refresh
            </ui-button>
            <label class="auto-refresh-control">
              <input
                type="checkbox"
                [checked]="store.autoRefresh()"
                (change)="toggleAutoRefresh($event)" />
              <span class="switch" aria-hidden="true"></span>
              <span>Auto-refresh <small>30s</small></span>
            </label>
            <ui-button
              variant="ghost"
              size="sm"
              icon="bi-sliders"
              [ariaExpanded]="!collapsed()"
              ariaLabel="Collapse or expand filters"
              (pressed)="toggleCollapsed()">
              {{ collapsed() ? 'Show filters' : 'Hide filters' }}
            </ui-button>
          </div>
        </header>

        @if (!collapsed()) {
          <div class="primary-filter-grid" role="group" aria-label="Order request filters">
            <ui-field
              label="Order number"
              forId="order-request-search"
              hint="Exact match by default">
              <ui-input
                inputId="order-request-search"
                type="search"
                autocomplete="off"
                placeholder="Search by order number"
                [value]="draft().search"
                (valueChange)="patchDraft({ search: stringValue($event) })"
                (keydown.enter)="apply($event)">
                <i uiInputPrefix class="bi bi-search" aria-hidden="true"></i>
              </ui-input>
              <label class="exact-match-control">
                <input
                  type="checkbox"
                  [checked]="draft().exactMatch"
                  (change)="patchDraft({ exactMatch: checkboxValue($event) })" />
                <span>Exact match</span>
              </label>
            </ui-field>

            <ui-field label="Phone" forId="order-request-phone" hint="Searches the last 9 digits">
              <ui-input
                inputId="order-request-phone"
                type="tel"
                autocomplete="tel"
                placeholder="05xxxxxxxx"
                [value]="draft().phone"
                (valueChange)="patchDraft({ phone: stringValue($event) })">
                <i uiInputPrefix class="bi bi-telephone" aria-hidden="true"></i>
              </ui-input>
            </ui-field>

            <ui-field label="Branch" forId="order-request-branch">
              <app-searchable-select
                label="Branch"
                inputId="order-request-branch"
                placeholder="All branches"
                [options]="store.branches()"
                [value]="draft().branchCode"
                [loading]="store.branchStatus() === 'loading'"
                [error]="store.branchError()"
                (valueChange)="patchDraft({ branchCode: $event })"
                (refresh)="store.loadBranches(true)">
              </app-searchable-select>
            </ui-field>

            <div class="field-block outcome-field">
              <span class="field-label" id="order-request-outcome-label">Outcome</span>
              <div class="segmented-control" role="group" aria-labelledby="order-request-outcome-label">
                <button
                  type="button"
                  [class.active]="draft().outcome === 'all'"
                  [attr.aria-pressed]="draft().outcome === 'all'"
                  (click)="patchDraft({ outcome: 'all' })">All</button>
                <button
                  type="button"
                  [class.active]="draft().outcome === 'succeeded'"
                  [attr.aria-pressed]="draft().outcome === 'succeeded'"
                  (click)="patchDraft({ outcome: 'succeeded' })">Succeeded</button>
                <button
                  type="button"
                  [class.active]="draft().outcome === 'failed'"
                  [attr.aria-pressed]="draft().outcome === 'failed'"
                  (click)="patchDraft({ outcome: 'failed' })">Failed</button>
              </div>
              <span class="field-hint">Derived from the request outcome</span>
            </div>

            <div class="field-block date-range-field">
              <app-date-range-picker
                [dateFrom]="draft().dateFrom"
                [dateTo]="draft().dateTo"
                (rangeChange)="onDateRangeChanged($event)">
              </app-date-range-picker>
              @if (dateRangeError(); as error) {
                <span class="date-range-error">{{ error }}</span>
              }
            </div>
          </div>

          <section class="status-section" aria-labelledby="order-request-status-label">
            <div class="status-heading">
              <div>
                <span class="field-label" id="order-request-status-label">Status</span>
                <span class="field-hint">Select one or more request states</span>
              </div>
              @if (draft().statuses.length) {
                <button type="button" class="clear-status" (click)="clearStatusSelection()">Clear selection</button>
              }
            </div>
            <div class="status-chip-row" role="group" aria-labelledby="order-request-status-label">
              @for (status of statusOptions; track status.value) {
                <button
                  type="button"
                  [class]="'status-chip status-pill--' + status.value"
                  [class.selected]="isStatusSelected(status.value)"
                  [attr.aria-pressed]="isStatusSelected(status.value)"
                  (click)="toggleStatus(status.value)">
                  <span class="status-dot" aria-hidden="true"></span>
                  {{ status.label }}
                </button>
              }
            </div>
          </section>

          <div class="filter-footer">
            <div class="active-filter-area" aria-live="polite">
              @if (store.activeFilterChips().length) {
                <span class="active-label">Applied</span>
                <div class="active-chips">
                  @for (chip of store.activeFilterChips(); track chip.key) {
                    <app-filter-chip [label]="chip.label" (remove)="store.removeFilterChip(chip.key)"></app-filter-chip>
                  }
                </div>
              } @else {
                <span class="no-active-filters">No filters applied yet</span>
              }
            </div>

            <div class="filter-actions">
              <span class="result-context">
                {{ store.total() | number }} matching
                @if (store.lastUpdatedAt(); as updatedAt) {
                  <span>&middot; Updated {{ updatedAt | date:'shortTime' }}</span>
                }
              </span>
              <ui-button
                variant="secondary"
                size="sm"
                [disabled]="!draftDirty() && store.status() !== 'error'"
                icon="bi-search"
                (pressed)="apply()">
                Apply filters
              </ui-button>
              <ui-button
                variant="ghost"
                size="sm"
                icon="bi-arrow-counterclockwise"
                [disabled]="!store.hasActiveFilters() && !draftDirty()"
                (pressed)="clearAll()">
                Clear all
              </ui-button>
            </div>
          </div>
        }

        @if (store.status() === 'loading') {
          <div class="query-status" role="status" aria-live="polite">
            <span class="loading-dot" aria-hidden="true"></span>
            Updating results...
          </div>
        }
        @if (store.errorMessage(); as errorMessage) {
          <div class="query-error" role="alert">
            <i class="bi bi-exclamation-triangle" aria-hidden="true"></i>
            <span>{{ errorMessage }}</span>
            <ui-button variant="ghost" size="sm" icon="bi-arrow-repeat" (pressed)="apply()">Retry</ui-button>
          </div>
        }
      </section>
    </ui-card>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
  `]
})
export class FilterBarComponent {
  readonly store = inject(OrderRequestsStore);
  readonly statusOptions = ORDER_REQUEST_STATUSES;
  readonly draft = signal<OrderRequestsFilterState>(this.cloneFilters(this.store.filters()));
  readonly draftDirty = signal(false);
  readonly collapsed = signal(false);
  readonly dateRangeError = signal<string | null>(null);

  constructor() {
    effect(() => {
      const applied = this.store.filters();
      if (!this.draftDirty()) this.draft.set(this.cloneFilters(applied));
    });
  }

  patchDraft(patch: Partial<OrderRequestsFilterState>) {
    this.draft.update(current => this.cloneFilters({ ...current, ...patch }));
    this.draftDirty.set(true);
    this.dateRangeError.set(null);
  }

  apply(event?: Event) {
    event?.preventDefault();
    const next = this.cloneFilters(this.draft());
    if (next.dateFrom && next.dateTo && next.dateFrom > next.dateTo) {
      this.dateRangeError.set('The start date must be on or before the end date.');
      return;
    }
    this.dateRangeError.set(null);
    this.draftDirty.set(false);
    this.store.applyFilters(next);
  }

  clearAll() {
    this.dateRangeError.set(null);
    this.draftDirty.set(false);
    this.draft.set(this.cloneFilters(createDefaultOrderRequestFilters()));
    this.store.clearFilters(true);
  }

  toggleCollapsed() { this.collapsed.update(value => !value); }

  toggleAutoRefresh(event: Event) {
    this.store.setAutoRefresh((event.target as HTMLInputElement).checked);
  }

  toggleStatus(status: number) {
    const selected = this.draft().statuses;
    const statuses = selected.includes(status)
      ? selected.filter(value => value !== status)
      : [...selected, status];
    this.patchDraft({ statuses });
  }

  clearStatusSelection() { this.patchDraft({ statuses: [] }); }

  isStatusSelected(status: number): boolean { return this.draft().statuses.includes(status); }

  onDateRangeChanged(range: DateRangeSelection) { this.patchDraft(range); }

  stringValue(value: string | number | null): string { return value == null ? '' : String(value); }

  checkboxValue(event: Event): boolean { return (event.target as HTMLInputElement).checked; }

  isProduction(): boolean { return (this.store.envKey() || '').toLocaleLowerCase().includes('production'); }

  private cloneFilters(filters: OrderRequestsFilterState): OrderRequestsFilterState {
    return { ...filters, statuses: [...filters.statuses] };
  }
}
