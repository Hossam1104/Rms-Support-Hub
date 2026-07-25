import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-search-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="search-card glass-card">
      <div class="card-title">
        <i class="bi bi-search"></i>
        <span>Search Database Orders (UPC)</span>
      </div>

      <div class="form-grid">
        <div class="form-group">
          <label class="form-label">Order Number</label>
          <input type="text" class="glass-input" [(ngModel)]="filters.orderNumber" placeholder="e.g. UPC-998822" />
        </div>
        <div class="form-group">
          <label class="form-label">Customer Mobile</label>
          <input type="text" class="glass-input" [(ngModel)]="filters.phone" placeholder="05xxxxxxxx" />
        </div>
        <div class="form-group">
          <label class="form-label">Branch Code</label>
          <input type="text" class="glass-input" [(ngModel)]="filters.branchCode" placeholder="e.g. 201" />
        </div>
        <div class="form-group">
          <label class="form-label">Order Status</label>
          <select class="glass-input" [(ngModel)]="filters.status">
            <option [ngValue]="null">All Statuses</option>
            <option [ngValue]="1">1 - New</option>
            <option [ngValue]="2">2 - Confirmed</option>
            <option [ngValue]="3">3 - Ready</option>
            <option [ngValue]="4">4 - With Delegate</option>
            <option [ngValue]="5">5 - Rejected</option>
            <option [ngValue]="6">6 - Canceled Client</option>
            <option [ngValue]="7">7 - Canceled Admin</option>
            <option [ngValue]="8">8 - Processing</option>
            <option [ngValue]="9">9 - Done</option>
          </select>
        </div>
      </div>

      <div class="form-actions">
        <button type="button" class="btn-clear glass-input" (click)="resetFilters()">Clear</button>
        <button type="button" class="glass-button" (click)="onSearch()">
          <i class="bi bi-search"></i> Search Database
        </button>
      </div>
    </div>
  `,
  styles: [`
    .search-card { padding: 24px; margin-bottom: 24px; }
    .card-title { display: flex; align-items: center; gap: 10px; font-size: 1.1rem; font-weight: 600; margin-bottom: 20px; color: var(--text-primary); }
    .card-title i { color: var(--primary); }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; margin-bottom: 20px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-label { font-size: 0.85rem; font-weight: 500; color: var(--text-secondary); }
    .form-actions { display: flex; justify-content: flex-end; gap: 12px; }
    .btn-clear { height: 38px; cursor: pointer; color: var(--text-secondary); }
  `]
})
export class SearchFormComponent {
  @Output() search = new EventEmitter<any>();

  filters = {
    orderNumber: '',
    phone: '',
    branchCode: '',
    status: null
  };

  onSearch() {
    this.search.emit(this.filters);
  }

  resetFilters() {
    this.filters = { orderNumber: '', phone: '', branchCode: '', status: null };
    this.onSearch();
  }
}
