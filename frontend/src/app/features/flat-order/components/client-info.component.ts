import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

/**
 * Rebound to the corrected R1 schema (FlatOrderPayloadBuilder.cs) --
 * client_country_code/client_phone/client_first_name/client_middle_name/
 * client_last_name/client_email/client_birthdate/client_gender,
 * order_address/address_code. The pre-R1 invented client_name/client_code/
 * client_mobile/client_national_id/shipping_address/district_name/
 * city_name inputs are gone -- FlatOrderPayloadBuilder has never read them
 * (see remediation_plan.md B1/B5, R10).
 */
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
          <label class="form-label">Country Code</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_country_code']" (ngModelChange)="onFieldChange('client_country_code', $event)" placeholder="966" />
        </div>
        <div class="form-group">
          <label class="form-label">Phone *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_phone']" (ngModelChange)="onFieldChange('client_phone', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">First Name *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_first_name']" (ngModelChange)="onFieldChange('client_first_name', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">Middle Name</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_middle_name']" (ngModelChange)="onFieldChange('client_middle_name', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Last Name *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['client_last_name']" (ngModelChange)="onFieldChange('client_last_name', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">Email</label>
          <input type="email" class="glass-input" [ngModel]="orderData['client_email']" (ngModelChange)="onFieldChange('client_email', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Birthdate</label>
          <input type="date" class="glass-input" [ngModel]="birthdateForInput()" (ngModelChange)="onFieldChange('client_birthdate', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Gender</label>
          <select class="glass-input" [ngModel]="orderData['client_gender']" (ngModelChange)="onFieldChange('client_gender', $event)">
            <option value="Male">Male</option>
            <option value="Female">Female</option>
          </select>
        </div>
        <div class="form-group full-width">
          <label class="form-label">Address *</label>
          <input type="text" class="glass-input" [ngModel]="orderData['order_address']" (ngModelChange)="onFieldChange('order_address', $event)" required />
        </div>
        <div class="form-group">
          <label class="form-label">Address Code</label>
          <input type="text" class="glass-input" [ngModel]="orderData['address_code']" (ngModelChange)="onFieldChange('address_code', $event)" />
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

  /** client_birthdate is stored/sent as an ISO datetime
   * (FlatOrderPayloadBuilder.FormatBirthdate); <input type=date> needs just
   * the yyyy-MM-dd prefix. */
  birthdateForInput(): string {
    const raw = this.orderData['client_birthdate'];
    return typeof raw === 'string' && raw.length >= 10 ? raw.slice(0, 10) : '';
  }

  onLookup() {
    if (this.lookupPhone.trim()) {
      this.lookupConsumer.emit(this.lookupPhone.trim());
    }
  }
}
