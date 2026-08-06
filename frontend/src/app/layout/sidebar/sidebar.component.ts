import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { SidebarStateService } from '../../core/services/sidebar-state.service';
import { UiButtonComponent } from '../../shared/ui';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, UiButtonComponent],
  template: `
    <aside class="sidebar" [class.collapsed]="collapsed()">
      <div class="sidebar-header">
        <div class="brand-logo" *ngIf="!collapsed()">
          <i class="bi bi-layers-half brand-icon"></i>
          <div class="brand-text">
            <span class="module-title">{{ moduleLabel || 'Order Tool' }}</span>
            <span class="client-title">{{ clientName || 'Client' }}</span>
          </div>
        </div>
        <ui-button class="btn-toggle" variant="ghost" size="sm"
          [icon]="collapsed() ? 'bi-chevron-right' : 'bi-chevron-left'"
          [ariaLabel]="collapsed() ? 'Expand navigation sidebar' : 'Collapse navigation sidebar'"
          [ariaExpanded]="!collapsed()"
          (pressed)="toggleCollapse()"></ui-button>
      </div>

      <nav class="sidebar-nav" aria-label="Module navigation">
        <a *ngIf="moduleKey !== 'ghc_unicommerce'" routerLink="order" routerLinkActive="active" class="nav-item" aria-label="Order Builder" title="Order Builder">
          <i class="bi bi-speedometer2"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Builder</span>
        </a>

        <a *ngIf="moduleKey === 'ghc_unicommerce'" routerLink="unicommerce" routerLinkActive="active" class="nav-item" aria-label="Invoice Builder" title="Invoice Builder">
          <i class="bi bi-file-earmark-spreadsheet"></i>
          <span class="nav-label" *ngIf="!collapsed()">Invoice Builder</span>
        </a>

        <a routerLink="order-requests" routerLinkActive="active" class="nav-item" aria-label="Order Requests" title="Order Requests">
          <i class="bi bi-clock-history"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Requests</span>
        </a>
      </nav>

      <div class="sidebar-footer" *ngIf="!collapsed()">
        <a routerLink="/tools/online-orders" class="back-link">
          <i class="bi bi-arrow-left"></i>
          <span>Back to Modules</span>
        </a>
      </div>
    </aside>
  `,
  styles: [`
    :host { display: contents; }
    .sidebar { position: fixed; top: var(--navbar-height); left: 0; bottom: 0; z-index: 900; display: flex; width: var(--sidebar-expanded-width); flex-direction: column; background: var(--surface-panel); border-right: 1px solid var(--border-subtle); color: var(--text-primary); transition: width var(--transition-normal), transform var(--transition-normal); }
    .sidebar.collapsed { width: var(--sidebar-collapsed-width); }
    .sidebar-header { display: flex; align-items: center; justify-content: space-between; min-height: 72px; padding: 16px; border-bottom: 1px solid var(--divider); }
    .brand-logo { display: flex; align-items: center; gap: 12px; min-width: 0; }
    .brand-icon { color: var(--accent); font-size: 1.5rem; }
    .brand-text { display: flex; flex-direction: column; min-width: 0; }
    .module-title { overflow: hidden; font-size: .95rem; font-weight: 750; text-overflow: ellipsis; white-space: nowrap; }
    .client-title { color: var(--text-muted); font-size: .75rem; }
    :host ::ng-deep ui-button.btn-toggle { display: grid; width: 32px; height: 32px; flex: 0 0 32px; }
    :host ::ng-deep ui-button.btn-toggle .ui-button { width: 32px; min-height: 32px; padding: 0; border-color: var(--border-subtle); background: var(--surface-interactive); color: var(--text-secondary); }
    :host ::ng-deep ui-button.btn-toggle .ui-button:hover:not(:disabled) { background: var(--surface-hover); color: var(--text-primary); }
    .sidebar-nav { display: flex; flex: 1; flex-direction: column; gap: 4px; padding: 16px 8px; }
    .nav-item { position: relative; display: flex; align-items: center; gap: 12px; padding: 11px 14px; border-radius: var(--radius-md); color: var(--text-secondary); text-decoration: none; transition: background var(--transition-fast), color var(--transition-fast), transform var(--d) var(--ease-spring); }
    .nav-item::before { content: ''; position: absolute; inset: 8px auto 8px 0; width: 2px; border-radius: var(--radius-pill); background: transparent; }
    .nav-item i { font-size: 1.1rem; }
    .nav-item:hover { background: var(--surface-hover); color: var(--text-primary); transform: translateX(3px); }
    .nav-item:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .nav-item.active { background: var(--surface-selected); color: var(--text-accent); font-weight: 700; transform: translateX(0); }
    .nav-item.active::before { background: var(--accent); }
    .sidebar-footer { padding: 16px; border-top: 1px solid var(--divider); }
    .back-link { display: flex; align-items: center; gap: 8px; color: var(--text-secondary); font-size: .85rem; font-weight: 500; text-decoration: none; }
    .back-link:hover { color: var(--text-accent); }
    .back-link:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }
    @media (max-width: 768px) {
      .sidebar { width: var(--sidebar-collapsed-width); box-shadow: var(--shadow-lg); }
      .sidebar-header { min-height: 56px; justify-content: center; padding: 10px 8px; }
      .brand-logo, .nav-label, .sidebar-footer { display: none; }
      :host ::ng-deep ui-button.btn-toggle { display: none; }
      .sidebar-nav { padding-inline: 8px; }
      .nav-item { justify-content: center; padding-inline: 12px; }
    }
  `]
})
export class SidebarComponent {
  @Input() moduleKey = '';
  @Input() moduleLabel = '';
  @Input() clientName = '';

  private readonly sidebarState = inject(SidebarStateService);
  readonly collapsed = this.sidebarState.collapsed;

  toggleCollapse() { this.sidebarState.toggle(); }
}
