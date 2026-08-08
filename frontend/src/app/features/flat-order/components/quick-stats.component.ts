import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatTileComponent, UiCardComponent } from '../../../shared/ui';
import { TotalsSummary } from '../../../core/models';

/**
 * U4 (UI_Rework_Plan.md D8): the summary reads the server's typed
 * TotalsSummary (GET calculate-totals, backed by the authoritative
 * TotalsCalculator the validator itself uses) -- there is no client-side
 * money math left in the builder. `null` totals mean "not loaded yet" and
 * are never rendered as fabricated zeros; once a real summary exists it
 * stays on screen during refreshes (loading only adds a subtle indicator).
 */
@Component({
  selector: 'app-quick-stats',
  standalone: true,
  imports: [CommonModule, StatTileComponent, UiCardComponent],
  template: `
    <div class="quick-stats-grid" *ngIf="totals">
      <app-stat-tile label="Total Amount" [value]="totals?.totalOrderAmount ?? 0" [decimals]="2" icon="bi-cart-check" variant="brand"></app-stat-tile>
      <app-stat-tile label="Paid Amount" [value]="totals?.totalPaidAmount ?? 0" [decimals]="2" icon="bi-wallet2" variant="success"></app-stat-tile>
      <app-stat-tile label="Remaining Balance" [value]="totals?.remainingBalance ?? 0" [decimals]="2" [variant]="(totals?.remainingBalance ?? 0) > 0 ? 'muted' : 'success'" icon="bi-calculator"></app-stat-tile>
    </div>

    <ui-card variant="raised" class="totals-breakdown" *ngIf="totals; else totalsUnavailable">
      <div class="breakdown-row">
        <span class="breakdown-item"><i class="bi bi-box-seam"></i> Products <strong>{{ productCount }}</strong></span>
        <span class="breakdown-item"><i class="bi bi-stack"></i> Quantity <strong>{{ totalQuantity }}</strong></span>
        <span class="breakdown-item">Subtotal <strong>{{ totals.totalProductAmount | number:'1.2-2' }}</strong></span>
        <span class="breakdown-item">Discount <strong>{{ totals.orderDiscount | number:'1.2-2' }}</strong></span>
        <span class="breakdown-item">VAT <strong>{{ totals.totalProductVat | number:'1.2-2' }}</strong></span>
        <span class="breakdown-item">Delivery <strong>{{ totals.deliveryCost | number:'1.2-2' }}</strong></span>
        <span class="breakdown-refresh" *ngIf="loading" title="Refreshing totals from the server"><i class="bi bi-arrow-repeat spin"></i></span>
      </div>
      <p class="breakdown-error" *ngIf="error">{{ error }} Showing the last known values.</p>
    </ui-card>

    <ng-template #totalsUnavailable>
      <ui-card variant="raised" class="totals-breakdown">
        <div class="breakdown-row breakdown-placeholder">
          <span *ngIf="loading">Calculating totals…</span>
          <span *ngIf="!loading && error" class="breakdown-error">{{ error }}</span>
          <span *ngIf="!loading && !error">Totals have not been calculated yet.</span>
        </div>
      </ui-card>
    </ng-template>
  `,
  styles: [`
    .quick-stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: var(--card-gap); margin-bottom: var(--panel-gap); }
    .totals-breakdown { padding: var(--panel-padding-compact) var(--panel-padding); margin-bottom: var(--section-gap); }
    .breakdown-row { display: flex; flex-wrap: wrap; gap: 8px 24px; align-items: center; font-size: 0.85rem; color: var(--text-secondary); }
    .breakdown-item strong { color: var(--text-primary); margin-left: 4px; }
    .breakdown-placeholder { color: var(--text-muted); }
    .breakdown-refresh { margin-left: auto; color: var(--text-muted); }
    .breakdown-error { font-size: 0.78rem; color: var(--state-warning-fg); margin: 8px 0 0; }
    .spin { animation: spin 1s infinite linear; display: inline-block; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
  `]
})
export class QuickStatsComponent {
  @Input() totals: TotalsSummary | null = null;
  @Input() productCount: number = 0;
  @Input() totalQuantity: number = 0;
  @Input() loading: boolean = false;
  @Input() error: string | null = null;
}
