import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <aside class="sidebar glass-panel" [class.collapsed]="collapsed()">
      <div class="sidebar-header">
        <div class="brand-logo" *ngIf="!collapsed()">
          <i class="bi bi-layers-half brand-icon"></i>
          <div class="brand-text">
            <span class="module-title">{{ moduleLabel || 'Order Tool' }}</span>
            <span class="client-title">{{ clientName || 'Client' }}</span>
          </div>
        </div>
        <button type="button" class="btn-toggle" (click)="toggleCollapse()" title="Toggle Sidebar">
          <i class="bi" [class.bi-chevron-left]="!collapsed()" [class.bi-chevron-right]="collapsed()"></i>
        </button>
      </div>

      <nav class="sidebar-nav">
        <!-- Flat Order Builder (GHC & UPC E-Commerce) -->
        <a *ngIf="moduleKey !== 'ghc_unicommerce'" routerLink="order" routerLinkActive="active" class="nav-item">
          <i class="bi bi-speedometer2"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Builder</span>
        </a>

        <!-- Uni-Commerce Invoice Builder (GHC Uni-Commerce) -->
        <a *ngIf="moduleKey === 'ghc_unicommerce'" routerLink="unicommerce" routerLinkActive="active" class="nav-item">
          <i class="bi bi-file-earmark-spreadsheet"></i>
          <span class="nav-label" *ngIf="!collapsed()">Invoice Builder</span>
        </a>

        <!-- Sent Order Requests History (All Modules) -->
        <a routerLink="requests" routerLinkActive="active" class="nav-item">
          <i class="bi bi-clock-history"></i>
          <span class="nav-label" *ngIf="!collapsed()">Order Requests</span>
        </a>

        <!-- UPC Database Validation (UPC Only) -->
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
    .sidebar {
      position: fixed;
      top: var(--navbar-height);
      left: 0;
      bottom: 0;
      width: var(--sidebar-width);
      z-index: 900;
      display: flex;
      flex-direction: column;
      transition: width var(--transition-normal);
    }
    .sidebar.collapsed { width: var(--sidebar-collapsed-width); }
    .sidebar-header { display: flex; align-items: center; justify-content: space-between; padding: 16px; border-bottom: 1px solid var(--glass-border); }
    .brand-logo { display: flex; align-items: center; gap: 12px; }
    .brand-icon { font-size: 1.5rem; color: var(--primary); }
    .brand-text { display: flex; flex-direction: column; }
    .module-title { font-weight: 700; font-size: 0.95rem; color: var(--text-primary); }
    .client-title { font-size: 0.75rem; color: var(--text-muted); }
    .btn-toggle { background: var(--bg-tertiary); border: 1px solid var(--glass-border); color: var(--text-secondary); border-radius: var(--radius-sm); width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; cursor: pointer; }
    .sidebar-nav { display: flex; flex-direction: column; gap: 4px; padding: 16px 8px; flex: 1; }
    .nav-item { display: flex; align-items: center; gap: 12px; padding: 10px 14px; color: var(--text-secondary); text-decoration: none; border-radius: var(--radius-md); transition: background var(--transition-fast), color var(--transition-fast), transform var(--d) var(--ease-spring); }
    .nav-item i { font-size: 1.1rem; }
    .nav-item:hover { background: var(--glass-hover-bg); color: var(--text-primary); transform: translateX(3px); }
    .nav-item.active { background: var(--grad-brand); color: var(--on-gradient); font-weight: 600; box-shadow: var(--shadow-sm); transform: translateX(0); }
    .sidebar-footer { padding: 16px; border-top: 1px solid var(--glass-border); }
    .back-link { display: flex; align-items: center; gap: 8px; color: var(--text-secondary); text-decoration: none; font-size: 0.85rem; font-weight: 500; }
    .back-link:hover { color: var(--primary); }
  `]
})
export class SidebarComponent {
  @Input() moduleKey: string = '';
  @Input() moduleLabel: string = '';
  @Input() clientName: string = '';

  collapsed = signal<boolean>(false);

  toggleCollapse() {
    this.collapsed.update(c => !c);
  }
}
