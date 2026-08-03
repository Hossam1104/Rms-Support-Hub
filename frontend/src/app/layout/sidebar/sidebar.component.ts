import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { SidebarStateService } from '../../core/services/sidebar-state.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
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
        <button type="button" class="btn-toggle" (click)="toggleCollapse()" [attr.aria-expanded]="!collapsed()" aria-label="Toggle navigation sidebar" title="Toggle Sidebar">
          <i class="bi" [class.bi-chevron-left]="!collapsed()" [class.bi-chevron-right]="collapsed()"></i>
        </button>
      </div>

      <nav class="sidebar-nav" aria-label="Module navigation">
        <a *ngIf="moduleKey !== 'ghc_unicommerce'" routerLink="order" routerLinkActive="active" class="nav-item">
          <i class="bi bi-speedometer2"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Builder</span>
        </a>

        <a *ngIf="moduleKey === 'ghc_unicommerce'" routerLink="unicommerce" routerLinkActive="active" class="nav-item">
          <i class="bi bi-file-earmark-spreadsheet"></i>
          <span class="nav-label" *ngIf="!collapsed()">Invoice Builder</span>
        </a>

        <a routerLink="requests" routerLinkActive="active" class="nav-item">
          <i class="bi bi-clock-history"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Requests</span>
        </a>

        <a *ngIf="moduleKey === 'upc_ecommerce'" routerLink="validation" routerLinkActive="active" class="nav-item">
          <i class="bi bi-list-check"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Validation</span>
        </a>
      </nav>

      <div class="sidebar-footer" *ngIf="!collapsed()">
        <a routerLink="/" class="back-link">
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
    .btn-toggle { display: grid; width: 32px; height: 32px; flex: 0 0 32px; place-items: center; border: 1px solid var(--border-subtle); border-radius: var(--radius-sm); background: var(--surface-interactive); color: var(--text-secondary); cursor: pointer; }
    .btn-toggle:hover { background: var(--surface-hover); color: var(--text-primary); }
    .btn-toggle:focus-visible { outline: none; box-shadow: var(--focus-ring); }
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
    @media (max-width: 768px) { .sidebar { box-shadow: var(--shadow-lg); } }
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
