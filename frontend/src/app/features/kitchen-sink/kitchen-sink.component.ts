import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ToastService } from '../../core/services/toast.service';
import { ThemeService } from '../../core/services/theme.service';
import { BranchOption } from '../../core/models';
import {
  CopyButtonComponent, DataTableColumn, DataTableComponent, DrawerComponent,
  EmptyStateComponent, FilterChipComponent, GradientCardComponent, JsonTreeComponent,
  PageHeaderComponent, PaginationComponent, RiyalComponent, SearchableSelectComponent,
  SkeletonComponent, StatTileComponent, StatusPillComponent, UiButtonComponent,
  UiCardComponent, UiFieldComponent, UiInputComponent, UiSectionComponent,
  UiSelectComponent, UiTableComponent, UiToolbarComponent, UiSelectOption,
  ConfirmDialogComponent
} from '../../shared/ui';

interface DemoRow extends Record<string, unknown> {
  id: number;
  orderNumber: string;
  branch: string;
  net: number;
}

/** Development-only visual contract for the shared design system. */
@Component({
  selector: 'app-kitchen-sink',
  standalone: true,
  imports: [
    CommonModule, CopyButtonComponent, DataTableComponent, DrawerComponent,
    EmptyStateComponent, FilterChipComponent, GradientCardComponent, JsonTreeComponent,
    PageHeaderComponent, PaginationComponent, RiyalComponent, SearchableSelectComponent,
    SkeletonComponent, StatTileComponent, StatusPillComponent, UiButtonComponent,
    UiCardComponent, UiFieldComponent, UiInputComponent, UiSectionComponent,
    UiSelectComponent, UiTableComponent, UiToolbarComponent, ConfirmDialogComponent
  ],
  template: `
    <app-page-header title="Kitchen Sink" subtitle="The shared control desk: every U5 primitive, state, theme, and accessibility edge. Dev-only.">
      <ui-button variant="secondary" size="sm" icon="bi-circle-half" (pressed)="themeService.toggleTheme()">
        {{ themeService.theme() === 'dark' ? 'Light theme' : 'Dark theme' }}
      </ui-button>
    </app-page-header>

    <ui-toolbar role="toolbar" ariaLabel="Kitchen sink controls">
      <span uiToolbarStart class="toolbar-kicker">U5 DESIGN SYSTEM</span>
      <span uiToolbarCenter class="toolbar-note">Dark-first / light maintained</span>
      <span uiToolbarEnd>
        <ui-button variant="ghost" size="sm" icon="bi-arrow-clockwise" (pressed)="resetDemo()">Reset demo</ui-button>
      </span>
    </ui-toolbar>

    <ui-section title="Cards and sections" description="Token surfaces replace gradient-heavy default containers." [completed]="true">
      <span uiSectionActions>
        <ui-button variant="ghost" size="sm" (pressed)="cardActivated.set(false)">Clear selection</ui-button>
      </span>
      <div class="card-grid">
        <ui-card>
          <span uiCardHeader>Base panel</span>
          <p>Quiet surface for routine data and forms.</p>
        </ui-card>
        <ui-card variant="raised">
          <span uiCardHeader>Raised panel</span>
          <p>Reserved for a step or summary that needs lift.</p>
        </ui-card>
        <ui-card variant="interactive" [ariaLabel]="'Interactive card'" (activated)="cardActivated.set(true)">
          <span uiCardHeader>Interactive option</span>
          <p>{{ cardActivated() ? 'Activated with keyboard or pointer.' : 'Activate this card.' }}</p>
        </ui-card>
        <ui-card variant="interactive" [disabled]="true">
          <span uiCardHeader>Disabled option</span>
          <p>Disabled states remain visibly and behaviorally disabled.</p>
        </ui-card>
      </div>
    </ui-section>

    <ui-section title="Fields and controls" description="Labels, descriptions, errors, focus, disabled, and form-compatible controls.">
      <div class="field-grid">
        <ui-field #emailField label="Operator email" forId="sink-email" [required]="true" hint="Used only for this local demonstration.">
          <ui-input inputId="sink-email" type="email" placeholder="operator@example.test" [ariaDescribedBy]="emailField.describedBy()"></ui-input>
        </ui-field>
        <ui-field #branchField label="Branch" forId="sink-branch" [required]="true" hint="Searchable branch selection is the U3 composition point.">
          <app-searchable-select label="Branch" placeholder="Search branches" [options]="branches" [value]="selectedBranch()" (valueChange)="selectedBranch.set($event)"></app-searchable-select>
        </ui-field>
        <ui-field #invalidField label="Invalid quantity" forId="sink-quantity" error="Enter a quantity greater than zero." [required]="true">
          <ui-input inputId="sink-quantity" type="number" value="0" [invalid]="true" [ariaDescribedBy]="invalidField.describedBy()"></ui-input>
        </ui-field>
        <ui-field label="Status" forId="sink-status" hint="Native select semantics with a tokenized surface.">
          <ui-select selectId="sink-status" placeholder="Choose a status" [options]="selectOptions" value="ready"></ui-select>
        </ui-field>
        <ui-field label="Read-only" forId="sink-readonly">
          <ui-input inputId="sink-readonly" value="Testing lane" [readOnly]="true"></ui-input>
        </ui-field>
        <ui-field label="Disabled" forId="sink-disabled">
          <ui-input inputId="sink-disabled" value="Unavailable" [disabled]="true"></ui-input>
        </ui-field>
      </div>
    </ui-section>

    <ui-section title="Buttons" description="Primary, secondary, ghost, danger, small, medium, icon, loading, and disabled states.">
      <div class="button-row">
        <ui-button variant="primary" icon="bi-check-lg" (pressed)="toast.showSuccess('Primary action completed.', 0)">Primary</ui-button>
        <ui-button variant="secondary" size="sm" icon="bi-sliders">Secondary small</ui-button>
        <ui-button variant="ghost" icon="bi-three-dots">Ghost</ui-button>
        <ui-button variant="danger" icon="bi-trash3">Danger</ui-button>
        <ui-button variant="primary" [loading]="buttonLoading()" loadingLabel="Saving..." (pressed)="buttonLoading.set(true)">Loading</ui-button>
        <ui-button variant="secondary" [disabled]="true">Disabled</ui-button>
      </div>
    </ui-section>

    <ui-section title="Tables and toolbar" description="Dense, sticky, zebra, horizontal overflow, empty state, and wrapping toolbar.">
      <ui-table [dense]="true" [stickyHeader]="true" [zebra]="true" caption="Dense order sample">
        <thead><tr><th>Order</th><th>Branch</th><th>Net</th></tr></thead>
        <tbody>
          <tr *ngFor="let row of tableRows | slice:0:4"><td>{{ row.orderNumber }}</td><td>{{ row.branch }}</td><td>{{ row.net | number:'1.2-2' }}</td></tr>
        </tbody>
      </ui-table>
      <ui-table [empty]="true"><span uiTableEmpty>No records in this state.</span></ui-table>
    </ui-section>

    <ui-section title="Status pills" description="All nine RequestOrderHeaders statuses remain gradient accents, not default surfaces.">
      <div class="pill-row">
        <app-status-pill *ngFor="let status of statuses" [status]="status"></app-status-pill>
      </div>
    </ui-section>

    <ui-section title="Toasts" description="Three visible, queued overflow, duplicate collapse, pause on hover/focus, and manual close.">
      <div class="button-row">
        <ui-button variant="primary" size="sm" (pressed)="toast.showSuccess('Order sent successfully!')">Success</ui-button>
        <ui-button variant="danger" size="sm" (pressed)="toast.showError('Upstream returned 502.')">Error</ui-button>
        <ui-button variant="secondary" size="sm" (pressed)="toast.showWarning('Testing lane is active.')">Warning</ui-button>
        <ui-button variant="ghost" size="sm" (pressed)="toast.showInfo('No item found in database.')">Info</ui-button>
        <ui-button variant="danger" size="sm" icon="bi-repeat" (pressed)="burstDuplicate()">Four identical errors</ui-button>
        <ui-button variant="secondary" size="sm" icon="bi-stack" (pressed)="queueToasts()">Queue six toasts</ui-button>
        <ui-button variant="ghost" size="sm" (pressed)="toast.clearAll()">Clear all</ui-button>
      </div>
      <p class="demo-meta">Visible {{ toast.toasts().length }} / {{ toast.maxVisible }} · queued {{ toast.queued().length }} · duplicate counter appears on the single matching entry.</p>
    </ui-section>

    <section class="legacy-showcase sink-section">
      <h2>Existing shared components</h2>
      <div class="tile-grid">
        <app-stat-tile label="Requests" [value]="statValue()" icon="bi-inbox" variant="brand" [active]="true"></app-stat-tile>
        <app-stat-tile label="Succeeded" [value]="statValue() - 12" icon="bi-check-circle" variant="success"></app-stat-tile>
        <app-stat-tile label="Failed" [value]="8" icon="bi-x-circle" variant="danger"></app-stat-tile>
        <app-stat-tile label="Cancelled" [value]="3" icon="bi-slash-circle" variant="muted"></app-stat-tile>
      </div>
      <div class="button-row"><ui-button variant="secondary" size="sm" (pressed)="statValue.set(statValue() + 137)">Bump count-up</ui-button></div>
      <div class="card-grid gradient-grid">
        <app-gradient-card variant="brand">Brand accent</app-gradient-card>
        <app-gradient-card variant="success">Success accent</app-gradient-card>
        <app-gradient-card variant="danger">Danger accent</app-gradient-card>
        <app-gradient-card variant="info">Info accent</app-gradient-card>
        <app-gradient-card variant="muted">Muted accent</app-gradient-card>
      </div>
      <p class="riyal-demo">1,284.50 <app-riyal [size]="1.1"></app-riyal> <app-copy-button value="Copied from the kitchen sink" label="Copy sample text"></app-copy-button></p>
    </section>

    <section class="legacy-showcase sink-section">
      <h2>Data, states, and overlays</h2>
      <div class="chip-row" *ngIf="chips().length; else noChips">
        <app-filter-chip *ngFor="let chip of chips()" [label]="chip" (remove)="removeChip(chip)"></app-filter-chip>
      </div>
      <ng-template #noChips><span class="text-muted">All chips removed.</span></ng-template>
      <app-json-tree title="Valid nested payload" [data]="samplePayload"></app-json-tree>
      <app-json-tree title="Malformed string (danger banner)" [data]="malformedJson"></app-json-tree>
      <app-data-table [columns]="tableColumns" [rows]="tableRows" height="260px"></app-data-table>
      <app-pagination [page]="page()" [pageSize]="25" [total]="140" (pageChange)="page.set($event)"></app-pagination>
      <div class="skeleton-stack"><app-skeleton height="14px" width="60%"></app-skeleton><app-skeleton height="14px" width="80%"></app-skeleton><app-skeleton height="40px" width="100%" radius="var(--radius-lg)"></app-skeleton></div>
      <app-empty-state icon="bi-inbox" title="No requests yet" description="Orders sent from this module will appear here."></app-empty-state>
      <div class="button-row"><ui-button variant="secondary" size="sm" (pressed)="drawerOpen.set(true)">Open drawer</ui-button><ui-button variant="danger" size="sm" (pressed)="confirmOpen.set(true)">Open confirm dialog</ui-button></div>
      <app-drawer *ngIf="drawerOpen()" title="Order UPC-99812" (close)="drawerOpen.set(false)"><p>Drawer content with focus management.</p></app-drawer>
      <app-confirm-dialog *ngIf="confirmOpen()" title="Cancel this order?" message="This sends a cancellation request to the upstream API." variant="danger" [requireReason]="true" reasonLabel="Cancellation reason" confirmLabel="Cancel order" (cancel)="confirmOpen.set(false)" (confirm)="onConfirmed($event)"></app-confirm-dialog>
    </section>
  `,
  styles: [`
    :host { display: block; max-width: 1240px; margin: 0 auto; padding: 24px 32px 80px; }
    .toolbar-kicker { color: var(--text-accent); font-size: .72rem; font-weight: 850; letter-spacing: .10em; }
    .toolbar-note, .demo-meta, .text-muted { color: var(--text-muted); font-size: .82rem; }
    .sink-section { margin-top: 24px; }
    .sink-section h2 { color: var(--text-primary); font-size: 1.1rem; margin: 0 0 14px; }
    .card-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 14px; }
    .card-grid p { margin: 0; color: var(--text-secondary); font-size: .86rem; }
    .field-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; }
    .button-row, .pill-row, .chip-row { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .pill-row { padding: 4px 0; }
    .ui-table-shell + ui-table { margin-top: 14px; }
    .legacy-showcase { display: flex; flex-direction: column; gap: 16px; }
    .tile-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 14px; }
    .gradient-grid app-gradient-card { display: block; min-height: 70px; padding: 22px; color: var(--text-primary); font-weight: 700; }
    .riyal-demo { display: flex; align-items: center; gap: 10px; color: var(--text-primary); font-size: 1.2rem; font-weight: 750; }
    .skeleton-stack { display: grid; gap: 10px; }
    @media (max-width: 720px) { :host { padding: 16px 16px 56px; } .field-grid { grid-template-columns: 1fr; } }
  `]
})
export class KitchenSinkComponent {
  readonly toast = inject(ToastService);
  readonly themeService = inject(ThemeService);

