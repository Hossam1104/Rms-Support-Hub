import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="filter-bar glass-card">
      <div class="filter-group flex-grow">
        <label class="filter-label">Search Order Code</label>
        <div class="input-with-icon">
          <i class="bi bi-search"></i>
          <input type="text" class="glass-input" [(ngModel)]="searchQuery" (ngModelChange)="onFilterChange()" placeholder="Filter by order code..." />
        </div>
      </div>

      <div class="filter-group">
        <label class="filter-label">Status</label>
        <select class="glass-input" [(ngModel)]="statusFilter" (ngModelChange)="onFilterChange()">
          <option value="all">All Statuses</option>
          <option value="success">Success (2xx)</option>
          <option value="failed">Failed (4xx/5xx)</option>
          <option value="cancelled">Cancelled</option>
        </select>
      </div>

      <div class="filter-group">
        <label class="filter-label">Environment</label>
        <select class="glass-input" [(ngModel)]="envFilter" (ngModelChange)="onFilterChange()">
          <option value="all">All Environments</option>
          @for (envKey of environmentKeys; track envKey) {
            <option [value]="envKey">{{ envKey }}</option>
          }
        </select>
      </div>

      <button type="button" class="btn-clear glass-input" (click)="resetFilters()" title="Reset Filters">
        <i class="bi bi-arrow-counterclockwise"></i> Reset
      </button>
    </div>
  `,
  styles: [`
    .filter-bar { display: flex; align-items: flex-end; gap: 16px; padding: 16px 20px; margin-bottom: 24px; flex-wrap: wrap; }
    .filter-group { display: flex; flex-direction: column; gap: 6px; }
    .flex-grow { flex: 1; min-width: 220px; }
    .filter-label { font-size: 0.8rem; font-weight: 500; color: var(--text-secondary); }
    .input-with-icon { position: relative; display: flex; align-items: center; }
    .input-with-icon i { position: absolute; left: 12px; color: var(--text-muted); }
    .input-with-icon input { padding-left: 36px; width: 100%; }
    .btn-clear { height: 38px; display: flex; align-items: center; gap: 6px; cursor: pointer; color: var(--text-secondary); }
    .btn-clear:hover { color: var(--text-primary); border-color: var(--primary); }
  `]
})
export class FilterBarComponent {
  @Input() environmentKeys: string[] = [];
  @Output() filterChange = new EventEmitter<{ query: string, status: string, env: string }>();

  searchQuery: string = '';
  statusFilter: string = 'all';
  envFilter: string = 'all';

  onFilterChange() {
    this.filterChange.emit({
      query: this.searchQuery,
      status: this.statusFilter,
      env: this.envFilter
    });
  }

  resetFilters() {
    this.searchQuery = '';
    this.statusFilter = 'all';
    this.envFilter = 'all';
    this.onFilterChange();
  }
}
