import { CommonModule } from '@angular/common';
import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { Component, ElementRef, computed, input, output, signal, viewChild } from '@angular/core';
import { UiControlSize } from '../ui-input/ui-input.component';

/** Semantic weight of an option, rendered as a small indicator rather than as
 * coloured text so the label stays readable in both themes. */
export type UiDropdownTone = 'neutral' | 'success' | 'danger' | 'warning';

export interface UiDropdownOption {
  /** The value written back to the model — never the display label. */
  value: string;
  label: string;
  tone?: UiDropdownTone;
  disabled?: boolean;
}

let nextDropdownId = 0;

const DROPDOWN_POSITIONS: ConnectedPosition[] = [
  { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
  { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 4 },
  { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom', offsetY: -4 }
];

/**
 * A select whose open list is ours, not the browser's.
 *
 * A native `<select>` can be styled while closed, but its option popup is drawn
 * by the OS and ignores the RMS+ palette entirely. This renders the list into
 * the CDK overlay container instead, so it escapes any clipping or stacking
 * ancestor and follows the same tokens as the rest of the app.
 *
 * Focus stays on the trigger for the whole interaction and the active option is
 * announced through aria-activedescendant, which is why the option elements are
 * never focused themselves.
 */
@Component({
  selector: 'ui-dropdown-select',
  standalone: true,
  imports: [CommonModule, OverlayModule],
  template: `
    <button
      #trigger
      cdkOverlayOrigin
      #origin="cdkOverlayOrigin"
      type="button"
      class="dd-trigger"
      [class.dd-trigger--sm]="size() === 'sm'"
      [class.dd-trigger--invalid]="invalid()"
      [class.is-open]="open()"
      [id]="buttonId() || null"
      [disabled]="disabled()"
      role="combobox"
      aria-haspopup="listbox"
      [attr.aria-expanded]="open()"
      [attr.aria-controls]="open() ? listboxId : null"
      [attr.aria-activedescendant]="open() && activeIndex() >= 0 ? optionId(activeIndex()) : null"
      [attr.aria-label]="ariaLabel() || null"
      [attr.aria-describedby]="ariaDescribedBy() || null"
      [attr.aria-invalid]="invalid()"
      (click)="toggle()"
      (keydown)="onKeydown($event)">
      @if (selected(); as option) {
        <span class="dd-dot" [class]="'dd-dot dd-dot--' + (option.tone || 'neutral')" aria-hidden="true"></span>
        <span class="dd-label">{{ option.label }}</span>
      } @else {
        <span class="dd-label dd-label--placeholder">{{ placeholder() || value() || '' }}</span>
      }
      <i class="bi bi-chevron-down dd-caret" aria-hidden="true"></i>
    </button>

    <ng-template cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="origin"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayPositions]="positions"
      [cdkConnectedOverlayPush]="true"
      [cdkConnectedOverlayViewportMargin]="12"
      [cdkConnectedOverlayMatchWidth]="true"
      [cdkConnectedOverlayHasBackdrop]="true"
      cdkConnectedOverlayBackdropClass="ui-dropdown-backdrop"
      cdkConnectedOverlayPanelClass="ui-dropdown-pane"
      (backdropClick)="close(false)"
      (detach)="close(false)">
      <div class="dd-panel" role="listbox" [id]="listboxId" [attr.aria-label]="ariaLabel() || null">
        @for (option of options(); track option.value; let index = $index) {
          <button
            type="button"
            role="option"
            class="dd-option"
            [id]="optionId(index)"
            [class.is-active]="index === activeIndex()"
            [class.is-selected]="option.value === value()"
            [attr.aria-selected]="option.value === value()"
            [disabled]="!!option.disabled"
            (mousedown)="$event.preventDefault()"
            (mouseenter)="activeIndex.set(index)"
            (click)="select(option)">
            <span class="dd-dot" [class]="'dd-dot dd-dot--' + (option.tone || 'neutral')" aria-hidden="true"></span>
            <span class="dd-label">{{ option.label }}</span>
            @if (option.value === value()) {
              <i class="bi bi-check2 dd-check" aria-hidden="true"></i>
            }
          </button>
        }
      </div>
    </ng-template>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .dd-trigger { position: relative; display: flex; align-items: center; gap: 7px; width: 100%; min-height: var(--control-height); box-sizing: border-box; padding: 0 30px 0 var(--panel-padding-compact); border: 1px solid var(--input-border); border-radius: var(--radius-md); background: var(--input-bg); box-shadow: inset 0 1px 0 var(--input-highlight); color: var(--text-primary); cursor: pointer; font: inherit; text-align: left; transition: border-color var(--transition-fast), box-shadow var(--transition-fast); }
    .dd-trigger:hover:not(:disabled) { border-color: var(--border-strong); }
    .dd-trigger:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .dd-trigger.is-open { border-color: var(--border-focus); }
    .dd-trigger:disabled { cursor: not-allowed; background: var(--surface-muted); opacity: .68; }
    .dd-trigger--invalid { border-color: var(--state-danger-border); }
    .dd-trigger--invalid:focus-visible { box-shadow: var(--focus-ring-danger); }
    .dd-trigger--sm { min-height: 32px; gap: 6px; padding: 0 26px 0 8px; border-radius: var(--radius-sm); font-size: .85rem; }

    .dd-caret { position: absolute; right: 10px; color: var(--text-muted); font-size: .72rem; pointer-events: none; }
    .dd-label { min-width: 0; flex: 1 1 auto; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .dd-label--placeholder { color: var(--text-muted); }

    .dd-dot { flex: 0 0 auto; width: 8px; height: 8px; border-radius: var(--radius-pill); background: var(--text-muted); }
    .dd-dot--success { background: var(--state-success-fg); }
    .dd-dot--danger { background: var(--state-danger-fg); }
    .dd-dot--warning { background: var(--state-warning-fg); }

    .dd-panel { display: flex; flex-direction: column; min-width: 132px; max-height: min(280px, 60vh); padding: 4px; overflow-y: auto; border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-overlay); box-shadow: var(--shadow-lg); color: var(--text-primary); }
    .dd-option { display: flex; align-items: center; gap: 8px; width: 100%; box-sizing: border-box; padding: 7px 8px; border: 0; border-radius: var(--radius-sm); background: transparent; color: inherit; cursor: pointer; font: inherit; font-size: .85rem; text-align: left; }
    .dd-option.is-active { background: var(--surface-hover); }
    .dd-option.is-selected { background: var(--surface-selected); font-weight: 700; }
    .dd-option:disabled { cursor: not-allowed; color: var(--text-muted); }
    .dd-check { flex: 0 0 auto; color: var(--text-accent); font-size: .78rem; }

    @media (prefers-reduced-motion: reduce) {
      .dd-trigger { transition: none; }
    }
  `]
})
export class UiDropdownSelectComponent {
  readonly buttonId = input('');
  readonly options = input<UiDropdownOption[]>([]);
  readonly value = input<string | null>(null);
  readonly size = input<UiControlSize>('md');
  readonly disabled = input(false);
  readonly invalid = input(false);
  readonly placeholder = input('');
  readonly ariaLabel = input<string | null>(null);
  readonly ariaDescribedBy = input<string | null>(null);
  readonly valueChange = output<string>();

  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');

  readonly listboxId = `ui-dropdown-${nextDropdownId++}`;
  readonly positions = DROPDOWN_POSITIONS;
  readonly open = signal(false);
  readonly activeIndex = signal(-1);

  readonly selected = computed(() => this.options().find(option => option.value === this.value()));

  optionId(index: number): string { return `${this.listboxId}-option-${index}`; }

  toggle() {
    this.open() ? this.close(false) : this.openPanel();
  }

  openPanel() {
    if (this.disabled() || this.open()) return;
    const selectedIndex = this.options().findIndex(option => option.value === this.value());
    this.activeIndex.set(selectedIndex >= 0 ? selectedIndex : this.options().length ? 0 : -1);
    this.open.set(true);
  }

  close(returnFocus: boolean) {
    if (!this.open()) return;
    this.open.set(false);
    this.activeIndex.set(-1);
    if (returnFocus) queueMicrotask(() => this.trigger()?.nativeElement.focus());
  }

  select(option: UiDropdownOption) {
    if (option.disabled) return;
    this.valueChange.emit(option.value);
    this.close(true);
  }

  onKeydown(event: KeyboardEvent) {
    if (this.disabled()) return;

    if (!this.open()) {
      if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) {
        event.preventDefault();
        this.openPanel();
      }
      return;
    }

    const lastIndex = this.options().length - 1;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.activeIndex.set(Math.min(this.activeIndex() + 1, lastIndex));
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.activeIndex.set(Math.max(this.activeIndex() - 1, 0));
        break;
      case 'Home':
        event.preventDefault();
        this.activeIndex.set(lastIndex >= 0 ? 0 : -1);
        break;
      case 'End':
        event.preventDefault();
        this.activeIndex.set(lastIndex);
        break;
      case 'Enter':
      case ' ': {
        event.preventDefault();
        const option = this.options()[this.activeIndex()];
        if (option) this.select(option);
        break;
      }
      case 'Escape':
        event.preventDefault();
        this.close(true);
        break;
      case 'Tab':
        this.close(false);
        break;
    }
  }
}
