import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { ModuleService } from '../../core/services/module.service';
import { ToastService } from '../../core/services/toast.service';
import { SearchFormComponent } from './components/search-form.component';
import { ResultsGridComponent } from './components/results-grid.component';
import { OrderDetailsModalComponent } from './components/order-details-modal.component';

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

  searchResults = signal<any[]>([]);
  selectedOrderNumber = signal<string | null>(null);
  selectedOrderDetails = signal<any>(null);

  onSearch(filters: any) {
    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.post<any>('modules/upc_ecommerce/validation/search', filters, { envKey }).subscribe({
      next: res => {
        if (res.success) {
          this.searchResults.set(res.results || []);
          this.toast.showSuccess(`Found ${res.count} orders in database.`);
        }
      },
      error: err => this.toast.showError(err.error?.message || 'Database search failed.')
    });
  }

  onViewDetails(orderNumber: string) {
    this.selectedOrderNumber.set(orderNumber);
    this.selectedOrderDetails.set(null);

    const envKey = this.moduleService.activeEnvironment()?.key;
    this.api.get<any>(`modules/upc_ecommerce/validation/order/${orderNumber}`, { envKey }).subscribe({
      next: res => {
        if (res.success) {
          this.selectedOrderDetails.set(res.data);
        }
      },
      error: () => this.toast.showError(`Failed to load details for order ${orderNumber}.`)
    });
  }
}
