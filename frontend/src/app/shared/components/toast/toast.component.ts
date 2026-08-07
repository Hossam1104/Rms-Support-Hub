import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastMessage, ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container" role="status" aria-live="polite" aria-atomic="false" aria-label="Notifications">
      <article
        *ngFor="let toast of toastService.toasts(); trackBy: trackToast"
        class="toast-item"
        [class.toast-item--success]="toast.type === 'success'"
        [class.toast-item--error]="toast.type === 'error'"
        [class.toast-item--warning]="toast.type === 'warning'"
        [class.toast-item--info]="toast.type === 'info'"
        tabindex="0"
        [attr.aria-label]="toastLabel(toast)"
        (mouseenter)="pause(toast.id)"
        (mouseleave)="resume(toast.id)"
        (focusin)="pause(toast.id)"
        (focusout)="resume(toast.id)">
        <i class="toast-icon bi" [class.bi-check-circle-fill]="toast.type === 'success'" [class.bi-x-circle-fill]="toast.type === 'error'" [class.bi-exclamation-triangle-fill]="toast.type === 'warning'" [class.bi-info-circle-fill]="toast.type === 'info'" aria-hidden="true"></i>
        <span class="toast-message">{{ toast.message }}</span>
        <span class="toast-count" *ngIf="toast.count > 1" aria-hidden="true">×{{ toast.count }}</span>
        <button type="button" class="toast-close" [attr.aria-label]="'Dismiss ' + toast.message" (click)="toastService.remove(toast.id)">&times;</button>
      </article>
    </div>
  `,
  styles: [`
    :host { display: contents; }
    .toast-container { position: fixed; right: 20px; bottom: 20px; z-index: var(--z-toast); display: flex; width: min(390px, calc(100vw - 32px)); flex-direction: column; gap: 10px; pointer-events: none; }
    .toast-item { display: flex; align-items: flex-start; gap: 10px; width: 100%; padding: 13px 14px; border: 1px solid var(--border-subtle); border-left: 3px solid var(--border-strong); border-radius: var(--radius-md); background: var(--surface-overlay); box-shadow: var(--shadow-lg); color: var(--text-primary); pointer-events: auto; animation: slideInRight var(--d) var(--ease-out); }
    .toast-item:focus-visible { outline: none; box-shadow: var(--focus-ring), var(--shadow-lg); }
    .toast-item--success { border-left-color: var(--state-success-fg); }
    .toast-item--error { border-left-color: var(--state-danger-fg); }
    .toast-item--warning { border-left-color: var(--state-warning-fg); }
    .toast-item--info { border-left-color: var(--state-info-fg); }
    .toast-icon { flex: 0 0 auto; padding-top: 1px; color: var(--text-secondary); }
    .toast-item--success .toast-icon { color: var(--state-success-fg); }
    .toast-item--error .toast-icon { color: var(--state-danger-fg); }
    .toast-item--warning .toast-icon { color: var(--state-warning-fg); }
    .toast-item--info .toast-icon { color: var(--state-info-fg); }
    .toast-message { flex: 1 1 auto; min-width: 0; font-size: .86rem; line-height: 1.4; overflow-wrap: anywhere; }
    .toast-count { flex: 0 0 auto; padding: 1px 6px; border-radius: var(--radius-pill); background: var(--surface-selected); color: var(--text-accent); font-size: .74rem; font-weight: 800; }
    .toast-close { display: grid; width: 24px; height: 24px; flex: 0 0 24px; place-items: center; padding: 0; border: 0; border-radius: var(--radius-sm); background: transparent; color: var(--text-muted); cursor: pointer; font-size: 1.1rem; line-height: 1; }
    .toast-close:hover { background: var(--surface-hover); color: var(--text-primary); }
    .toast-close:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    @media (max-width: 640px) { .toast-container { right: 12px; bottom: 12px; width: calc(100vw - 24px); } }
  `]
})
export class ToastComponent {
  readonly toastService = inject(ToastService);

  trackToast(_index: number, toast: ToastMessage): string { return toast.id; }
  toastLabel(toast: ToastMessage): string { return `${toast.type}: ${toast.message}${toast.count > 1 ? `, repeated ${toast.count} times` : ''}`; }
  pause(id: string) { this.toastService.pause(id); }
  resume(id: string) { this.toastService.resume(id); }
}
