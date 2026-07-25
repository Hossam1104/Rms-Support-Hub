import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-quick-stats',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="quick-stats-grid">
      <div class="stat-card glass-card">
        <div class="stat-icon icon-primary"><i class="bi bi-cart-check"></i></div>
        <div class="stat-meta">
          <span class="stat-label">Total Amount</span>
          <span class="stat-value">{{ totalAmount | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
      <div class="stat-card glass-card">
        <div class="stat-icon icon-success"><i class="bi bi-wallet2"></i></div>
        <div class="stat-meta">
          <span class="stat-label">Paid Amount</span>
          <span class="stat-value">{{ paidAmount | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
      <div class="stat-card glass-card" [class.negative]="remainingBalance > 0">
        <div class="stat-icon icon-warning"><i class="bi bi-calculator"></i></div>
        <div class="stat-meta">
          <span class="stat-label">Remaining Balance</span>
          <span class="stat-value">{{ remainingBalance | number:'1.2-2' }} <small>SAR</small></span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .quick-stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 20px; margin-bottom: 24px; }
    .stat-card { display: flex; align-items: center; gap: 16px; padding: 20px; }
    .stat-card.negative .stat-value { color: var(--warning); }
    .stat-icon { width: 48px; height: 48px; border-radius: var(--radius-md); display: flex; align-items: center; justify-content: center; font-size: 1.4rem; }
    .icon-primary { background: rgba(99, 102, 241, 0.15); color: var(--primary); }
    .icon-success { background: var(--success-bg); color: var(--success); }
    .icon-warning { background: var(--warning-bg); color: var(--warning); }
    .stat-meta { display: flex; flex-direction: column; }
    .stat-label { font-size: 0.8rem; color: var(--text-muted); }
    .stat-value { font-size: 1.3rem; font-weight: 700; color: var(--text-primary); }
    .stat-value small { font-size: 0.8rem; font-weight: 400; color: var(--text-secondary); }
  `]
})
export class QuickStatsComponent {
  @Input() totalAmount: number = 0;
  @Input() paidAmount: number = 0;
  @Input() remainingBalance: number = 0;
}
