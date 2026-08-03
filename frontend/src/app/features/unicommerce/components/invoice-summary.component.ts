import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UiCardComponent } from '../../../shared/ui';

@Component({
  selector: 'app-invoice-summary',
  standalone: true,
  imports: [CommonModule, UiCardComponent],
  template: `
    <div class="quick-stats-grid">
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Gross Amount</span><span class="stat-value">{{ grossAmount | number:'1.2-2' }} <small>SAR</small></span></div></ui-card>
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Total Discount</span><span class="stat-value text-danger">-{{ totalDiscount | number:'1.2-2' }} <small>SAR</small></span></div></ui-card>
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Total VAT</span><span class="stat-value text-info">+{{ totalVat | number:'1.2-2' }} <small>SAR</small></span></div></ui-card>
      <ui-card variant="raised" class="stat-card highlight"><div class="stat-meta"><span class="stat-label">Net Amount (Total)</span><span class="stat-value text-success">{{ netAmount | number:'1.2-2' }} <small>SAR</small></span></div></ui-card>
      <ui-card variant="raised" class="stat-card"><div class="stat-meta"><span class="stat-label">Calculated Customer Credit</span><span class="stat-value">{{ customerCreditAmount | number:'1.2-2' }} <small>SAR</small></span></div></ui-card>
    </div>
  `,
  styles: [`
    .quick-stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; }
    .stat-card { min-height: 92px; }
    .stat-card.highlight { border-color: var(--state-success-border); }
    .stat-meta { display: flex; flex-direction: column; }
    .stat-label { font-size: .75rem; color: var(--text-muted); font-weight: 650; }
    .stat-value { font-size: 1.15rem; font-weight: 800; color: var(--text-primary); }
    .stat-value small { font-size: .75rem; font-weight: 450; color: var(--text-secondary); }
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
