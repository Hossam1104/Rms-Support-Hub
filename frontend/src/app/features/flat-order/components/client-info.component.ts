import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UiButtonComponent, UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption, UiToolbarComponent } from '../../../shared/ui';
import { normalizeLocalPhone } from '../../../core/utils/phone.util';

@Component({
  selector: 'app-client-info',
  standalone: true,
  imports: [CommonModule, UiButtonComponent, UiFieldComponent, UiInputComponent, UiSelectComponent, UiToolbarComponent],
  template: `
    <div class="form-panel">
      <ui-toolbar [compact]="true" [wrap]="true" role="search" ariaLabel="Consumer lookup">
        <div uiToolbarStart class="lookup-copy">
          <i class="bi bi-search" aria-hidden="true"></i>
          <span>Prefill from consumer lookup</span>
        </div>
        <div uiToolbarCenter class="lookup-input">
          <ui-input inputId="consumer-lookup-phone" type="tel" ariaLabel="Consumer phone number for lookup" [value]="lookupPhone" placeholder="Mobile number" autocomplete="tel" (valueChange)="onLookupPhoneChange($event)"></ui-input>
        </div>
        <div uiToolbarEnd>
          <ui-button variant="secondary" size="sm" icon="bi bi-person-check" [disabled]="!lookupPhone.trim()" (pressed)="onLookup()">Lookup consumer</ui-button>
        </div>
      </ui-toolbar>

      <div class="form-grid">
        <ui-field label="Country code" forId="field-client-country-code">
          <ui-input inputId="field-client-country-code" type="tel" [value]="textValue('client_country_code')" placeholder="966" (valueChange)="onFieldChange('client_country_code', $event)"></ui-input>
        </ui-field>

        <ui-field #phoneField label="Phone" forId="field-client-phone" [required]="true" hint="Local number only — the country code is the separate field." [error]="fieldError('client_phone')">
          <ui-input inputId="field-client-phone" type="tel" [value]="textValue('client_phone')" autocomplete="tel" [invalid]="hasError('client_phone')" [ariaDescribedBy]="phoneField.describedBy()" (valueChange)="onFieldChange('client_phone', $event)"></ui-input>
        </ui-field>

        <ng-container *ngIf="moduleKey === 'ghc_ecommerce'">
          <ui-field label="Order country code" forId="field-order-country-code">
            <ui-input inputId="field-order-country-code" type="tel" [value]="textValue('order_country_code')" placeholder="966" (valueChange)="onFieldChange('order_country_code', $event)"></ui-input>
          </ui-field>
          <ui-field label="Order phone" forId="field-order-phone" hint="Local number only — sent separately from the order country code.">
            <ui-input inputId="field-order-phone" type="tel" [value]="textValue('order_phone')" (valueChange)="onFieldChange('order_phone', $event)"></ui-input>
          </ui-field>
        </ng-container>

        <ui-field #firstNameField label="First name" forId="field-client-first-name" [required]="true" [error]="fieldError('client_first_name')">
          <ui-input inputId="field-client-first-name" autocomplete="given-name" [value]="textValue('client_first_name')" [invalid]="hasError('client_first_name')" [ariaDescribedBy]="firstNameField.describedBy()" (valueChange)="onFieldChange('client_first_name', $event)"></ui-input>
        </ui-field>

        <ui-field label="Middle name" forId="field-client-middle-name">
          <ui-input inputId="field-client-middle-name" autocomplete="additional-name" [value]="textValue('client_middle_name')" (valueChange)="onFieldChange('client_middle_name', $event)"></ui-input>
        </ui-field>

        <ui-field label="Last name" forId="field-client-last-name" [required]="true">
          <ui-input inputId="field-client-last-name" autocomplete="family-name" [value]="textValue('client_last_name')" (valueChange)="onFieldChange('client_last_name', $event)"></ui-input>
        </ui-field>

        <ui-field label="Email" forId="field-client-email">
          <ui-input inputId="field-client-email" type="email" autocomplete="email" [value]="textValue('client_email')" (valueChange)="onFieldChange('client_email', $event)"></ui-input>
        </ui-field>

        <ui-field label="Birthdate" forId="field-client-birthdate">
          <ui-input inputId="field-client-birthdate" type="date" [value]="birthdateForInput()" (valueChange)="onFieldChange('client_birthdate', $event)"></ui-input>
        </ui-field>

        <ui-field label="Gender" forId="field-client-gender">
          <ui-select selectId="field-client-gender" [options]="genderOptions" [value]="textValue('client_gender') || null" placeholder="Select gender" (valueChange)="onFieldChange('client_gender', $event)"></ui-select>
        </ui-field>

        <ui-field #addressField label="Address" forId="field-order-address" [required]="true" [error]="fieldError('order_address')" class="field-span-8">
          <ui-input inputId="field-order-address" autocomplete="street-address" [value]="textValue('order_address')" [invalid]="hasError('order_address')" [ariaDescribedBy]="addressField.describedBy()" (valueChange)="onFieldChange('order_address', $event)"></ui-input>
        </ui-field>

        <ui-field label="Address code" forId="field-address-code" class="field-span-4">
          <ui-input inputId="field-address-code" [value]="textValue('address_code')" (valueChange)="onFieldChange('address_code', $event)"></ui-input>
        </ui-field>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .form-panel { min-width: 0; }
    .lookup-copy { display: inline-flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: .78rem; font-weight: 750; }
    .lookup-copy i { color: var(--accent); }
    .lookup-input { flex: 1 1 230px; min-width: 180px; }
    .form-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: var(--form-gap); margin-top: var(--panel-gap); }
    ui-field { grid-column: span 3; }
    ui-field:nth-child(2), ui-field:nth-child(3) { grid-column: span 4; }
    .field-span-8 { grid-column: span 8; }
    .field-span-4 { grid-column: span 4; }
    @media (max-width: 900px) { ui-field, ui-field:nth-child(2), ui-field:nth-child(3), .field-span-8, .field-span-4 { grid-column: span 6; } }
    @media (max-width: 620px) { ui-field, ui-field:nth-child(2), ui-field:nth-child(3), .field-span-8, .field-span-4 { grid-column: 1 / -1; } }
  `]
})
export class ClientInfoComponent {
  @Input() moduleKey = '';
  @Input() orderData: Record<string, unknown> = {};
  @Input() fieldErrors: Record<string, string[]> = {};
  @Output() fieldChange = new EventEmitter<{ fieldName: string, value: unknown }>();
  @Output() lookupConsumer = new EventEmitter<string>();

