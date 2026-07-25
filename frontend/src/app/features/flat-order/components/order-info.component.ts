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
          <select class="glass-input" [ngModel]="orderData['order_status']" (ngModelChange)="onFieldChange('order_status', $event)">
            <option value="1">1 - New</option>
            <option value="2">2 - Confirmed</option>
            <option value="3">3 - Ready</option>
            <option value="4">4 - With Delegate</option>
            <option value="5">5 - Rejected</option>
            <option value="6">6 - Canceled Client</option>
            <option value="7">7 - Canceled Admin</option>
            <option value="8">8 - Processing</option>
            <option value="9">9 - Done</option>
          </select>
        </div>
        <div class="form-group" *ngIf="moduleKey === 'upc_ecommerce'">
          <label class="form-label">Order Payment Status</label>
          <select class="glass-input" [ngModel]="orderData['order_payment_status']" (ngModelChange)="onFieldChange('order_payment_status', $event)">
            <option value="1">1 - Pending</option>
            <option value="2">2 - Paid</option>
            <option value="3">3 - Failed</option>
            <option value="4">4 - Refunded</option>
          </select>
        </div>
        <div class="form-group full-width">
          <label class="form-label">Order Notes</label>
          <input type="text" class="glass-input" [ngModel]="orderData['order_notes']" (ngModelChange)="onFieldChange('order_notes', $event)" placeholder="Special delivery instructions..." />
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
  @Input() orderData: Record<string, any> = {};
  @Input() moduleKey: string = '';
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: any }>();

  onFieldChange(fieldName: string, value: any) {
    this.fieldChange.emit({ fieldName, value });
  }
}
