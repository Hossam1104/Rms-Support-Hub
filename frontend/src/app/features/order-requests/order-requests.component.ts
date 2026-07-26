import { Component, OnInit, OnDestroy, inject, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterOutlet } from '@angular/router';
import { OrderRequestsStore, OrderRequestsFilterState, EMPTY_FILTERS } from './order-requests.store';
import { ModuleService } from '../../core/services/module.service';
import { PageHeaderComponent, StatTileComponent, PaginationComponent } from '../../shared/ui';
import { FilterBarComponent } from './components/filter-bar.component';
import { RequestsTableComponent } from './components/requests-table.component';

/**
 * Rebuilt on the R5 API and the R8 kit (remediation_plan.md B14, R9). The
 * store is provided here (not root) so it resets whenever this route is
 * left and re-entered, and so the routed :requestId drawer
 * (order-request-drawer.component.ts, rendered through the <router-outlet>
 * below) can inject the exact same instance.
 */
@Component({
  selector: 'app-order-requests',
  standalone: true,
  providers: [OrderRequestsStore],
  imports: [
    CommonModule, FormsModule, RouterOutlet, PageHeaderComponent, StatTileComponent,
    PaginationComponent, FilterBarComponent, RequestsTableComponent
  ],
  template: `
    <app-page-header [title]="'Order Requests'" [subtitle]="moduleService.activeModule()?.label">
      <span class="env-pill" [class.env-production]="isProduction()" [class.env-testing]="!isProduction()">
        {{ activeEnvKey() || 'Default environment' }}
      </span>
      <button type="button" class="hero-btn" (click)="store.refresh()" [disabled]="store.status() === 'loading'">
        <i class="bi bi-arrow-clockwise" [class.spin]="store.status() === 'loading'"></i> Refresh
      </button>
      <label class="auto-refresh-toggle">
        <input type="checkbox" [(ngModel)]="autoRefresh" />
        Auto-refresh 30s
      </label>
    </app-page-header>

    <div class="stat-tiles">
      <app-stat-tile
        label="Requests" icon="bi-inbox" variant="brand"
        [value]="store.stats()?.total || 0"
        [active]="!store.hasActiveFilters()"
        (clicked)="store.applyStatTile('requests')">
      </app-stat-tile>
      <app-stat-tile
        label="Succeeded" icon="bi-check-circle" variant="success"
        [value]="store.stats()?.succeeded || 0"
        [active]="store.filters().outcome === 'succeeded'"
        (clicked)="store.applyStatTile('succeeded')">
      </app-stat-tile>
      <app-stat-tile
        label="Failed" icon="bi-x-circle" variant="danger"
        [value]="store.stats()?.failed || 0"
        [active]="store.filters().outcome === 'failed'"
        (clicked)="store.applyStatTile('failed')">
      </app-stat-tile>
      <app-stat-tile
        label="Cancelled" icon="bi-slash-circle" variant="muted"
        [value]="store.stats()?.cancelled || 0"
        [active]="isCancelledFilterActive()"
        (clicked)="store.applyStatTile('cancelled')">
      </app-stat-tile>
    </div>

    <app-filter-bar></app-filter-bar>

    <app-requests-table></app-requests-table>

    <app-pagination
      [page]="store.page()"
      [pageSize]="store.pageSize()"
      [total]="store.total()"
      (pageChange)="store.setPage($event)"
      (pageSizeChange)="store.setPageSize($event)">
    </app-pagination>

    <router-outlet></router-outlet>
  `,
  styles: [`
    :host { display: block; }
    .env-pill { padding: 6px 14px; border-radius: var(--radius-pill); font-size: 0.78rem; font-weight: 700; color: var(--on-gradient); }
    .env-production { background: var(--grad-amber); }
    .env-testing { background: var(--grad-info); }
    .hero-btn {
      display: flex; align-items: center; gap: 6px;
      background: rgba(255,255,255,.15); border: 1px solid rgba(255,255,255,.3); color: var(--text-primary);
      border-radius: var(--radius-pill); padding: 8px 16px; cursor: pointer; font-size: 0.85rem;
    }
    .hero-btn:disabled { opacity: 0.6; cursor: not-allowed; }
    .spin { animation: spin 1s linear infinite; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    .auto-refresh-toggle { display: flex; align-items: center; gap: 6px; font-size: 0.8rem; color: var(--text-primary); cursor: pointer; }
    .stat-tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 20px; }
  `]
})
export class OrderRequestsComponent implements OnInit, OnDestroy {
  store = inject(OrderRequestsStore);
  moduleService = inject(ModuleService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  autoRefresh = false;
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  private syncingFromUrl = false;

  constructor() {
    // Serialize filters/page/pageSize to query params so reload and
    // back/forward preserve state (R9 step 4).
    effect(() => {
      const f = this.store.filters();
      const page = this.store.page();
      const pageSize = this.store.pageSize();
      if (this.syncingFromUrl) return;

      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: this.toQueryParams(f, page, pageSize),
        queryParamsHandling: 'merge',
        replaceUrl: true
      });
    });
  }

  ngOnInit() {
    const parentKey = this.route.parent?.snapshot.paramMap.get('key') || '';
    const qp = this.route.snapshot.queryParamMap;

    this.syncingFromUrl = true;
    const filters: OrderRequestsFilterState = {
      ...EMPTY_FILTERS,
      search: qp.get('search') || '',
      phone: qp.get('phone') || '',
      branchCode: qp.get('branchCode'),
      statuses: qp.getAll('status').map(Number).filter(n => Number.isFinite(n)),
      outcome: (qp.get('outcome') as OrderRequestsFilterState['outcome']) || 'all',
      dateFrom: qp.get('dateFrom'),
      dateTo: qp.get('dateTo')
    };
    const page = Number(qp.get('page')) || 1;
    const pageSize = Number(qp.get('pageSize')) || 25;

    const envKey = this.moduleService.activeEnvironment()?.key || null;
    this.store.init(parentKey, envKey, filters, page, pageSize);
    this.syncingFromUrl = false;

    this.refreshTimer = setInterval(() => {
      if (this.autoRefresh && this.store.selectedId() === null && !document.hidden) {
        this.store.refresh();
      }
    }, 30000);
  }

  ngOnDestroy() {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  isProduction(): boolean {
    return (this.moduleService.activeEnvironment()?.environment || '') === 'Production';
  }

  activeEnvKey(): string | undefined {
    return this.moduleService.activeEnvironment()?.key;
  }

  isCancelledFilterActive(): boolean {
    const s = this.store.filters().statuses;
    return s.length === 2 && s.includes(6) && s.includes(7);
  }

  private toQueryParams(f: OrderRequestsFilterState, page: number, pageSize: number): Record<string, string | string[] | null> {
    return {
      search: f.search || null,
      phone: f.phone || null,
      branchCode: f.branchCode || null,
      status: f.statuses.length ? f.statuses.map(String) : null,
      outcome: f.outcome !== 'all' ? f.outcome : null,
      dateFrom: f.dateFrom || null,
      dateTo: f.dateTo || null,
      page: page > 1 ? String(page) : null,
      pageSize: pageSize !== 25 ? String(pageSize) : null
    };
  }
}
