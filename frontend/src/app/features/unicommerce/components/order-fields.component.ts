import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RiyalComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';

@Component({
  selector: 'app-order-fields',
  standalone: true,
  imports: [CommonModule, FormsModule, RiyalComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSelectComponent],
  template: `
    <ui-card variant="raised" class="card-section">
      <div uiCardHeader class="section-heading"><span class="section-title"><i class="bi bi-file-earmark-spreadsheet" aria-hidden="true"></i> Invoice Order Headers &amp; Payments</span></div>

      <div class="form-grid">
        <ui-field label="Reference Number" forId="invoice-reference" [required]="true">
          <ui-input inputId="invoice-reference" placeholder="e.g. REF-1002" [value]="$any(orderData['reference_number']) || ''" (valueChange)="onFieldChange('reference_number', $event)"></ui-input>
        </ui-field>
        <ui-field label="Online Order Number" forId="invoice-online-number" [required]="true">
          <ui-input inputId="invoice-online-number" placeholder="e.g. ONL-9988" [value]="$any(orderData['online_order_number']) || ''" (valueChange)="onFieldChange('online_order_number', $event)"></ui-input>
        </ui-field>
        <ui-field label="Customer Carrier Name" forId="invoice-carrier" [required]="true">
          <ui-select selectId="invoice-carrier" [options]="carrierOptions" [value]="$any(orderData['customer_name']) || 'AMAZON'" (valueChange)="onFieldChange('customer_name', $event)"></ui-select>
        </ui-field>
        <ui-field label="Order Creation Date" forId="invoice-creation-date">
          <ui-input inputId="invoice-creation-date" type="datetime-local" [value]="$any(orderData['order_creation_date']) || ''" (valueChange)="onFieldChange('order_creation_date', $event)"></ui-input>
        </ui-field>
        <label class="checkbox-row full-width">
          <input type="checkbox" [ngModel]="$any(orderData['is_return']) === true" (ngModelChange)="onFieldChange('is_return', $event)" />
          <span>Is Return Invoice?</span>
        </label>
        <ui-field *ngIf="orderData['is_return']" label="Parent Reference Number" forId="invoice-parent-reference" [required]="true" class="full-width">
          <ui-input inputId="invoice-parent-reference" placeholder="Original invoice ReferenceNumber..." [value]="$any(orderData['parent_reference_number']) || ''" (valueChange)="onFieldChange('parent_reference_number', $event)"></ui-input>
        </ui-field>
        <ui-field label="Paid Online Amount" forId="invoice-paid-online">
          <ui-input inputId="invoice-paid-online" type="number" step="0.01" [value]="$any(orderData['paid_online_amount']) || 0" (valueChange)="onFieldChange('paid_online_amount', $event)">
            <app-riyal uiInputSuffix [size]=".9"></app-riyal>
          </ui-input>
        </ui-field>
        <ui-field label="Paid With Points Amount" forId="invoice-paid-points">
          <ui-input inputId="invoice-paid-points" type="number" step="0.01" [value]="$any(orderData['paid_with_points_amount']) || 0" (valueChange)="onFieldChange('paid_with_points_amount', $event)">
            <app-riyal uiInputSuffix [size]=".9"></app-riyal>
          </ui-input>
        </ui-field>
      </div>
    </ui-card>
  `,
  styles: [`
    .card-section { margin-bottom: 24px; }
    .section-heading { display: flex; align-items: center; gap: 10px; }
    .section-title { display: inline-flex; align-items: center; gap: 10px; color: var(--text-primary); }
    .section-title i { color: var(--accent); }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .checkbox-row { display: flex; align-items: center; gap: 8px; color: var(--text-primary); font-size: .9rem; font-weight: 650; cursor: pointer; }
    .checkbox-row input { accent-color: var(--accent); }
  `]
})
export class OrderFieldsComponent {
  @Input() orderData: Record<string, unknown> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();

  readonly carrierOptions: UiSelectOption[] = [
    'AMAZON', 'ARAMEX', 'NAQEL', 'SMSA', 'TABBY', 'TAMARA', 'TRENDYOL', 'REDBOX', 'OWNFLEET'
  ].map(value => ({ value, label: value }));

  onFieldChange(fieldName: string, value: unknown) {
    this.fieldChange.emit({ fieldName, value });
  }
}
