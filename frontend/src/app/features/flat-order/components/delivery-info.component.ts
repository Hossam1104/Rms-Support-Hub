import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UiFieldComponent, UiInputComponent } from '../../../shared/ui';

@Component({
  selector: 'app-delivery-info',
  standalone: true,
  imports: [CommonModule, UiFieldComponent, UiInputComponent],
  template: `
    <div class="form-panel">
      <div class="form-grid">
        <ui-field label="Delivery date" forId="field-delivery-date" [error]="fieldError('delivery_date')">
          <ui-input inputId="field-delivery-date" type="date" [value]="textValue('delivery_date')" [invalid]="hasError('delivery_date')" (valueChange)="onFieldChange('delivery_date', $event)"></ui-input>
        </ui-field>
        <ui-field label="From time" forId="field-delivery-from-time" [error]="fieldError('delivery_from_time')">
          <ui-input inputId="field-delivery-from-time" type="time" [value]="textValue('delivery_from_time')" [invalid]="hasError('delivery_from_time')" (valueChange)="onFieldChange('delivery_from_time', $event)"></ui-input>
        </ui-field>
        <ui-field label="To time" forId="field-delivery-to-time" [error]="fieldError('delivery_to_time')">
          <ui-input inputId="field-delivery-to-time" type="time" [value]="textValue('delivery_to_time')" [invalid]="hasError('delivery_to_time')" (valueChange)="onFieldChange('delivery_to_time', $event)"></ui-input>
        </ui-field>
        <ui-field label="Fulfillment plant" forId="field-fulfillment-plant" [error]="fieldError('fullfilment_plant')">
          <ui-input inputId="field-fulfillment-plant" [value]="textValue('fullfilment_plant')" placeholder="WH-01" [invalid]="hasError('fullfilment_plant')" (valueChange)="onFieldChange('fullfilment_plant', $event)"></ui-input>
        </ui-field>
        <ui-field label="Shipping address line 2" forId="field-shipping-address-2" [error]="fieldError('shipping_address_2')" class="field-span-12">
          <ui-input inputId="field-shipping-address-2" [value]="textValue('shipping_address_2')" placeholder="Building / landmark" [invalid]="hasError('shipping_address_2')" (valueChange)="onFieldChange('shipping_address_2', $event)"></ui-input>
        </ui-field>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .form-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 16px; }
    ui-field { grid-column: span 3; }
    .field-span-12 { grid-column: 1 / -1; }
    @media (max-width: 900px) { ui-field { grid-column: span 6; } .field-span-12 { grid-column: 1 / -1; } }
    @media (max-width: 620px) { ui-field, .field-span-12 { grid-column: 1 / -1; } }
  `]
})
export class DeliveryInfoComponent {
  @Input() orderData: Record<string, unknown> = {};
  @Input() fieldErrors: Record<string, string[]> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();

  hasError(fieldName: string): boolean { return (this.fieldErrors[fieldName]?.length ?? 0) > 0; }
  fieldError(fieldName: string): string | null { return this.fieldErrors[fieldName]?.join(' ') || null; }
  textValue(fieldName: string): string {
    const value = this.orderData[fieldName];
    return value == null ? '' : String(value);
  }
  onFieldChange(fieldName: string, value: unknown) { this.fieldChange.emit({ fieldName, value }); }
}
