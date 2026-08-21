import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiError } from '../../core/models';
import { ProductionUnlockService } from '../../core/services/production-unlock.service';

@Component({
  selector: 'app-production-unlock-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="unlock-backdrop" (click)="close()"></div>
    <section class="unlock-dialog" role="dialog" aria-modal="true" aria-labelledby="production-unlock-title">
      <div class="unlock-header">
        <i class="bi bi-shield-lock-fill" aria-hidden="true"></i>
        <div>
          <p class="unlock-eyebrow">Production protection</p>
          <h2 id="production-unlock-title">Unlock {{ request()?.moduleKey }}</h2>
        </div>
      </div>

      <p class="unlock-copy">
        This unlock applies only to {{ request()?.environmentKey }} for this browser session.
        Order Requests remains available without unlocking.
      </p>

      <form (ngSubmit)="submit()" autocomplete="off">
        <label class="unlock-label" for="production-unlock-password">Owner-configured Production unlock</label>
        <input
          id="production-unlock-password"
          class="unlock-input"
          type="password"
          name="productionUnlock"
          autocomplete="off"
          [(ngModel)]="password"
          [disabled]="submitting()"
          aria-describedby="production-unlock-help" />
        <p id="production-unlock-help" class="unlock-help">The secret is verified by the server and is never saved in the browser.</p>

        <p *ngIf="errorMessage()" class="unlock-error" role="alert">{{ errorMessage() }}</p>

        <div class="unlock-actions">
          <button type="button" class="unlock-secondary" (click)="close()" [disabled]="submitting()">Cancel</button>
          <button type="submit" class="unlock-primary" [disabled]="submitting() || !password.trim()">
            {{ submitting() ? 'Verifying…' : 'Unlock Production' }}
          </button>
        </div>
      </form>
    </section>
  `,
  styles: [`
    :host { position: fixed; inset: 0; z-index: 2200; }
    .unlock-backdrop { position: absolute; inset: 0; background: var(--backdrop); backdrop-filter: blur(3px); }
    .unlock-dialog { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); width: min(470px, calc(100vw - 32px)); padding: 26px; border: 1px solid var(--border-strong); border-radius: var(--radius-lg); background: var(--surface-panel); box-shadow: var(--shadow-lg); }
    .unlock-header { display: flex; align-items: flex-start; gap: 12px; }
    .unlock-header > i { color: var(--state-danger-fg); font-size: 1.45rem; }
    .unlock-eyebrow { margin: 0 0 3px; color: var(--text-muted); font-size: var(--text-xs); font-weight: 750; letter-spacing: .08em; text-transform: uppercase; }
    h2 { margin: 0; color: var(--text-primary); font-size: 1.15rem; }
    .unlock-copy { margin: 18px 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: 1.55; }
    .unlock-label { display: block; margin-bottom: 6px; color: var(--text-secondary); font-size: var(--text-sm); font-weight: 700; }
    .unlock-input { box-sizing: border-box; width: 100%; min-height: var(--control-height); padding: 0 11px; border: 1px solid var(--input-border); border-radius: var(--radius-md); background: var(--input-bg); color: var(--text-primary); font: inherit; }
    .unlock-input:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .unlock-help { margin: 7px 0 0; color: var(--text-muted); font-size: var(--text-xs); }
    .unlock-error { margin: 14px 0 0; color: var(--state-danger-fg); font-size: var(--text-sm); }
    .unlock-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 24px; }
    .unlock-secondary, .unlock-primary { min-height: var(--control-height); padding: 0 16px; border-radius: var(--radius-md); font: inherit; font-weight: 750; cursor: pointer; }
    .unlock-secondary { border: 1px solid var(--border-strong); background: var(--surface-interactive); color: var(--text-primary); }
    .unlock-primary { border: 1px solid var(--state-danger-border); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    button:disabled { cursor: not-allowed; opacity: .55; }
  `]
})
export class ProductionUnlockDialogComponent {
  private readonly router = inject(Router);
  readonly unlock = inject(ProductionUnlockService);

  readonly request = this.unlock.dialog;
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  password = '';

  close(): void {
    // The backdrop and Cancel button share this method. Keeping the dialog
    // open while the request is in flight prevents a late successful response
    // from installing an unlock context after the user believes they
    // cancelled the operation.
    if (this.submitting()) return;
    this.password = '';
    this.errorMessage.set(null);
    this.unlock.close();
  }

  submit(): void {
    const request = this.request();
    const password = this.password;
    if (!request || !password.trim() || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set(null);
    this.unlock.unlock(request, password).subscribe({
      next: () => {
        this.submitting.set(false);
        this.password = '';
        this.unlock.close();
        if (request.destination !== 'order-requests') {
          this.router.navigate(['/tools/online-orders/modules', request.moduleKey, request.destination]);
        }
      },
      error: (error: ApiError) => {
        this.submitting.set(false);
        this.password = '';
        this.errorMessage.set(this.unlock.safeError(error));
      }
    });
  }
}
