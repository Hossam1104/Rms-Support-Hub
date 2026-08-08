import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';

let nextFieldId = 0;

@Component({
  selector: 'ui-field',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="ui-field" [class.is-disabled]="disabled()" [class.has-error]="!!error()">
      <label class="ui-field__label" [attr.for]="fieldId()">
        <span>{{ label() }}</span>
        <span class="ui-field__status" *ngIf="labelStatus()">{{ labelStatus() }}</span>
        <span class="ui-field__required" *ngIf="required()" aria-hidden="true">*</span>
        <span class="sr-only" *ngIf="required()">required</span>
      </label>
      <div class="ui-field__control" [attr.data-field-id]="fieldId()"><ng-content></ng-content></div>
      <p class="ui-field__hint" *ngIf="hint() && !error()" [id]="hintId">{{ hint() }}</p>
      <p class="ui-field__error" *ngIf="error()" [id]="errorId" role="alert">
        <i class="bi bi-exclamation-circle" aria-hidden="true"></i>{{ error() }}
      </p>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-field { display: flex; flex-direction: column; gap: var(--field-gap); min-width: 0; }
    .ui-field__label { display: inline-flex; align-items: baseline; gap: 4px; color: var(--text-secondary); font-size: .8rem; font-weight: 700; line-height: 1.25; }
    .ui-field__status { color: var(--text-accent); border: 1px solid var(--border-focus); border-radius: var(--radius-pill); padding: 0 5px; font-size: .62rem; font-weight: 800; letter-spacing: .04em; }
    .ui-field__required { color: var(--state-danger-fg); }
    .ui-field__control { min-width: 0; }
    .ui-field__hint, .ui-field__error { margin: 0; font-size: var(--text-xs); line-height: 1.35; }
    .ui-field__hint { color: var(--text-muted); }
    .ui-field__error { display: flex; align-items: flex-start; gap: 5px; color: var(--state-danger-fg); }
    .ui-field.is-disabled { opacity: .62; }
  `]
})
export class UiFieldComponent {
  readonly label = input.required<string>();
  readonly labelStatus = input('');
  readonly forId = input<string | null>(null);
  readonly required = input(false);
  readonly hint = input('');
  readonly error = input<string | null>(null);
  readonly disabled = input(false);

  private readonly generatedId = `ui-field-${nextFieldId++}`;
  readonly fieldId = computed(() => this.forId() || this.generatedId);
  readonly hintId = `${this.generatedId}-hint`;
  readonly errorId = `${this.generatedId}-error`;
  readonly describedBy = computed(() => this.error() ? this.errorId : this.hint() ? this.hintId : null);
}
