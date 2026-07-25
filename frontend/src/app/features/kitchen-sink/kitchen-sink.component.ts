import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../core/services/toast.service';
import { ThemeService } from '../../core/services/theme.service';
import {
  GradientCardComponent, StatTileComponent, StatusPillComponent, JsonTreeComponent,
  DrawerComponent, ConfirmDialogComponent, EmptyStateComponent, SkeletonComponent,
  DataTableComponent, DataTableColumn, PaginationComponent, RiyalComponent,
  CopyButtonComponent, FilterChipComponent, PageHeaderComponent
} from '../../shared/ui';

interface DemoRow extends Record<string, unknown> {
  id: number;
  orderNumber: string;
  branch: string;
  net: number;
}

/**
 * Dev-only showcase of every shared/ui component in every state -- see
 * app.routes.ts (only registered when !environment.production) and
 * docs/design-system.md. Never linked from production navigation.
 */
@Component({
  selector: 'app-kitchen-sink',
  standalone: true,
  imports: [
    CommonModule, GradientCardComponent, StatTileComponent, StatusPillComponent, JsonTreeComponent,
    DrawerComponent, ConfirmDialogComponent, EmptyStateComponent, SkeletonComponent,
    DataTableComponent, PaginationComponent, RiyalComponent, CopyButtonComponent,
    FilterChipComponent, PageHeaderComponent
  ],
  template: `
    <app-page-header title="Kitchen Sink" subtitle="Every shared/ui component, every state. Dev-only.">
      <button type="button" class="theme-toggle" (click)="themeService.toggleTheme()">
        Toggle theme ({{ themeService.theme() }})
      </button>
    </app-page-header>

    <section class="sink-section">
      <h2>Stat tiles</h2>
      <div class="tile-grid">
        <app-stat-tile label="Requests" [value]="statValue()" icon="bi-inbox" variant="brand" [active]="true"></app-stat-tile>
        <app-stat-tile label="Succeeded" [value]="statValue() - 12" icon="bi-check-circle" variant="success"></app-stat-tile>
        <app-stat-tile label="Failed" [value]="8" icon="bi-x-circle" variant="danger"></app-stat-tile>
        <app-stat-tile label="Cancelled" [value]="3" icon="bi-slash-circle" variant="muted"></app-stat-tile>
      </div>
      <button type="button" class="demo-btn" (click)="statValue.set(statValue() + 137)">Bump count (tests count-up)</button>
    </section>

    <section class="sink-section">
      <h2>Status pills -- all nine</h2>
      <div class="pill-row">
        @for (s of [1,2,3,4,5,6,7,8,9]; track s) {
          <app-status-pill [status]="s"></app-status-pill>
        }
      </div>
      <button type="button" class="demo-btn" (click)="popStatus.set(popStatus() === 1 ? 9 : 1)">
        Toggle status (tests pop animation): <app-status-pill [status]="popStatus()"></app-status-pill>
      </button>
    </section>

    <section class="sink-section">
      <h2>Gradient cards -- all variants</h2>
      <div class="card-grid">
        <app-gradient-card variant="brand">Brand</app-gradient-card>
        <app-gradient-card variant="success">Success</app-gradient-card>
        <app-gradient-card variant="danger">Danger</app-gradient-card>
        <app-gradient-card variant="info">Info</app-gradient-card>
        <app-gradient-card variant="muted">Muted</app-gradient-card>
      </div>
    </section>

    <section class="sink-section">
      <h2>Riyal glyph + copy button</h2>
      <p class="riyal-demo">1,284.50 <app-riyal [size]="1.1"></app-riyal></p>
      <app-copy-button value="Copied from the kitchen sink" label="Copy sample text"></app-copy-button>
    </section>

    <section class="sink-section">
      <h2>Filter chips</h2>
      <div class="chip-row" *ngIf="chips().length; else noChips">
        @for (chip of chips(); track chip) {
          <app-filter-chip [label]="chip" (remove)="removeChip(chip)"></app-filter-chip>
        }
      </div>
      <ng-template #noChips><span class="text-muted">All chips removed.</span></ng-template>
    </section>

    <section class="sink-section">
      <h2>JSON tree -- nested payload and malformed string</h2>
      <app-json-tree title="Valid nested payload" [data]="samplePayload"></app-json-tree>
      <app-json-tree title="Malformed string (danger banner)" [data]="malformedJson"></app-json-tree>
    </section>

    <section class="sink-section">
      <h2>Data table + pagination (virtual scroll, staggered rows)</h2>
      <app-data-table [columns]="tableColumns" [rows]="tableRows" height="260px"></app-data-table>
      <app-pagination [page]="page()" [pageSize]="25" [total]="140" (pageChange)="page.set($event)"></app-pagination>
    </section>

    <section class="sink-section">
      <h2>Skeletons</h2>
      <app-skeleton height="14px" width="60%"></app-skeleton>
      <app-skeleton height="14px" width="80%"></app-skeleton>
      <app-skeleton height="40px" width="100%" radius="var(--radius-lg)"></app-skeleton>
    </section>

    <section class="sink-section">
      <h2>Empty state</h2>
      <app-empty-state icon="bi-inbox" title="No requests yet" description="Orders sent from this module will appear here.">
        <button type="button" class="demo-btn">Take an action</button>
      </app-empty-state>
    </section>

    <section class="sink-section">
      <h2>Drawer</h2>
      <button type="button" class="demo-btn" (click)="drawerOpen.set(true)">Open drawer</button>
      <app-drawer *ngIf="drawerOpen()" title="Order UPC-99812" (close)="drawerOpen.set(false)">
        <p>Drawer content -- CDK focus trap, Esc/backdrop close, spring slide-in.</p>
      </app-drawer>
    </section>

    <section class="sink-section">
      <h2>Confirm dialog (danger, required reason)</h2>
      <button type="button" class="demo-btn" (click)="confirmOpen.set(true)">Open confirm dialog</button>
      <app-confirm-dialog
        *ngIf="confirmOpen()"
        title="Cancel this order?"
        message="This sends a cancellation request to the upstream API."
        variant="danger"
        [requireReason]="true"
        reasonLabel="Cancellation reason"
        confirmLabel="Cancel order"
        (cancel)="confirmOpen.set(false)"
        (confirm)="onConfirmed($event)">
      </app-confirm-dialog>
    </section>

    <section class="sink-section">
      <h2>Toasts</h2>
      <div class="demo-row">
        <button type="button" class="demo-btn" (click)="toast.showSuccess('Order sent successfully!')">Success toast</button>
        <button type="button" class="demo-btn" (click)="toast.showError('Upstream returned 502.')">Error toast</button>
        <button type="button" class="demo-btn" (click)="toast.showWarning('This feature isn\\'t available yet.')">Warning toast</button>
        <button type="button" class="demo-btn" (click)="toast.showInfo('No item found in database.')">Info toast</button>
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; padding: 24px 32px 80px; max-width: 1200px; margin: 0 auto; }
    .theme-toggle {
      background: rgba(255,255,255,.15); border: 1px solid rgba(255,255,255,.3); color: var(--text-primary);
      border-radius: var(--radius-pill); padding: 8px 16px; cursor: pointer;
    }
    .sink-section { margin-bottom: 40px; }
    .sink-section h2 { font-size: 1.1rem; color: var(--text-primary); margin-bottom: 16px; }
    .tile-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 12px; }
    .pill-row { display: flex; flex-wrap: wrap; gap: 10px; margin-bottom: 12px; }
    .card-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 16px; }
    .card-grid app-gradient-card { display: block; padding: 20px; color: var(--text-primary); font-weight: 600; }
    .riyal-demo { font-size: 1.3rem; font-weight: 700; color: var(--text-primary); }
    .chip-row { display: flex; flex-wrap: wrap; gap: 8px; }
    .text-muted { color: var(--text-muted); font-size: 0.85rem; }
    .demo-btn {
      background: var(--grad-brand); color: var(--on-gradient); border: none; border-radius: var(--radius-md);
      padding: 8px 16px; cursor: pointer; font-weight: 600; margin-top: 8px;
    }
    .demo-row { display: flex; flex-wrap: wrap; gap: 10px; }
  `]
})
export class KitchenSinkComponent {
  toast = inject(ToastService);
  themeService = inject(ThemeService);

