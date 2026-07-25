import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-consumer-section',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card-section glass-card">
      <div class="card-title">
        <i class="bi bi-person-circle"></i>
        <span>Invoice Consumer Details</span>
      </div>

      <div class="lookup-bar mb-3">
        <div class="input-group">
          <input type="text" class="glass-input" [(ngModel)]="lookupPhone" placeholder="Enter consumer phone number..." />
          <button type="button" class="glass-button" (click)="onLookup()"><i class="bi bi-search"></i> Lookup Consumer</button>
        </div>
      </div>

      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">First Name</label>
          <input type="text" class="glass-input" [ngModel]="consumer.firstName" (ngModelChange)="onFieldChange('firstName', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Middle Name</label>
          <input type="text" class="glass-input" [ngModel]="consumer.middleName" (ngModelChange)="onFieldChange('middleName', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Last Name</label>
          <input type="text" class="glass-input" [ngModel]="consumer.lastName" (ngModelChange)="onFieldChange('lastName', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Consumer Code</label>
          <input type="text" class="glass-input" [ngModel]="consumer.consumerCode" (ngModelChange)="onFieldChange('consumerCode', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Primary Phone</label>
          <input type="text" class="glass-input" [ngModel]="consumer.primaryPhoneNumber" (ngModelChange)="onFieldChange('primaryPhoneNumber', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Email</label>
          <input type="email" class="glass-input" [ngModel]="consumer.email" (ngModelChange)="onFieldChange('email', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">National ID</label>
          <input type="text" class="glass-input" [ngModel]="consumer.nationalId" (ngModelChange)="onFieldChange('nationalId', $event)" />
        </div>
        <div class="form-group">
          <label class="form-label">Nationality</label>
          <input type="text" class="glass-input" [ngModel]="consumer.nationality" (ngModelChange)="onFieldChange('nationality', $event)" />
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
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-label { font-size: 0.85rem; font-weight: 500; color: var(--text-secondary); }
  `]
})
export class ConsumerSectionComponent {
  @Input() consumer: any = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: any }>();
  @Output() lookupConsumer = new EventEmitter<string>();

  lookupPhone: string = '';

  onFieldChange(fieldName: string, value: any) {
    this.fieldChange.emit({ fieldName, value });
  }

  onLookup() {
    if (this.lookupPhone.trim()) {
      this.lookupConsumer.emit(this.lookupPhone.trim());
    }
  }
}
