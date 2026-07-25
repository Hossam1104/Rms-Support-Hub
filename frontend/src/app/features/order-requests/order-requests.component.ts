import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { FilterBarComponent } from './components/filter-bar.component';
import { OrderCardComponent } from './components/order-card.component';
import { CancelDialogComponent } from './components/cancel-dialog.component';

@Component({
  selector: 'app-order-requests',
  standalone: true,
  imports: [CommonModule, FilterBarComponent, OrderCardComponent, CancelDialogComponent],
  template: `
    <div class="order-requests-container">
      <app-filter-bar
        [environmentKeys]="environmentKeys()"
        (filterChange)="onFilterChange($event)">
      </app-filter-bar>

      <div class="orders-list" *ngIf="filteredHistory().length > 0; else emptyState">
        @for (entry of filteredHistory(); track entry.id) {
          <app-order-card
            [entry]="entry"
            (resend)="onResendOrder($event)"
            (openCancelModal)="selectedEntryForCancel.set($event)">
          </app-order-card>
        }
      </div>

      <ng-template #emptyState>
        <div class="empty-placeholder glass-card">
          <i class="bi bi-clock-history"></i>
          <h3>No Sent Orders Found</h3>
          <p>Orders sent in this module will appear here automatically with full payload history.</p>
        </div>
      </ng-template>
    </div>

    <app-cancel-dialog
      *ngIf="selectedEntryForCancel()"
      [orderCode]="selectedEntryForCancel()?.orderCode || ''"
      (close)="selectedEntryForCancel.set(null)"
      (confirmCancel)="onConfirmCancel($event)">
    </app-cancel-dialog>
  `,
  styles: [`
    .order-requests-container { display: flex; flex-direction: column; }
    .empty-placeholder { text-align: center; padding: 60px 20px; color: var(--text-muted); margin-top: 20px; }
    .empty-placeholder i { font-size: 3rem; margin-bottom: 12px; display: block; color: var(--text-secondary); }
    .empty-placeholder h3 { font-size: 1.2rem; color: var(--text-primary); margin-bottom: 6px; }
  `]
})
export class OrderRequestsComponent implements OnInit {
  private api = inject(ApiService);
  moduleService = inject(ModuleService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);

  moduleKey = signal<string>('');
  history = signal<any[]>([]);
  filteredHistory = signal<any[]>([]);
  environmentKeys = signal<string[]>([]);

  selectedEntryForCancel = signal<any>(null);

  ngOnInit() {
    this.route.parent?.paramMap.subscribe(params => {
      const key = params.get('key') || '';
      this.moduleKey.set(key);
      if (key) {
        this.loadHistory();
      }
    });

    const activeMod = this.moduleService.activeModule();
    if (activeMod?.environments) {
      this.environmentKeys.set(activeMod.environments.map(e => e.key));
    }
  }

  loadHistory() {
    const key = this.moduleKey();
    this.api.get<any[]>(`modules/${key}/order-history`).subscribe({
      next: items => {
        this.history.set(items || []);
        this.filteredHistory.set(items || []);
      },
      error: () => this.toast.showError('Failed to load order history.')
    });
  }

  onFilterChange(filter: { query: string, status: string, env: string }) {
    let result = [...this.history()];

    if (filter.query.trim()) {
      const q = filter.query.trim().toLowerCase();
      result = result.filter(item => item.orderCode.toLowerCase().includes(q));
    }

    if (filter.status !== 'all') {
      if (filter.status === 'success') {
        result = result.filter(item => item.responseStatusCode >= 200 && item.responseStatusCode < 300);
      } else if (filter.status === 'failed') {
        result = result.filter(item => item.responseStatusCode < 200 || item.responseStatusCode >= 300);
      } else if (filter.status === 'cancelled') {
        result = result.filter(item => item.isCancelled);
      }
    }

    if (filter.env !== 'all') {
      result = result.filter(item => item.environmentKey === filter.env);
    }

    this.filteredHistory.set(result);
  }

  onResendOrder(entry: any) {
    const key = this.moduleKey();
    const envKey = entry.environmentKey;

    this.api.post<any>(`modules/${key}/send-request`, { environmentKey: envKey, customApiUrl: entry.apiUrl }).subscribe({
      next: res => {
        if (res.success) {
          this.toast.showSuccess(`Order ${entry.orderCode} re-sent successfully!`);
          this.loadHistory();
        } else {
          this.toast.showError(`Re-send failed. Status: ${res.statusCode}`);
        }
      },
      error: () => this.toast.showError('Failed to execute order re-send.')
    });
  }

  onConfirmCancel(reason: string) {
    const entry = this.selectedEntryForCancel();
    if (!entry) return;

    const key = this.moduleKey();
    this.api.post<any>(`modules/${key}/order-history/${entry.id}/cancel`, { orderNumber: entry.orderCode, cancelReason: reason }).subscribe({
      next: res => {
        this.toast.showSuccess(`Order ${entry.orderCode} cancelled successfully.`);
        this.selectedEntryForCancel.set(null);
        this.loadHistory();
      },
      error: () => this.toast.showError('Failed to process order cancellation.')
    });
  }
}
