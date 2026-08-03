import { Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OverlayModule } from '@angular/cdk/overlay';
import { CdkVirtualScrollViewport, ScrollingModule } from '@angular/cdk/scrolling';
import { BranchOption } from '../../../core/models';

let nextSelectId = 0;

/**
 * Accessible picker for values with a code and a human label. The input owns
 * focus while its CDK overlay is open, making keyboard control work with
 * virtualized options through aria-activedescendant.
 */
@Component({
  selector: 'app-searchable-select',
  standalone: true,
  imports: [CommonModule, OverlayModule, ScrollingModule],
  template: `
    <div class="searchable-select" [class.is-open]="open" [class.has-error]="!!error">
      <div class="select-control" cdkOverlayOrigin #origin="cdkOverlayOrigin">
        <input #trigger [id]="inputId || null" type="text" role="combobox" autocomplete="off"
          [attr.aria-label]="label" [attr.aria-expanded]="open" [attr.aria-controls]="listboxId"
          [attr.aria-activedescendant]="open && activeIndex >= 0 ? optionId(activeIndex) : null"
          [attr.aria-describedby]="ariaDescribedBy"
          [attr.aria-invalid]="!!error" [placeholder]="placeholder" [disabled]="disabled" [value]="displayValue"
          (focus)="openPanel()" (click)="openPanel()" (input)="onInput($any($event.target).value)" (keydown)="onKeydown($event)">
        <button *ngIf="value && !disabled" type="button" class="clear-button" [attr.aria-label]="'Clear ' + label"
          (mousedown)="$event.preventDefault()" (click)="clear()"><i class="bi bi-x"></i></button>
        <i class="bi caret" [class.bi-chevron-up]="open" [class.bi-chevron-down]="!open" aria-hidden="true"></i>
      </div>

      <ng-template cdkConnectedOverlay [cdkConnectedOverlayOrigin]="origin" [cdkConnectedOverlayOpen]="open"
        [cdkConnectedOverlayHasBackdrop]="true" cdkConnectedOverlayBackdropClass="searchable-select-backdrop"
        (backdropClick)="closePanel(true)" (detach)="onOverlayDetach()">
        <section class="select-overlay" [attr.aria-label]="label + ' options'">
          <div class="select-state" *ngIf="loading"><i class="bi bi-arrow-repeat spin"></i> Loading branches?</div>
          <div class="select-state error" *ngIf="!loading && error"><span>{{ error }}</span><button type="button" (click)="refresh.emit()">Try again</button></div>
          <div class="select-state" *ngIf="!loading && !error && filteredOptions.length === 0">{{ options.length ? 'No matching branches.' : 'No branches are available.' }}</div>
          <cdk-virtual-scroll-viewport *ngIf="!loading && !error && filteredOptions.length" #viewport class="select-list"
            [itemSize]="40" [minBufferPx]="200" [maxBufferPx]="400" [id]="listboxId" role="listbox" [attr.aria-label]="label + ' options'">
            <button *cdkVirtualFor="let option of filteredOptions; let index = index" type="button" role="option" class="select-option"
              [id]="optionId(index)" [class.active]="index === activeIndex" [class.selected]="option.code === value"
              [attr.aria-selected]="option.code === value" (mouseenter)="setActive(index)" (mousedown)="$event.preventDefault()" (click)="select(option)">
              {{ format(option) }}
            </button>
          </cdk-virtual-scroll-viewport>
        </section>
      </ng-template>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .searchable-select { min-width: 0; }
    .select-control { position: relative; display: flex; align-items: center; }
    input { width: 100%; min-width: 0; box-sizing: border-box; background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-sm); color: var(--text-primary); padding: 8px 56px 8px 10px; font: inherit; }
    input:focus { outline: 2px solid var(--focus-ring, var(--primary)); outline-offset: 2px; }
    input:disabled { cursor: not-allowed; opacity: .6; }
    .is-open input { border-color: var(--primary); }
    .has-error input { border-color: var(--danger); }
    .caret { position: absolute; right: 11px; color: var(--text-muted); pointer-events: none; }
    .clear-button { position: absolute; right: 29px; display: grid; place-items: center; width: 20px; height: 20px; padding: 0; border: 0; border-radius: var(--radius-pill); background: transparent; color: var(--text-muted); cursor: pointer; }
    .clear-button:hover { background: var(--glass-hover-bg); color: var(--text-primary); }
    .select-overlay { width: min(420px, calc(100vw - 32px)); margin-top: 4px; overflow: hidden; background: var(--bg-secondary); border: 1px solid var(--glass-border); border-radius: var(--radius-md); box-shadow: var(--shadow-lg); color: var(--text-primary); }
    .select-list { height: min(240px, 45vh); }
    .select-option { display: block; width: 100%; height: 40px; padding: 0 12px; overflow: hidden; border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; text-align: left; text-overflow: ellipsis; white-space: nowrap; }
    .select-option:hover, .select-option.active { background: var(--glass-hover-bg); }
    .select-option.selected { color: var(--primary); font-weight: 700; }
    .select-state { display: flex; align-items: center; gap: 8px; min-height: 68px; padding: 12px; color: var(--text-secondary); font-size: .86rem; }
    .select-state.error { justify-content: space-between; color: var(--danger); }
    .select-state button { border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; font-weight: 700; text-decoration: underline; }
    .spin { animation: searchableSelectSpin var(--transition-slow, .7s) linear infinite; }
    @keyframes searchableSelectSpin { to { transform: rotate(360deg); } }
  `]
})
export class SearchableSelectComponent {
  @Input() label = 'Select an option';
  @Input() placeholder = 'Search by name or code';
  @Input() options: BranchOption[] = [];
  @Input() value: string | null = null;
  @Input() loading = false;
  @Input() error: string | null = null;
  @Input() disabled = false;
  @Input() inputId: string | null = null;
  @Input() ariaDescribedBy: string | null = null;

