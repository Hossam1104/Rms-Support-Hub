import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { OrderRequestListItem, OrderRequestListResponse, OrderRequestDetailResponse } from '../../core/models';
import { SearchFormComponent, OrderSearchFilters } from './components/search-form.component';
import { ResultsGridComponent } from './components/results-grid.component';
import { OrderDetailsModalComponent } from './components/order-details-modal.component';

/**
 * upc_ecommerce is hardcoded here (matching the pre-existing behaviour of
 * the deleted ValidationController, which was UPC-only) rather than reading
 * the active module from the route -- see remediation_plan.md B21/R5.
 * Capabilities.OrderRequests is only true for upc_ecommerce today.
 */
const MODULE_KEY = 'upc_ecommerce';

@Component({
  selector: 'app-order-validation',
  standalone: true,
  imports: [CommonModule, SearchFormComponent, ResultsGridComponent, OrderDetailsModalComponent],
  template: `
    <div class="validation-container">
      <app-search-form (search)="onSearch($event)"></app-search-form>

      <app-results-grid
        [results]="searchResults()"
        (viewDetails)="onViewDetails($event)">
      </app-results-grid>
    </div>

    <app-order-details-modal
      *ngIf="selectedOrderNumber()"
      [orderNumber]="selectedOrderNumber() || ''"
      [details]="selectedOrderDetails()"
      (close)="selectedOrderNumber.set(null)">
    </app-order-details-modal>
  `,
  styles: [`
    .validation-container { display: flex; flex-direction: column; }
  `]
})
export class OrderValidationComponent {
  private api = inject(ApiService);
  moduleService = inject(ModuleService);
  private toast = inject(ToastService);

  searchResults = signal<OrderRequestListItem[]>([]);
  selectedOrderNumber = signal<string | null>(null);
  selectedOrderDetails = signal<OrderRequestDetailResponse | null>(null);

  onSearch(filters: OrderSearchFilters) {
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<OrderRequestListResponse>(`modules/${MODULE_KEY}/order-requests`, {
      orderNumber: filters.orderNumber || undefined,
      phone: filters.phone || undefined,
      branchCode: filters.branchCode || undefined,
      status: filters.status ?? undefined,
      pageSize: 100,
      envKey
    }).subscribe({
      next: res => {
        this.searchResults.set(res.items);
        this.toast.showSuccess(`Found ${res.total} orders in database.`);
      },
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      error: () => this.searchResults.set([])
    });
  }

  onViewDetails(orderNumber: string) {
    this.selectedOrderNumber.set(orderNumber);
    this.selectedOrderDetails.set(null);

    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<OrderRequestDetailResponse>(`modules/${MODULE_KEY}/order-requests/by-order/${orderNumber}`, { envKey }).subscribe({
      next: res => this.selectedOrderDetails.set(res),
      error: () => this.selectedOrderNumber.set(null)
    });
  }
}
