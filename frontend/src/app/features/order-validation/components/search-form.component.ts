import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';

export interface OrderSearchFilters {
  orderNumber: string;
  phone: string;
  branchCode: string;
  status: number | null;
}

@Component({
  selector: 'app-search-form',
  standalone: true,
  imports: [CommonModule, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSelectComponent],
  template: `
    <ui-card variant="raised" class="search-card">
      <div uiCardHeader class="section-heading">
        <span class="section-title"><i class="bi bi-search" aria-hidden="true"></i> Search Database Orders (UPC)</span>
        <span class="superseded-label">Superseded by Order Requests</span>
      </div>

      <div class="form-grid">
        <ui-field label="Order Number" forId="validation-order-number">
          <ui-input inputId="validation-order-number" placeholder="e.g. UPC-998822" [value]="filters.orderNumber" (valueChange)="setText('orderNumber', $event)"></ui-input>
        </ui-field>
        <ui-field label="Customer Mobile" forId="validation-phone">
          <ui-input inputId="validation-phone" type="tel" placeholder="05xxxxxxxx" [value]="filters.phone" (valueChange)="setText('phone', $event)"></ui-input>
        </ui-field>
        <ui-field label="Branch Code" forId="validation-branch">
          <ui-input inputId="validation-branch" placeholder="e.g. 201" [value]="filters.branchCode" (valueChange)="setText('branchCode', $event)"></ui-input>
        </ui-field>
        <ui-field label="Order Status" forId="validation-status">
          <ui-select selectId="validation-status" placeholder="All Statuses" [options]="statusOptions" [value]="statusValue()" (valueChange)="setStatus($event)"></ui-select>
        </ui-field>
      </div>

      <div uiCardFooter class="form-actions">
        <ui-button variant="secondary" icon="bi-arrow-counterclockwise" (pressed)="resetFilters()">Clear</ui-button>
        <ui-button icon="bi-search" (pressed)="onSearch()">Search Database</ui-button>
      </div>
    </ui-card>
  `,
  styles: [`
    .search-card { margin-bottom: 24px; }
    .section-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .section-title { display: inline-flex; align-items: center; gap: 10px; color: var(--text-primary); }
    .section-title i { color: var(--accent); }
    .superseded-label { color: var(--state-warning-fg); background: var(--state-warning-bg); border: 1px solid var(--state-warning-border); border-radius: var(--radius-pill); padding: 4px 10px; font-size: .7rem; font-weight: 800; letter-spacing: .03em; text-transform: uppercase; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 10px; }
    @media (max-width: 620px) { .section-heading { align-items: flex-start; flex-direction: column; } .superseded-label { align-self: flex-start; } }
  `]
})
export class SearchFormComponent {
  @Output() search = new EventEmitter<OrderSearchFilters>();

  filters: OrderSearchFilters = { orderNumber: '', phone: '', branchCode: '', status: null };

  readonly statusOptions: UiSelectOption[] = [
    { value: '1', label: '1 - New' }, { value: '2', label: '2 - Confirmed' }, { value: '3', label: '3 - Ready' },
    { value: '4', label: '4 - With Delegate' }, { value: '5', label: '5 - Rejected' }, { value: '6', label: '6 - Canceled Client' },
    { value: '7', label: '7 - Canceled Admin' }, { value: '8', label: '8 - Processing' }, { value: '9', label: '9 - Done' }
  ];

  setText(field: 'orderNumber' | 'phone' | 'branchCode', value: unknown) {
    this.filters = { ...this.filters, [field]: String(value ?? '') };
  }

  setStatus(value: string | null) {
    this.filters = { ...this.filters, status: value ? Number(value) : null };
  }

  statusValue(): string { return this.filters.status === null ? '' : `${this.filters.status}`; }

  onSearch() { this.search.emit({ ...this.filters }); }

  resetFilters() {
    this.filters = { orderNumber: '', phone: '', branchCode: '', status: null };
    this.onSearch();
  }
}
