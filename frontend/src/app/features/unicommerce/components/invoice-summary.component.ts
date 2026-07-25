import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-invoice-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="quick-stats-grid">
      <div class="stat-card glass-card">
        <div class="stat-meta">
          <span class="stat-label">Gross Amount</span>
          <span class="stat-value">{{ grossAmount | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
      <div class="stat-card glass-card">
        <div class="stat-meta">
          <span class="stat-label">Total Discount</span>
          <span class="stat-value text-danger">-{{ totalDiscount | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
      <div class="stat-card glass-card">
        <div class="stat-meta">
          <span class="stat-label">Total VAT</span>
          <span class="stat-value text-info">+{{ totalVat | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
      <div class="stat-card glass-card highlight">
        <div class="stat-meta">
          <span class="stat-label">Net Amount (Total)</span>
          <span class="stat-value text-success">{{ netAmount | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
      <div class="stat-card glass-card">
        <div class="stat-meta">
          <span class="stat-label">Calculated Customer Credit</span>
          <span class="stat-value">{{ customerCreditAmount | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .quick-stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; }
    .stat-card { padding: 16px; display: flex; flex-direction: column; justify-content: center; }
    .stat-card.highlight { border-color: var(--success); }
    .stat-meta { display: flex; flex-direction: column; }
    .stat-label { font-size: 0.75rem; color: var(--text-muted); font-weight: 500; }
    .stat-value { font-size: 1.15rem; font-weight: 700; color: var(--text-primary); }
    .stat-value small { font-size: 0.75rem; font-weight: 400; color: var(--text-secondary); }
    .text-success { color: var(--success); }
    .text-danger { color: var(--danger); }
    .text-info { color: var(--primary); }
  `]
})
export class InvoiceSummaryComponent {
  @Input() grossAmount: number = 0;
  @Input() totalDiscount: number = 0;
  @Input() totalVat: number = 0;
  @Input() netAmount: number = 0;
  @Input() customerCreditAmount: number = 0;
}
