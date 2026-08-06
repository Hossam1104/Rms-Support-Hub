import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { PageHeaderComponent } from '../../shared/ui';
import { TOOL_ROUTE_DATA, ToolRouteData } from '../../core/models';

interface HubToolEntry {
  route: string;
  icon: string;
  accentClass: string;
  description: string;
  meta: ToolRouteData;
}

/** Session 01 placeholder for the QA Support Hub dashboard: a working,
 * keyboard-accessible entry point for every tool route. The full card grid,
 * status model, and motion language arrive with Phase B. */
@Component({
  selector: 'app-hub',
  standalone: true,
  imports: [CommonModule, RouterLink, NavbarComponent, PageHeaderComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="hub-container">
      <app-page-header title="QA Support Hub" subtitle="One workspace for prompt engineering, online order operations, and POS maintenance."></app-page-header>

      <nav class="tool-list slide-up" aria-label="Tools">
        @for (tool of tools; track tool.route) {
          <a class="tool-row" [routerLink]="tool.route">
            <i class="bi tool-icon" [class]="tool.icon + ' ' + tool.accentClass"></i>
            <span class="tool-text">
              <span class="tool-name">{{ tool.meta.title }}</span>
              <span class="tool-desc">{{ tool.description }}</span>
            </span>
            <span class="tool-status" [class.status-available]="tool.meta.status === 'available'" [class.status-pending]="tool.meta.status === 'migration-pending'">
              {{ tool.meta.status === 'available' ? 'Available' : 'Migration Pending' }}
            </span>
          </a>
        }
      </nav>
    </main>
  `,
  styles: [`
    .hub-container {
      margin-top: var(--navbar-height);
      padding: 40px 40px 60px;
      max-width: 1300px;
      margin-left: auto;
      margin-right: auto;
    }
    .tool-list { display: flex; flex-direction: column; gap: 16px; }
    .tool-row {
      display: flex;
      align-items: center;
      gap: 20px;
      padding: 20px 24px;
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-lg);
      background: var(--surface-panel);
      color: var(--text-primary);
      text-decoration: none;
      transition: border-color var(--transition-fast), background var(--transition-fast), transform var(--d-fast) var(--ease-spring);
    }
    .tool-row:hover { border-color: var(--border-strong); background: var(--surface-raised); transform: translateX(4px); }
    .tool-row:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .tool-icon { font-size: 1.6rem; -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .tool-icon.accent-brand { background: var(--grad-brand); -webkit-background-clip: text; }
    .tool-icon.accent-info { background: var(--grad-info); -webkit-background-clip: text; }
    .tool-icon.accent-amber { background: var(--grad-amber); -webkit-background-clip: text; }
    .tool-text { display: flex; flex: 1; flex-direction: column; gap: 4px; min-width: 0; }
    .tool-name { font-size: 1.05rem; font-weight: 700; }
    .tool-desc { color: var(--text-secondary); font-size: 0.88rem; }
    .tool-status {
      flex-shrink: 0;
      padding: 5px 14px;
      border-radius: var(--radius-pill);
      font-size: 0.78rem;
      font-weight: 700;
    }
    .tool-status.status-available { color: var(--state-success-fg); background: var(--state-success-bg); border: 1px solid var(--state-success-border); }
    .tool-status.status-pending { color: var(--state-warning-fg); background: var(--state-warning-bg); border: 1px solid var(--state-warning-border); }
    @media (max-width: 640px) {
      .hub-container { padding: 24px 16px 40px; }
      .tool-row { flex-wrap: wrap; gap: 12px; padding: 16px; }
    }
  `]
})
export class HubComponent {
  readonly tools: HubToolEntry[] = [
    {
      route: '/tools/prompt-studio',
      icon: 'bi-magic',
      accentClass: 'accent-brand',
      description: 'Refine bugs and stories, generate test cases. Workspace migration in progress.',
      meta: TOOL_ROUTE_DATA.promptStudio
    },
    {
      route: '/tools/online-orders',
      icon: 'bi-bag-check',
      accentClass: 'accent-info',
      description: 'Monitor order requests and build order payloads for connected clients.',
      meta: TOOL_ROUTE_DATA.onlineOrders
    },
    {
      route: '/tools/pos-maintenance',
      icon: 'bi-pc-display',
      accentClass: 'accent-amber',
      description: 'Approved POS support and maintenance workflows.',
      meta: TOOL_ROUTE_DATA.posMaintenance
    }
  ];
}