  statValue = signal(1284);
  popStatus = signal(1);
  page = signal(1);
  drawerOpen = signal(false);
  confirmOpen = signal(false);
  chips = signal(['branch: P900', 'status: failed', 'succeeded only']);

  samplePayload = {
    order_code: 'UPC-99812',
    branch_code: 'P900',
    order_products: [
      { item_code: '000000000000212401', item_name: 'Beesline F/Cr.Future Barrier 50Gm', quantity: 2, unit_price: 175.0 },
      { item_code: '000000000000100002', item_name: 'Acc 200Mg 20Sachets.', quantity: 1, unit_price: 11.4 }
    ],
    order_gps: [21.5433, 39.1728],
    is_delivery: true,
    order_notes: null
  };

  malformedJson = '{"order_code": "UPC-1", "branch_code": ';

  tableColumns: DataTableColumn[] = [
    { key: 'orderNumber', label: 'Order #' },
    { key: 'branch', label: 'Branch' },
    { key: 'net', label: 'Net Total', align: 'right' }
  ];

  tableRows: DemoRow[] = Array.from({ length: 40 }, (_, i) => ({
    id: i,
    orderNumber: `UPC-${9000 + i}`,
    branch: `P${900 + (i % 5)}`,
    net: Math.round((100 + i * 17.3) * 100) / 100
  }));

  removeChip(chip: string) {
    this.chips.update(list => list.filter(c => c !== chip));
  }

  onConfirmed(reason: string) {
    this.confirmOpen.set(false);
    this.toast.showSuccess(`Confirmed with reason: "${reason}"`);
  }
}
