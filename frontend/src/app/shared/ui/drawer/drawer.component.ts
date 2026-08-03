import { Component, Input, Output, EventEmitter, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';

/**
 * Right-side overlay drawer. Uses CDK a11y's cdkTrapFocus (focus trap +
 * auto-capture) rather than the full low-level Overlay.create() API --
 * consistent with this app's existing fixed-position dialog convention
 * (see cancel-dialog.component.ts) while still getting real keyboard-trap
 * behavior, which is the part that actually matters for accessibility.
 */
@Component({
  selector: 'app-drawer',
  standalone: true,
  imports: [CommonModule, A11yModule],
  template: `
    <div class="drawer-backdrop" (click)="close.emit()"></div>
    <div class="drawer-panel" cdkTrapFocus cdkTrapFocusAutoCapture role="dialog" aria-modal="true">
      <div class="drawer-header">
        <h3>{{ title }}</h3>
        <button type="button" class="drawer-close" (click)="close.emit()" aria-label="Close">
          <i class="bi bi-x-lg"></i>
        </button>
      </div>
      <div class="drawer-body">
        <ng-content></ng-content>
      </div>
    </div>
  `,
  styles: [`
    :host { position: fixed; inset: 0; z-index: 2000; }
    .drawer-backdrop {
      position: absolute; inset: 0;
      background: var(--backdrop);
      backdrop-filter: blur(2px);
      animation: fadeIn var(--d) var(--ease-out);
    }
    .drawer-panel {
      position: absolute;
      top: 0; right: 0; bottom: 0;
      width: min(720px, 100vw);
      background: var(--surface-panel);
      border-left: 1px solid var(--border-subtle);
      box-shadow: var(--shadow-lg);
      display: flex;
      flex-direction: column;
      animation: drawerSlide var(--d-slow) var(--ease-spring);
    }
    .drawer-header {
      display: flex; justify-content: space-between; align-items: center;
      padding: 20px 24px;
      border-bottom: 1px solid var(--divider);
      flex-shrink: 0;
    }
    .drawer-header h3 { margin: 0; font-size: 1.1rem; color: var(--text-primary); }
    .drawer-close { background: none; border: none; color: var(--text-muted); font-size: 1.1rem; cursor: pointer; }
    .drawer-close:hover { color: var(--text-primary); }
    .drawer-body { flex: 1; overflow-y: auto; padding: 24px; }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    @keyframes drawerSlide { from { transform: translateX(100%); } to { transform: translateX(0); } }
    @media (max-width: 900px) {
      .drawer-panel { width: 100vw; }
    }
  `]
})
export class DrawerComponent {
  @Input() title: string = '';
  @Output() close = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  onEscape() {
    this.close.emit();
  }
}
