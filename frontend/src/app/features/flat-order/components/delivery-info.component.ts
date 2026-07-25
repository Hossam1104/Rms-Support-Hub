import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-delivery-info',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-truck"></i>
        <span>Delivery Schedule & Fulfillment</span>
      </div>

      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">Delivery Date</label>
          <input type="date" class="glass-input" [ngModel]="orderData['delivery_date']" (ngModelChange)="onFieldChange('delivery_date', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Delivery From Time</label>
          <input type="time" class="glass-input" [ngModel]="orderData['delivery_from_time']" (ngModelChange)="onFieldChange('delivery_from_time', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Delivery To Time</label>
          <input type="time" class="glass-input" [ngModel]="orderData['delivery_to_time']" (ngModelChange)="onFieldChange('delivery_to_time', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Fulfillment Plant</label>
          <input type="text" class="glass-input" [ngModel]="orderData['fullfilment_plant']" (ngModelChange)="onFieldChange('fullfilment_plant', $event)" placeholder="e.g. WH-01" />
        </div>
        <div class="form-group full-width">
          <label class="form-label">Shipping Address Line 2</label>
          <input type="text" class="glass-input" [ngModel]="orderData['shipping_address_2']" (ngModelChange)="onFieldChange('shipping_address_2', $event)" placeholder="Building / Landmark" />
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
export class DeliveryInfoComponent {
  @Input() orderData: Record<string, any> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: any }>();

  onFieldChange(fieldName: string, value: any) {
    this.fieldChange.emit({ fieldName, value });
  }
}
