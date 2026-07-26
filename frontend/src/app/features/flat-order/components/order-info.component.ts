import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-order-info',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-receipt"></i>
        <span>Order Header Information</span>
      </div>
      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">Branch Code *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['branch_code']" (ngModelChange)="onFieldChange('branch_code', $event)" placeholder="e.g. 101" required />
        </div>
        <div class="form-group">
          <label class="form-label">Order Code *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['order_code']" (ngModelChange)="onFieldChange('order_code', $event)" placeholder="e.g. ORD-998822" required />
        </div>
        <div class="form-group">
          <label class="form-label">Parent Order Code</label>
          <input type="text" class="glass-input" [ngModel]="orderData['parent_order_code']" (ngModelChange)="onFieldChange('parent_order_code', $event)" placeholder="Optional" />
        </div>
        <div class="form-group">
          <label class="form-label">Delivery Cost (SAR)</label>
          <input type="number" step="0.01" class="glass-input" [ngModel]="orderData['order_delivery_cost']" (ngModelChange)="onFieldChange('order_delivery_cost', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Order Status</label>
          <input type="text" class="glass-input" [ngModel]="orderData['order_status']" (ngModelChange)="onFieldChange('order_status', $event)" placeholder="new" />
        </div>
        <div class="form-group full-width">
          <label class="form-label">Order Notes</label>
          <input type="text" class="glass-input" [ngModel]="orderData['order_notes']" (ngModelChange)="onFieldChange('order_notes', $event)" placeholder="Special delivery instructions..." />
        </div>
        <div class="form-group">
          <label class="form-label">GPS Latitude</label>
          <input type="number" step="any" class="glass-input" [ngModel]="gpsLat()" (ngModelChange)="onGpsChange($event, gpsLng())" placeholder="21.5433" />
        </div>
        <div class="form-group">
          <label class="form-label">GPS Longitude</label>
          <input type="number" step="any" class="glass-input" [ngModel]="gpsLng()" (ngModelChange)="onGpsChange(gpsLat(), $event)" placeholder="39.1728" />
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
export class OrderInfoComponent {
  @Input() orderData: Record<string, unknown> = {};
  @Input() moduleKey: string = '';
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();

  onFieldChange(fieldName: string, value: unknown) {
    this.fieldChange.emit({ fieldName, value });
  }

  /** order_gps is a [lat, lng] pair (see FlatOrderPayloadBuilder.GetGps) --
   * defaults to the reference payload's default coordinates server-side
   * when absent, so these two inputs only need to emit a value once the
   * operator actually edits one of them. */
  private gps(): number[] {
    const raw = this.orderData['order_gps'];
    return Array.isArray(raw) && raw.length === 2 ? raw as number[] : [21.779006345949554, 39.08578576461103];
  }
  gpsLat(): number { return this.gps()[0]; }
  gpsLng(): number { return this.gps()[1]; }

  onGpsChange(lat: unknown, lng: unknown) {
    this.onFieldChange('order_gps', [Number(lat) || 0, Number(lng) || 0]);
  }
}
