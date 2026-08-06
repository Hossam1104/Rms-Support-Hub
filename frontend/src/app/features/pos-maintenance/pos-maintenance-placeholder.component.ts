import { Component } from '@angular/core';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { PageHeaderComponent } from '../../shared/ui';

/** Session 01 route placeholder for the POS Maintenance Tool. The standalone
 * source project has not been supplied, so this page must only report the
 * migration state and planned scope -- it must never offer fake maintenance
 * actions (implementation plan §11.1). */
@Component({
  selector: 'app-pos-maintenance-placeholder',
  standalone: true,
  imports: [NavbarComponent, PageHeaderComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="placeholder-container">
      <app-page-header title="POS Maintenance" subtitle="Approved POS support and maintenance workflows."></app-page-header>

      <section class="pending-panel" aria-labelledby="pos-pending-heading">
        <span class="pending-badge" role="status">Migration Pending</span>
        <h2 id="pos-pending-heading">POS Maintenance is not available yet</h2>
        <p>
          The standalone POS Maintenance Tool has not been supplied for migration. This page reserves the
          route and documents the planned scope — no maintenance operations can run from here.
        </p>
        <ul class="scope-list">
          <li>Read-only machine and service status</li>
          <li>Allowlisted, audited maintenance operations</li>
          <li>Secured backend orchestration with a local agent</li>
        </ul>
      </section>
    </main>
  `,
  styles: [`
    .placeholder-container {
      margin-top: var(--navbar-height);
      padding: 40px 40px 60px;
      max-width: 1300px;
      margin-left: auto;
      margin-right: auto;
    }
    .pending-panel {
      padding: 32px;
      border: 1px solid var(--state-warning-border);
      border-radius: var(--radius-lg);
      background: var(--surface-panel);
    }
    .pending-badge {
      display: inline-block;
      padding: 5px 14px;
      border-radius: var(--radius-pill);
      color: var(--state-warning-fg);
      background: var(--state-warning-bg);
      border: 1px solid var(--state-warning-border);
      font-size: 0.78rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .pending-panel h2 { margin: 16px 0 8px; font-size: 1.25rem; color: var(--text-primary); }
    .pending-panel p { margin: 0 0 16px; color: var(--text-secondary); max-width: 640px; }
    .scope-list { margin: 0; padding-left: 20px; color: var(--text-secondary); }
    .scope-list li { margin-bottom: 6px; }
    @media (max-width: 640px) {
      .placeholder-container { padding: 24px 16px 40px; }
      .pending-panel { padding: 24px 18px; }
    }
  `]
})
export class PosMaintenancePlaceholderComponent {}
