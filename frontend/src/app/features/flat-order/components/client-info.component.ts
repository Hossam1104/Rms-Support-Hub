import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-client-info',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-person-badge"></i>
        <span>Client & Customer Information</span>
      </div>

      <div class="lookup-bar mb-3">
        <div class="input-group">
          <input type="text" class="glass-input" [(ngModel)]="lookupPhone" placeholder="Enter mobile number to search DB..." />
          <button type="button" class="glass-button" (click)="onLookup()"><i class="bi bi-search"></i> Lookup Consumer</button>
        </div>
      </div>

      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">Client Name *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_name']" (ngModelChange)="onFieldChange('client_name', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">Client Code *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_code']" (ngModelChange)="onFieldChange('client_code', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">Client Mobile *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_mobile']" (ngModelChange)="onFieldChange('client_mobile', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">National ID</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_national_id']" (ngModelChange)="onFieldChange('client_national_id', $event)" />
        </div>
        <div class="form-group full-width">
          <label class="form-label">Shipping Address *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['shipping_address']" (ngModelChange)="onFieldChange('shipping_address', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">District Name</label>
          <input type="text" class="glass-input" [ngModel]="orderData['district_name']" (ngModelChange)="onFieldChange('district_name', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">City Name</label>
          <input type="text" class="glass-input" [ngModel]="orderData['city_name']" (ngModelChange)="onFieldChange('city_name', $event)" />
        </div>
      </div>
    </div>
  `,
  styles: [`
    .card-section { padding: 24px; margin-bottom: 24px; }
    .card-title { display: flex; align-items: center; gap: 10px; font-size: 1.1rem; font-weight: 600; margin-bottom: 20px; color: var(--text-primary); }
    .card-title i { color: var(--primary); }
    .input-group { display: flex; gap: 10px; margin-bottom: 16px; }
    .input-group input { flex: 1; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-label { font-size: 0.85rem; font-weight: 500; color: var(--text-secondary); }
  `]
})
export class ClientInfoComponent {
  @Input() orderData: Record<string, unknown> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();
  @Output() lookupConsumer = new EventEmitter<string>();

  lookupPhone: string = '';

  onFieldChange(fieldName: string, value: unknown) {
    this.fieldChange.emit({ fieldName, value });
  }

  onLookup() {
    if (this.lookupPhone.trim()) {
      this.lookupConsumer.emit(this.lookupPhone.trim());
    }
  }
}
