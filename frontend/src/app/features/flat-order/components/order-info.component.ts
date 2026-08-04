import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BranchOption } from '../../../core/models';
import { RiyalComponent, SearchableSelectComponent, UiFieldComponent, UiInputComponent } from '../../../shared/ui';

@Component({
  selector: 'app-order-info',
  standalone: true,
  imports: [CommonModule, RiyalComponent, SearchableSelectComponent, UiFieldComponent, UiInputComponent],
  template: `
    <div class="form-panel">
      <div class="form-grid">
        <ui-field #branchField label="Branch" forId="field-branch-code" [required]="true" [error]="fieldError('branch_code') || branchError">
          <app-searchable-select
            label="Branch"
            [options]="branches"
            [value]="branchCode()"
            [loading]="branchesLoading"
            [error]="fieldError('branch_code') || branchError"
            inputId="field-branch-code"
            [ariaDescribedBy]="branchField.describedBy()"
            (valueChange)="onBranchChange($event)"
            (refresh)="branchRefresh.emit()">
          </app-searchable-select>
        </ui-field>

        <ui-field #orderCodeField label="Order code" forId="field-order-code" [required]="true" [error]="fieldError('order_code')">
          <ui-input inputId="field-order-code" autocomplete="off" [value]="textValue('order_code')" placeholder="ORD-998822" [invalid]="hasError('order_code')" [ariaDescribedBy]="orderCodeField.describedBy()" (valueChange)="onFieldChange('order_code', $event)"></ui-input>
        </ui-field>

        <ui-field label="Parent order code" forId="field-parent-order-code">
          <ui-input inputId="field-parent-order-code" [value]="textValue('parent_order_code')" placeholder="Optional" (valueChange)="onFieldChange('parent_order_code', $event)"></ui-input>
        </ui-field>

        <ui-field label="Delivery cost" forId="field-delivery-cost" hint="Server totals remain authoritative.">
          <ui-input inputId="field-delivery-cost" type="number" [value]="numberValue('order_delivery_cost')" placeholder="0.00" (valueChange)="onFieldChange('order_delivery_cost', $event)">
            <app-riyal uiInputSuffix [size]=".9"></app-riyal>
          </ui-input>
        </ui-field>

        <ui-field label="Order status" forId="field-order-status">
          <ui-input inputId="field-order-status" [value]="textValue('order_status')" placeholder="new" (valueChange)="onFieldChange('order_status', $event)"></ui-input>
        </ui-field>

        <ui-field label="Order notes" forId="field-order-notes" class="field-span-12">
          <ui-input inputId="field-order-notes" [value]="textValue('order_notes')" placeholder="Special delivery instructions" (valueChange)="onFieldChange('order_notes', $event)"></ui-input>
        </ui-field>

        <ui-field label="GPS latitude" forId="field-gps-latitude">
          <ui-input inputId="field-gps-latitude" type="number" [value]="gpsLat()" placeholder="21.5433" (valueChange)="onGpsChange($event, gpsLng())"></ui-input>
        </ui-field>

        <ui-field label="GPS longitude" forId="field-gps-longitude">
          <ui-input inputId="field-gps-longitude" type="number" [value]="gpsLng()" placeholder="39.1728" (valueChange)="onGpsChange(gpsLat(), $event)"></ui-input>
        </ui-field>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .form-panel { min-width: 0; }
    .form-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 16px; }
    ui-field { grid-column: span 4; }
    ui-field:first-child, ui-field:nth-child(2) { grid-column: span 6; }
    .field-span-12 { grid-column: 1 / -1; }
    @media (max-width: 900px) { ui-field, ui-field:first-child, ui-field:nth-child(2) { grid-column: span 6; } .field-span-12 { grid-column: 1 / -1; } }
    @media (max-width: 620px) { ui-field, ui-field:first-child, ui-field:nth-child(2), .field-span-12 { grid-column: 1 / -1; } }
  `]
})
export class OrderInfoComponent {
  @Input() orderData: Record<string, unknown> = {};
  @Input() moduleKey = '';
  @Input() branches: BranchOption[] = [];
  @Input() branchesLoading = false;
  @Input() branchError: string | null = null;
  /** U4: server send-validation errors keyed by draft field name. */
  @Input() fieldErrors: Record<string, string[]> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();
  @Output() branchRefresh = new EventEmitter<void>();

  hasError(fieldName: string): boolean { return (this.fieldErrors[fieldName]?.length ?? 0) > 0; }
  fieldError(fieldName: string): string | null { return this.fieldErrors[fieldName]?.join(' ') || null; }

  onFieldChange(fieldName: string, value: unknown) { this.fieldChange.emit({ fieldName, value }); }
  onBranchChange(code: string | null) { this.onFieldChange('branch_code', code ?? ''); }

  branchCode(): string | null {
    const value = this.orderData['branch_code'];
    return value == null || value === '' ? null : String(value);
  }

  textValue(fieldName: string): string {
    const value = this.orderData[fieldName];
    return value == null ? '' : String(value);
  }

  numberValue(fieldName: string): number | null {
    const value = this.orderData[fieldName];
    if (value == null || value === '') return null;
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
  }

  private gps(): number[] {
    const raw = this.orderData['order_gps'];
    return Array.isArray(raw) && raw.length === 2 ? raw.map(value => Number(value) || 0) : [21.779006345949554, 39.08578576461103];
  }
  gpsLat(): number { return this.gps()[0]; }
  gpsLng(): number { return this.gps()[1]; }
  onGpsChange(lat: unknown, lng: unknown) { this.onFieldChange('order_gps', [Number(lat) || 0, Number(lng) || 0]); }
}