  readonly cardActivated = signal(false);
  readonly buttonLoading = signal(false);
  readonly statValue = signal(1284);
  readonly page = signal(1);
  readonly drawerOpen = signal(false);
  readonly confirmOpen = signal(false);
  readonly selectedBranch = signal<string | null>(null);
  readonly chips = signal(['branch: P900', 'status: failed', 'succeeded only']);
  readonly statuses = [1, 2, 3, 4, 5, 6, 7, 8, 9];
  readonly branches: BranchOption[] = [
    { code: '101', name: 'Main Branch' },
    { code: 'P900', name: 'Riyadh Pharmacy' },
    { code: 'JED-04', name: 'Jeddah Central' }
  ];
  readonly selectOptions: UiSelectOption[] = [
    { value: 'new', label: 'New' },
    { value: 'ready', label: 'Ready' },
    { value: 'done', label: 'Done' }
  ];

  readonly samplePayload = {
    order_code: 'UPC-99812', branch_code: 'P900',
    order_products: [{ item_code: '000000000000212401', quantity: 2, unit_price: 175.0 }],
    order_gps: [21.5433, 39.1728], is_delivery: true, order_notes: null
  };
  readonly malformedJson = '{"order_code": "UPC-1", "branch_code": ';
  readonly tableColumns: DataTableColumn[] = [
    { key: 'orderNumber', label: 'Order #' },
    { key: 'branch', label: 'Branch' },
    { key: 'net', label: 'Net Total', align: 'right' }
  ];
  readonly tableRows: DemoRow[] = Array.from({ length: 40 }, (_, i) => ({
    id: i, orderNumber: `UPC-${9000 + i}`, branch: `P${900 + (i % 5)}`,
    net: Math.round((100 + i * 17.3) * 100) / 100
  }));

  removeChip(chip: string) { this.chips.update(list => list.filter(value => value !== chip)); }

  burstDuplicate() {
    for (let i = 0; i < 4; i += 1) this.toast.showError('Repeated validation failure.', 0);
  }

  queueToasts() {
    for (let i = 1; i <= 6; i += 1) this.toast.showInfo(`Queued notification ${i}.`, 0);
  }

  resetDemo() {
    this.cardActivated.set(false);
    this.buttonLoading.set(false);
    this.selectedBranch.set(null);
    this.chips.set(['branch: P900', 'status: failed', 'succeeded only']);
    this.toast.clearAll();
  }

  onConfirmed(reason: string) {
    this.confirmOpen.set(false);
    this.toast.showSuccess(`Confirmed with reason: "${reason}"`);
  }
}
