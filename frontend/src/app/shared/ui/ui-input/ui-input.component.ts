import { CommonModule } from '@angular/common';
import { Component, computed, effect, forwardRef, input, output, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export type UiInputType = 'text' | 'email' | 'number' | 'tel' | 'url' | 'search' | 'date' | 'time' | 'datetime-local';
export type UiControlSize = 'sm' | 'md';

@Component({
  selector: 'ui-input',
  standalone: true,
  imports: [CommonModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => UiInputComponent), multi: true }],
  template: `
    <div class="ui-input" [class.ui-input--sm]="size() === 'sm'" [class.ui-input--invalid]="invalid()" [class.ui-input--disabled]="effectiveDisabled()">
      <span class="ui-input__prefix"><ng-content select="[uiInputPrefix]"></ng-content></span>
      <input
        [id]="inputId() || null"
        [name]="name() || null"
        [type]="type()"
        [value]="currentValue() ?? ''"
        [placeholder]="placeholder()"
        [attr.step]="step() || null"
        [attr.maxlength]="maxLength() || null"
        [disabled]="effectiveDisabled()"
        [readOnly]="readOnly()"
        [attr.aria-label]="ariaLabel() || null"
        [attr.aria-describedby]="ariaDescribedBy() || null"
        [attr.aria-invalid]="invalid()"
        [attr.autocomplete]="autocomplete() || null"
        (input)="onInput($event)"
        (blur)="onBlur()">
      <span class="ui-input__suffix"><ng-content select="[uiInputSuffix]"></ng-content></span>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-input { display: flex; align-items: center; min-width: 0; min-height: var(--control-height); padding: 0 var(--panel-padding-compact); background: var(--input-bg); border: 1px solid var(--input-border); border-radius: var(--radius-md); box-shadow: inset 0 1px 0 var(--input-highlight); color: var(--text-primary); transition: border-color var(--transition-fast), box-shadow var(--transition-fast), background var(--transition-fast), transform var(--transition-fast); }
    .ui-input:focus-within { border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .ui-input--invalid { border-color: var(--state-danger-border); }
    .ui-input--invalid:focus-within { box-shadow: var(--focus-ring-danger); }
    .ui-input--disabled { cursor: not-allowed; background: var(--surface-muted); opacity: .68; }
    input { width: 100%; min-width: 0; padding: 0; border: 0; outline: 0; background: transparent; color: inherit; font: inherit; }
    input::placeholder { color: var(--text-muted); }
    input:read-only { cursor: default; }
    .ui-input--sm { min-height: var(--control-height-compact); padding-inline: var(--panel-padding-compact); border-radius: var(--radius-sm); font-size: var(--text-sm); }
    .ui-input__prefix, .ui-input__suffix { display: inline-flex; align-items: center; flex: 0 0 auto; color: var(--text-muted); }
    .ui-input__prefix:empty, .ui-input__suffix:empty { display: none; }
    .ui-input__prefix { margin-right: 8px; } .ui-input__suffix { margin-left: 8px; }
  `]
})
export class UiInputComponent implements ControlValueAccessor {
  readonly inputId = input('');
  readonly name = input('');
  readonly type = input<UiInputType>('text');
  readonly placeholder = input('');
  readonly step = input<string | number | null>(null);
  readonly maxLength = input<number | null>(null);
  readonly value = input<string | number | null>('');
  readonly size = input<UiControlSize>('md');
  readonly disabled = input(false);
  readonly readOnly = input(false);
  readonly invalid = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly ariaDescribedBy = input<string | null>(null);
  readonly autocomplete = input<string | null>(null);
  readonly valueChange = output<string | number | null>();

  readonly currentValue = signal<string | number | null>('');
  private readonly formDisabled = signal(false);
  readonly effectiveDisabled = computed(() => this.disabled() || this.formDisabled());
  private onChange: (value: string | number | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;
  private readonly formValueWritten = signal(false);

  constructor() {
    effect(() => {
      if (!this.formValueWritten()) this.currentValue.set(this.value());
    });
  }

  writeValue(value: unknown): void {
    this.formValueWritten.set(true);
    this.currentValue.set(value as string | number | null ?? '');
  }
  registerOnChange(fn: (value: string | number | null) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.formDisabled.set(isDisabled); }

  onInput(event: Event) {
    const raw = (event.target as HTMLInputElement).value;
    const next = this.type() === 'number' ? (raw === '' ? null : Number(raw)) : raw;
    this.currentValue.set(next);
    this.onChange(next);
    this.valueChange.emit(next);
  }

  onBlur() { this.onTouched(); }
}
