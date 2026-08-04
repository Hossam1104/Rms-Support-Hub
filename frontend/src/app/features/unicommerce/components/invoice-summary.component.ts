import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RiyalComponent, UiCardComponent } from '../../../shared/ui';

@Component({
  selector: 'app-invoice-summary',
  standalone: true,
  imports: [CommonModule, RiyalComponent, UiCardComponent],
  template: `
    <div class="quick-stats-grid">
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Gross Amount</span><span class="stat-value"><app-riyal [size]=".85"></app-riyal>{{ grossAmount | number:'1.2-2' }}</span></div></ui-card>
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Total Discount</span><span class="stat-value text-danger">-<app-riyal [size]=".85"></app-riyal>{{ totalDiscount | number:'1.2-2' }}</span></div></ui-card>
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Total VAT</span><span class="stat-value text-info">+<app-riyal [size]=".85"></app-riyal>{{ totalVat | number:'1.2-2' }}</span></div></ui-card>
      <ui-card variant="raised" class="stat-card highlight"><div class="stat-meta"><span class="stat-label">Net Amount (Total)</span><span class="stat-value text-success"><app-riyal [size]=".85"></app-riyal>{{ netAmount | number:'1.2-2' }}</span></div></ui-card>
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Calculated Customer Credit</span><span class="stat-value"><app-riyal [size]=".85"></app-riyal>{{ customerCreditAmount | number:'1.2-2' }}</span></div></ui-card>
    </div>
  `,
  styles: [`
    .quick-stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; }
    .stat-card { min-height: 92px; }
    .stat-card.highlight { border-color: var(--state-success-border); }
    .stat-meta { display: flex; flex-direction: column; }
    .stat-label { font-size: .75rem; color: var(--text-muted); font-weight: 650; }
    .stat-value { display: inline-flex; align-items: center; gap: 3px; font-size: 1.15rem; font-weight: 800; color: var(--text-primary); }
    .text-success { color: var(--state-success-fg); }
    .text-danger { color: var(--state-danger-fg); }
    .text-info { color: var(--state-info-fg); }
  `]
})
export class InvoiceSummaryComponent {
  @Input() grossAmount = 0;
  @Input() totalDiscount = 0;
  @Input() totalVat = 0;
  @Input() netAmount = 0;
  @Input() customerCreditAmount = 0;
}
