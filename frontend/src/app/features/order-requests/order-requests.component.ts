import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { OrderRequestListItem, OrderRequestListResponse, OrderRequestCancelResponse } from '../../core/models';
import { FilterBarComponent } from './components/filter-bar.component';
import { OrderCardComponent } from './components/order-card.component';
import { CancelDialogComponent } from './components/cancel-dialog.component';

/**
 * Reads the real OrderRequests table via OrderRequestsController (R5),
 * replacing the pre-R5 OrderHistoryService JSON-file store this component
 * used to call (`.../order-history`, deleted -- see remediation_plan.md
 * B10). The full stat-tile / server-side-filter / route-driven-drawer
 * rebuild is R9's job (remediation_plan.md session table); this session
 * only re-points the existing list+expand UI at the real, typed contract.
 */
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

      <div class="orders-list" *ngIf="filteredItems().length > 0; else emptyState">
        @for (entry of filteredItems(); track entry.id) {
          <app-order-card
            [entry]="entry"
            [moduleKey]="moduleKey()"
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
      [orderCode]="selectedEntryForCancel()?.orderNumber || ''"
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
  items = signal<OrderRequestListItem[]>([]);
  filteredItems = signal<OrderRequestListItem[]>([]);
  environmentKeys = signal<string[]>([]);

  selectedEntryForCancel = signal<OrderRequestListItem | null>(null);

  private currentFilter = { query: '', status: 'all', env: 'all' };

  ngOnInit() {
    this.route.parent?.paramMap.subscribe(params => {
      const key = params.get('key') || '';
      this.moduleKey.set(key);
      if (key) {
        this.loadRequests();
      }
    });

    const activeMod = this.moduleService.activeModule();
    if (activeMod?.environments) {
      this.environmentKeys.set(activeMod.environments.map(e => e.key));
    }
  }

  loadRequests() {
    const key = this.moduleKey();
    const envKey = this.currentFilter.env !== 'all' ? this.currentFilter.env : undefined;
    this.api.get<OrderRequestListResponse>(`modules/${key}/order-requests`, { pageSize: 100, envKey }).subscribe({
      next: res => {
        this.items.set(res.items);
        this.applyFilter();
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast
      // (e.g. 501 for a module without Capabilities.OrderRequests yet).
      error: () => {
        this.items.set([]);
        this.filteredItems.set([]);
      }
    });
  }

  onFilterChange(filter: { query: string, status: string, env: string }) {
    const envChanged = filter.env !== this.currentFilter.env;
    this.currentFilter = filter;

    if (envChanged) {
      // env selects which database/connection the server queries -- it is
      // not a client-side row filter, so it requires a re-fetch.
      this.loadRequests();
      return;
    }

    this.applyFilter();
  }

  private applyFilter() {
    let result = [...this.items()];
    const { query, status } = this.currentFilter;

    if (query.trim()) {
      const q = query.trim().toLowerCase();
      result = result.filter(item => item.orderNumber.toLowerCase().includes(q));
    }

    if (status === 'success') {
      result = result.filter(item => item.isSucceeded === true);
    } else if (status === 'failed') {
      result = result.filter(item => item.isSucceeded === false);
    } else if (status === 'cancelled') {
      result = result.filter(item => item.orderStatus === 6 || item.orderStatus === 7);
    }

    this.filteredItems.set(result);
  }

  onResendOrder(entry: OrderRequestListItem) {
    if (!entry.branchCode) {
      this.toast.showError('This request has no branch on record; use the order builder to resend it manually.');
      return;
    }

    const key = this.moduleKey();
    this.api.post<{ success: boolean, statusCode: number }>(
      `modules/${key}/order-requests/${entry.id}/resend`,
      { branchCode: entry.branchCode }
    ).subscribe({
      next: res => {
        if (res.success) {
          this.toast.showSuccess(`Order ${entry.orderNumber} re-sent successfully!`);
        } else {
          this.toast.showError(`Re-send failed. Status: ${res.statusCode}`);
        }
        this.loadRequests();
      },
      error: () => {}
    });
  }

  onConfirmCancel(reason: string) {
    const entry = this.selectedEntryForCancel();
    if (!entry) return;

    const key = this.moduleKey();
    this.api.post<OrderRequestCancelResponse>(`modules/${key}/order-requests/${entry.id}/cancel`, { reason }).subscribe({
      next: res => {
        this.toast.showSuccess(res.success
          ? `Order ${entry.orderNumber} cancelled successfully.`
          : `Cancellation sent, upstream returned status ${res.statusCode}.`);
        this.selectedEntryForCancel.set(null);
        this.loadRequests();
      },
      error: () => this.selectedEntryForCancel.set(null)
    });
  }
}
