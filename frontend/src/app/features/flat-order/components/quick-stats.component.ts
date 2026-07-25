import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatTileComponent } from '../../../shared/ui';

/** Restyled onto the R8 token system (remediation_plan.md B24): now backed
 * by shared/ui's stat-tile (gradient icon + count-up), same @Input()
 * contract as before so the parent (flat-order.component.ts) needs no
 * changes. */
@Component({
  selector: 'app-quick-stats',
  standalone: true,
  imports: [CommonModule, StatTileComponent],
  template: `
    <div class="quick-stats-grid">
      <app-stat-tile label="Total Amount" [value]="totalAmount" [decimals]="2" icon="bi-cart-check" variant="brand"></app-stat-tile>
      <app-stat-tile label="Paid Amount" [value]="paidAmount" [decimals]="2" icon="bi-wallet2" variant="success"></app-stat-tile>
      <app-stat-tile label="Remaining Balance" [value]="remainingBalance" [decimals]="2" [variant]="remainingBalance > 0 ? 'muted' : 'success'" icon="bi-calculator"></app-stat-tile>
    </div>
  `,
  styles: [`
    .quick-stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 20px; margin-bottom: 24px; }
  `]
})
export class QuickStatsComponent {
  @Input() totalAmount: number = 0;
  @Input() paidAmount: number = 0;
  @Input() remainingBalance: number = 0;
}