  readonly genderOptions: UiSelectOption[] = [
    { value: 'Male', label: 'Male' },
    { value: 'Female', label: 'Female' }
  ];
  lookupPhone = '';

  hasError(fieldName: string): boolean { return (this.fieldErrors[fieldName]?.length ?? 0) > 0; }
  fieldError(fieldName: string): string | null { return this.fieldErrors[fieldName]?.join(' ') || null; }
  /** `client_phone` is stripped of a leading Saudi country code on the way
   * into the draft (typed or pasted) so the field settles on the local number
   * the separate country-code field already accounts for. Every other field
   * is passed through untouched. */
  onFieldChange(fieldName: string, value: unknown) {
    const applied = fieldName === 'client_phone' ? normalizeLocalPhone(value) : value;
    this.fieldChange.emit({ fieldName, value: applied });
  }

  textValue(fieldName: string): string {
    const value = this.orderData[fieldName];
    return value == null ? '' : String(value);
  }

  /** Stored ISO datetimes are shortened for the date input and are sent back
   * through the existing draft patch path unchanged as the selected date. */
  birthdateForInput(): string {
    const raw = this.orderData['client_birthdate'];
    return typeof raw === 'string' && raw.length >= 10 ? raw.slice(0, 10) : '';
  }

  onLookup() {
    if (this.lookupPhone.trim()) this.lookupConsumer.emit(this.lookupPhone.trim());
  }

  onLookupPhoneChange(value: string | number | null) {
    this.lookupPhone = value == null ? '' : String(value);
  }
}
