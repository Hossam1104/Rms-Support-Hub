import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-order-fields',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-file-earmark-spreadsheet"></i>
        <span>Invoice Order Headers & Payments</span>
      </div>

      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">Reference Number *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['reference_number']" (ngModelChange)="onFieldChange('reference_number', $event)" placeholder="e.g. REF-1002" required />
        </div>
        <div class="form-group">
          <label class="form-label">Online Order Number *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['online_order_number']" (ngModelChange)="onFieldChange('online_order_number', $event)" placeholder="e.g. ONL-9988" required />
        </div>
        <div class="form-group">
          <label class="form-label">Customer Carrier Name *</label>
          <select class="glass-input" [ngModel]="orderData['customer_name']" (ngModelChange)="onFieldChange('customer_name', $event)">
            <option value="AMAZON">AMAZON</option>
            <option value="ARAMEX">ARAMEX</option>
            <option value="NAQEL">NAQEL</option>
            <option value="SMSA">SMSA</option>
            <option value="TABBY">TABBY</option>
            <option value="TAMARA">TAMARA</option>
            <option value="TRENDYOL">TRENDYOL</option>
            <option value="REDBOX">REDBOX</option>
            <option value="OWNFLEET">OWNFLEET</option>
          </select>
        </div>
        <div class="form-group">
          <label class="form-label">Order Creation Date</label>
          <input type="datetime-local" class="glass-input" [ngModel]="orderData['order_creation_date']" (ngModelChange)="onFieldChange('order_creation_date', $event)" />
        </div>
        <div class="form-group checkbox-group full-width">
          <label class="checkbox-label">
            <input type="checkbox" [ngModel]="orderData['is_return']" (ngModelChange)="onFieldChange('is_return', $event)" />
            <span>Is Return Invoice?</span>
          </label>
        </div>
        <div class="form-group full-width" *ngIf="orderData['is_return']">
          <label class="form-label">Parent Reference Number *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['parent_reference_number']" (ngModelChange)="onFieldChange('parent_reference_number', $event)" placeholder="Original invoice ReferenceNumber..." required />
        </div>
        <div class="form-group">
          <label class="form-label">Paid Online Amount (SAR)</label>
          <input type="number" step="0.01" class="glass-input" [ngModel]="orderData['paid_online_amount']" (ngModelChange)="onFieldChange('paid_online_amount', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Paid With Points Amount (SAR)</label>
          <input type="number" step="0.01" class="glass-input" [ngModel]="orderData['paid_with_points_amount']" (ngModelChange)="onFieldChange('paid_with_points_amount', $event)" />
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
    .checkbox-group { flex-direction: row; align-items: center; }
    .checkbox-label { display: flex; align-items: center; gap: 8px; font-size: 0.95rem; font-weight: 600; cursor: pointer; color: var(--text-primary); }
    .form-label { font-size: 0.85rem; font-weight: 500; color: var(--text-secondary); }
  `]
})
export class OrderFieldsComponent {
  @Input() orderData: Record<string, any> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: any }>();

  onFieldChange(fieldName: string, value: any) {
    this.fieldChange.emit({ fieldName, value });
  }
}
