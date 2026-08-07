import { CommonModule } from '@angular/common';
import { Component, computed, effect, forwardRef, input, output, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'app-prompt-textarea',
  standalone: true,
  imports: [CommonModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PromptTextareaComponent), multi: true }],
  template: `
    <textarea
      class="prompt-textarea"
      [id]="textareaId() || null"
      [name]="name() || null"
      [rows]="rows()"
      [value]="currentValue()"
      [placeholder]="placeholder()"
      [disabled]="effectiveDisabled()"
      [attr.aria-label]="ariaLabel() || null"
      [attr.aria-describedby]="ariaDescribedBy() || null"
      (input)="onInput($event)"
      (blur)="onBlur()">
    </textarea>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .prompt-textarea { display: block; width: 100%; min-width: 0; min-height: 108px; padding: 12px 13px; resize: vertical; background: var(--input-bg); border: 1px solid var(--input-border); border-radius: var(--radius-md); box-shadow: inset 0 1px 0 var(--input-highlight); color: var(--text-primary); font: inherit; line-height: var(--leading-normal); transition: border-color var(--transition-fast), box-shadow var(--transition-fast); }
    .prompt-textarea:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .prompt-textarea:disabled { cursor: not-allowed; background: var(--surface-muted); opacity: .68; }
    .prompt-textarea::placeholder { color: var(--text-muted); }
  `]
})
export class PromptTextareaComponent implements ControlValueAccessor {
  readonly textareaId = input('');
  readonly name = input('');
  readonly placeholder = input('');
  readonly rows = input(5);
  readonly value = input<string | null>('');
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly ariaDescribedBy = input<string | null>(null);
  readonly valueChange = output<string>();

  readonly currentValue = signal('');
  private readonly formDisabled = signal(false);
  readonly effectiveDisabled = computed(() => this.disabled() || this.formDisabled());
  private readonly formValueWritten = signal(false);
  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    effect(() => {
      if (!this.formValueWritten()) this.currentValue.set(this.value() ?? '');
    });
  }

  writeValue(value: unknown): void {
    this.formValueWritten.set(true);
    this.currentValue.set(typeof value === 'string' ? value : '');
  }
  registerOnChange(fn: (value: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(isDisabled: boolean): void { this.formDisabled.set(isDisabled); }

  onInput(event: Event) {
    const value = (event.target as HTMLTextAreaElement).value;
    this.currentValue.set(value);
    this.onChange(value);
    this.valueChange.emit(value);
  }

  onBlur() { this.onTouched(); }
}
