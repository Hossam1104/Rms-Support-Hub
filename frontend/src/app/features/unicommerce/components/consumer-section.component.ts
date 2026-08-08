import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Consumer } from '../../../core/models';
import { UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent } from '../../../shared/ui';

@Component({
  selector: 'app-consumer-section',
  standalone: true,
  imports: [CommonModule, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent],
  template: `
    <ui-card variant="raised" class="card-section">
      <div uiCardHeader class="section-heading">
        <span class="section-title"><i class="bi bi-person-circle" aria-hidden="true"></i> Invoice Consumer Details</span>
      </div>

      <div class="lookup-row">
        <ui-field label="Lookup consumer phone" forId="consumer-lookup-phone" class="lookup-field">
          <ui-input inputId="consumer-lookup-phone" type="tel" placeholder="Enter consumer phone number..."
            [value]="lookupPhone" (valueChange)="lookupPhone = $any($event) || ''"></ui-input>
        </ui-field>
        <ui-button variant="secondary" icon="bi-search" (pressed)="onLookup()">Lookup Consumer</ui-button>
      </div>

      <div class="form-grid">
        <ui-field label="First Name" forId="consumer-first-name">
          <ui-input inputId="consumer-first-name" [value]="consumer.firstName || ''" (valueChange)="onFieldChange('firstName', $event)"></ui-input>
        </ui-field>
        <ui-field label="Middle Name" forId="consumer-middle-name">
          <ui-input inputId="consumer-middle-name" [value]="consumer.middleName || ''" (valueChange)="onFieldChange('middleName', $event)"></ui-input>
        </ui-field>
        <ui-field label="Last Name" forId="consumer-last-name">
          <ui-input inputId="consumer-last-name" [value]="consumer.lastName || ''" (valueChange)="onFieldChange('lastName', $event)"></ui-input>
        </ui-field>
        <ui-field label="Consumer Code" forId="consumer-code">
          <ui-input inputId="consumer-code" [value]="consumer.consumerCode || ''" (valueChange)="onFieldChange('consumerCode', $event)"></ui-input>
        </ui-field>
        <ui-field label="Primary Phone" forId="consumer-primary-phone">
          <ui-input inputId="consumer-primary-phone" type="tel" [value]="consumer.primaryPhoneNumber || ''" (valueChange)="onFieldChange('primaryPhoneNumber', $event)"></ui-input>
        </ui-field>
        <ui-field label="Email" forId="consumer-email">
          <ui-input inputId="consumer-email" type="email" [value]="consumer.email || ''" (valueChange)="onFieldChange('email', $event)"></ui-input>
        </ui-field>
        <ui-field label="National ID" forId="consumer-national-id">
          <ui-input inputId="consumer-national-id" [value]="consumer.nationalId || ''" (valueChange)="onFieldChange('nationalId', $event)"></ui-input>
        </ui-field>
        <ui-field label="Nationality" forId="consumer-nationality">
          <ui-input inputId="consumer-nationality" [value]="consumer.nationality || ''" (valueChange)="onFieldChange('nationality', $event)"></ui-input>
        </ui-field>
      </div>
    </ui-card>
  `,
  styles: [`
    .card-section { margin-bottom: var(--section-gap); }
    .section-heading { display: flex; align-items: center; gap: 10px; }
    .section-title { display: inline-flex; align-items: center; gap: 10px; color: var(--text-primary); }
    .section-title i { color: var(--accent); }
    .lookup-row { display: flex; align-items: flex-end; gap: var(--panel-gap); margin-bottom: var(--panel-gap); }
    .lookup-field { flex: 1; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: var(--form-gap); }
    @media (max-width: 560px) { .lookup-row { align-items: stretch; flex-direction: column; } .lookup-row ui-button { align-self: flex-start; } }
  `]
})
export class ConsumerSectionComponent {
  @Input() consumer: Consumer = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();
  @Output() lookupConsumer = new EventEmitter<string>();

  lookupPhone = '';

  onFieldChange(fieldName: string, value: unknown) {
    this.fieldChange.emit({ fieldName, value });
  }

  onLookup() {
    if (this.lookupPhone.trim()) this.lookupConsumer.emit(this.lookupPhone.trim());
  }
}
