import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DeliveryDetails } from '../../../core/models';
import { UiCardComponent, UiFieldComponent, UiInputComponent } from '../../../shared/ui';

@Component({
  selector: 'app-delivery-section',
  standalone: true,
  imports: [CommonModule, UiCardComponent, UiFieldComponent, UiInputComponent],
  template: `
    <ui-card variant="raised" class="card-section">
      <div uiCardHeader class="section-heading"><span class="section-title"><i class="bi bi-geo-alt" aria-hidden="true"></i> Delivery Details</span></div>

      <div class="form-grid">
        <ui-field label="Delivery Phone Number" forId="delivery-phone">
          <ui-input inputId="delivery-phone" type="tel" [value]="delivery.deliveryPhoneNumber || ''" (valueChange)="onFieldChange('deliveryPhoneNumber', $event)"></ui-input>
        </ui-field>
        <ui-field label="Delivery Location URL" forId="delivery-location-url">
          <ui-input inputId="delivery-location-url" type="url" placeholder="https://maps.google.com/..." [value]="delivery.deliveryLocationUrl || ''" (valueChange)="onFieldChange('deliveryLocationUrl', $event)"></ui-input>
        </ui-field>
        <ui-field label="Delivery Fees (SAR)" forId="delivery-fees">
          <ui-input inputId="delivery-fees" type="number" step="0.01" [value]="delivery.deliveryFees || 0" (valueChange)="onFieldChange('deliveryFees', $event)"></ui-input>
        </ui-field>
        <ui-field label="Delivery Address" forId="delivery-address" class="full-width">
          <ui-input inputId="delivery-address" placeholder="Full street address..." [value]="delivery.deliveryAddress || ''" (valueChange)="onFieldChange('deliveryAddress', $event)"></ui-input>
        </ui-field>
        <ui-field label="Delivery Notes" forId="delivery-notes" class="full-width">
          <ui-input inputId="delivery-notes" placeholder="Carrier notes..." [value]="delivery.deliveryNotes || ''" (valueChange)="onFieldChange('deliveryNotes', $event)"></ui-input>
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
  `]
})
export class DeliverySectionComponent {
  @Input() delivery: DeliveryDetails = { deliveryFees: 0 };
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();

  onFieldChange(fieldName: string, value: unknown) {
    this.fieldChange.emit({ fieldName, value });
  }
}
