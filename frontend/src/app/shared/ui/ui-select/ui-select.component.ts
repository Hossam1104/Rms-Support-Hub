import { CommonModule } from '@angular/common';
import { Component, computed, effect, forwardRef, input, output, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { UiControlSize } from '../ui-input/ui-input.component';

export interface UiSelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

@Component({
  selector: 'ui-select',
  standalone: true,
  imports: [CommonModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => UiSelectComponent), multi: true }],
  template: `
    <select
      class="ui-select"
      [class.ui-select--sm]="size() === 'sm'"
      [class.ui-select--invalid]="invalid()"
      [class.ui-select--disabled]="effectiveDisabled()"
      [id]="selectId() || null"
      [name]="name() || null"
      [disabled]="effectiveDisabled()"
      [attr.aria-label]="ariaLabel() || null"
      [attr.aria-describedby]="ariaDescribedBy() || null"
      [attr.aria-invalid]="invalid()"
      (change)="onSelect($event)"
      (blur)="onBlur()">
      <option *ngIf="placeholder()" value="" [selected]="currentValue() === ''" disabled>{{ placeholder() }}</option>
      <option *ngFor="let option of options()" [value]="option.value" [selected]="currentValue() === option.value" [disabled]="!!option.disabled">{{ option.label }}</option>
    </select>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-select { width: 100%; min-height: var(--control-height); padding: 0 34px 0 var(--panel-padding-compact); appearance: none; background-color: var(--input-bg); background-image: var(--select-chevron); background-position: right 12px center; background-repeat: no-repeat; background-size: 12px; border: 1px solid var(--input-border); border-radius: var(--radius-md); box-shadow: inset 0 1px 0 var(--input-highlight); color: var(--text-primary); cursor: pointer; font: inherit; transition: border-color var(--transition-fast), box-shadow var(--transition-fast), transform var(--transition-fast); }
    .ui-select:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .ui-select--invalid { border-color: var(--state-danger-border); }
    .ui-select--invalid:focus-visible { box-shadow: var(--focus-ring-danger); }
    .ui-select--disabled { cursor: not-allowed; background-color: var(--surface-muted); opacity: .68; }
    .ui-select--sm { min-height: var(--control-height-compact); padding-left: var(--panel-padding-compact); border-radius: var(--radius-sm); font-size: var(--text-sm); }
  `]
})
export class UiSelectComponent implements ControlValueAccessor {
  readonly selectId = input('');
  readonly name = input('');
  readonly options = input<UiSelectOption[]>([]);
  readonly placeholder = input('');
  readonly value = input<string | null>(null);
  readonly size = input<UiControlSize>('md');
  readonly disabled = input(false);
  readonly invalid = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly ariaDescribedBy = input<string | null>(null);
  readonly valueChange = output<string | null>();

  readonly currentValue = signal<string | null>('');
  private readonly formDisabled = signal(false);
  readonly effectiveDisabled = computed(() => this.disabled() || this.formDisabled());
  private onChange: (value: string | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;
  private readonly formValueWritten = signal(false);

  constructor() {
    effect(() => {
      if (!this.formValueWritten()) this.currentValue.set(this.value());
    });
  }

  writeValue(value: unknown): void {
    this.formValueWritten.set(true);
    this.currentValue.set(value as string | null ?? '');
  }
  registerOnChange(fn: (value: string | null) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.formDisabled.set(isDisabled); }

  onSelect(event: Event) {
    const value = (event.target as HTMLSelectElement).value || null;
    this.currentValue.set(value);
    this.onChange(value);
    this.valueChange.emit(value);
  }

  onBlur() { this.onTouched(); }
}
