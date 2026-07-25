import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DeliveryDetails } from '../../../core/models';

@Component({
  selector: 'app-delivery-section',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-geo-alt"></i>
        <span>Delivery Details</span>
      </div>

      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">Delivery Phone Number</label>
          <input type="text" class="glass-input" [ngModel]="delivery.deliveryPhoneNumber" (ngModelChange)="onFieldChange('deliveryPhoneNumber', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Delivery Location URL</label>
          <input type="url" class="glass-input" [ngModel]="delivery.deliveryLocationUrl" (ngModelChange)="onFieldChange('deliveryLocationUrl', $event)" placeholder="https://maps.google.com/..." />
        </div>
        <div class="form-group">
          <label class="form-label">Delivery Fees (SAR)</label>
          <input type="number" step="0.01" class="glass-input" [ngModel]="delivery.deliveryFees" (ngModelChange)="onFieldChange('deliveryFees', $event)" />
        </div>
        <div class="form-group full-width">
          <label class="form-label">Delivery Address</label>
          <input type="text" class="glass-input" [ngModel]="delivery.deliveryAddress" (ngModelChange)="onFieldChange('deliveryAddress', $event)" placeholder="Full street address..." />
        </div>
        <div class="form-group full-width">
          <label class="form-label">Delivery Notes</label>
          <input type="text" class="glass-input" [ngModel]="delivery.deliveryNotes" (ngModelChange)="onFieldChange('deliveryNotes', $event)" placeholder="Carrier notes..." />
        </div>
      </div>
    </div>
  `,
  styles: [`
    .card-section { padding: 24px; margin-bottom: 24px; }
    .card-title { display: flex; align-items: center; gap: 10px; font-size: 1.1rem; font-weight: 600; margin-bottom: 20px; color: var(--text-primary); }
    .card-title i { color: var(--primary); }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-label { font-size: 0.85rem; font-weight: 500; color: var(--text-secondary); }
  `]
})
export class DeliverySectionComponent {
  @Input() delivery: DeliveryDetails = { deliveryFees: 0 };
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();

  onFieldChange(fieldName: string, value: unknown) {
    this.fieldChange.emit({ fieldName, value });
  }
}
