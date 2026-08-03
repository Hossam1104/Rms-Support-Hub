import { Component, ElementRef, EventEmitter, HostListener, Input, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EnvironmentDto } from '../../../core/models';

/**
 * U1 (UI_Rework_Plan.md D4): the environment lane was only selectable from
 * the landing page, so a refresh or a deep link into the module shell gave
 * no on-screen indication of whether the operator was on Testing or
 * Production. This badge is mounted globally in the navbar (always visible,
 * not just on the landing card) and doubles as the switcher.
 */
@Component({
  selector: 'app-env-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="env-badge-wrap" *ngIf="environment">
      <button
        type="button"
        class="env-pill"
        [class.env-pill--prod]="isProduction()"
        [class.env-pill--test]="!isProduction()"
        [disabled]="options.length < 2"
        (click)="toggle($event)">
        <i class="bi" [class.bi-exclamation-triangle-fill]="isProduction()" [class.bi-shield-check]="!isProduction()"></i>
        {{ isProduction() ? 'PROD' : 'TEST' }}
        <i class="bi bi-chevron-down chevron" *ngIf="options.length > 1"></i>
      </button>

      <div class="env-menu" *ngIf="open()">
        <button
          type="button"
          class="env-option"
          *ngFor="let opt of options"
          [class.active]="opt.key === environment?.key"
          (click)="pick(opt)">
          <span class="option-dot" [class.dot-prod]="opt.environment === 'Production'"></span>
          {{ opt.key }}
          <span class="option-tag" *ngIf="opt.isDefault">default</span>
        </button>
      </div>
    </div>
  `,
  styles: [`
    .env-badge-wrap { position: relative; }
    .env-pill {
      display: flex; align-items: center; gap: 6px;
      border: none; border-radius: var(--radius-pill);
      padding: 6px 12px; font-size: 0.78rem; font-weight: 700; letter-spacing: .03em;
      color: var(--on-gradient); cursor: pointer;
    }
    .env-pill:disabled { cursor: default; }
    .env-pill--test { background: var(--grad-info); }
    .env-pill--prod { background: var(--grad-danger); }
    .chevron { font-size: 0.65rem; }
    .env-menu {
      position: absolute; top: calc(100% + 6px); right: 0; z-index: 1200;
      min-width: 220px;
      background: var(--surface-panel); border: 1px solid var(--border-subtle);
      border-radius: var(--radius-md); box-shadow: var(--shadow-lg);
      padding: 6px; display: flex; flex-direction: column; gap: 2px;
    }
    .env-option {
      display: flex; align-items: center; gap: 8px;
      background: none; border: none; text-align: left; cursor: pointer;
      padding: 8px 10px; border-radius: var(--radius-sm);
      font-size: 0.85rem; color: var(--text-primary);
    }
    .env-option:hover { background: var(--surface-hover); }
    .env-option.active { font-weight: 700; }
    .option-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--accent); flex-shrink: 0; }
    .option-dot.dot-prod { background: var(--state-danger-fg); }
    .option-tag {
      margin-left: auto; font-size: 0.65rem; text-transform: uppercase;
      color: var(--text-muted); border: 1px solid var(--border-subtle);
      border-radius: var(--radius-pill); padding: 1px 6px;
    }
  `]
})
export class EnvBadgeComponent {
  @Input() environment: EnvironmentDto | null = null;
  @Input() options: EnvironmentDto[] = [];
  @Output() select = new EventEmitter<EnvironmentDto>();

  private host = inject(ElementRef<HTMLElement>);
  open = signal(false);

  isProduction(): boolean {
    return this.environment?.environment === 'Production';
  }

  toggle(event: Event) {
    event.stopPropagation();
    if (this.options.length < 2) return;
    this.open.update(v => !v);
  }

  pick(env: EnvironmentDto) {
    this.open.set(false);
    if (env.key !== this.environment?.key) this.select.emit(env);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.open.set(false);
  }
}