  @Output() valueChange = new EventEmitter<string | null>();
  @Output() refresh = new EventEmitter<void>();

  @ViewChild('trigger') private trigger?: ElementRef<HTMLInputElement>;
  @ViewChild(CdkVirtualScrollViewport) private viewport?: CdkVirtualScrollViewport;

  readonly listboxId = `searchable-select-${nextSelectId++}`;
  open = false;
  query = '';
  activeIndex = -1;

  get selected(): BranchOption | undefined {
    return this.options.find(option => option.code === this.value);
  }

  get displayValue(): string {
    return this.open ? this.query : this.selected ? this.format(this.selected) : this.value || '';
  }

  get filteredOptions(): BranchOption[] {
    const query = this.query.trim().toLocaleLowerCase();
    if (!query) return this.options;
    return this.options.filter(option => option.name.toLocaleLowerCase().includes(query) || option.code.toLocaleLowerCase().includes(query));
  }

  format(option: BranchOption): string { return `${option.name} (${option.code})`; }
  optionId(index: number): string { return `${this.listboxId}-option-${index}`; }

  openPanel() {
    if (this.disabled || this.open) return;
    this.open = true;
    this.query = '';
    const selectedIndex = this.filteredOptions.findIndex(option => option.code === this.value);
    this.setActive(selectedIndex >= 0 ? selectedIndex : this.filteredOptions.length ? 0 : -1);
  }

  closePanel(returnFocus: boolean) {
    this.open = false;
    this.query = '';
    this.activeIndex = -1;
    if (returnFocus) this.returnFocus();
  }

  onOverlayDetach() {
    if (!this.open) return;
    this.open = false;
    this.query = '';
    this.activeIndex = -1;
  }

  onInput(value: string) {
    if (!this.open) this.openPanel();
    this.query = value;
    this.setActive(this.filteredOptions.length ? 0 : -1);
  }

  onKeydown(event: KeyboardEvent) {
    if (this.disabled) return;
    if (!this.open && ['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) {
      event.preventDefault();
      this.openPanel();
      return;
    }
    if (!this.open) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.setActive(Math.min(this.activeIndex + 1, this.filteredOptions.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.setActive(Math.max(this.activeIndex - 1, 0));
    } else if (event.key === 'Enter' && this.activeIndex >= 0) {
      event.preventDefault();
      const option = this.filteredOptions[this.activeIndex];
      if (option) this.select(option);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.closePanel(true);
    }
  }

  setActive(index: number) {
    this.activeIndex = index;
    if (index >= 0) queueMicrotask(() => this.viewport?.scrollToIndex(index, 'smooth'));
  }

  select(option: BranchOption) {
    this.valueChange.emit(option.code);
    this.closePanel(true);
  }

  clear() {
    this.valueChange.emit(null);
    this.closePanel(true);
  }

  private returnFocus() { queueMicrotask(() => this.trigger?.nativeElement.focus()); }
}
