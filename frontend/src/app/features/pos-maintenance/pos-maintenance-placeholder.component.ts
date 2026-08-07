import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PosCapability, POS_CAPABILITIES } from '../../core/models';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { PageHeaderComponent } from '../../shared/ui';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-pos-maintenance-placeholder',
  standalone: true,
  imports: [NavbarComponent, PageHeaderComponent, StatusBadgeComponent, RouterLink],
  template: `
    <app-navbar></app-navbar>

    <main class="pos-page" aria-label="POS Maintenance Tool">
      <app-page-header
        title="POS Maintenance Tool"
        subtitle="This tool is currently under development and will be integrated into QA Support Hub when ready.">
      </app-page-header>

      <section class="status-grid" aria-label="POS availability">
        <article class="status-panel" aria-labelledby="pos-status-title">
          <p class="section-kicker">Status</p>
          <div class="status-panel__heading">
            <span class="status-panel__icon" aria-hidden="true"><i class="bi bi-hourglass-split"></i></span>
            <div>
              <h2 id="pos-status-title">Coming Soon</h2>
              <app-status-badge label="Coming Soon" variant="info" role="status"></app-status-badge>
            </div>
          </div>
          <p>This tool is currently under development and will be integrated into QA Support Hub when ready.</p>
        </article>

        <article class="availability-panel" aria-labelledby="available-now-title">
          <p class="section-kicker">Available now</p>
          <h2 id="available-now-title">Information only</h2>
          <p>Planned capability areas are shown for context. No POS operations or maintenance controls are available.</p>
          <span class="availability-note"><i class="bi bi-info-circle" aria-hidden="true"></i> No operational controls are exposed.</span>
        </article>
      </section>

      <section class="capabilities-section" aria-labelledby="planned-capabilities-title">
        <div class="section-heading">
          <div>
            <p class="section-kicker">Planned capability areas</p>
            <h2 id="planned-capabilities-title">Future support areas</h2>
          </div>
          <p>These categories are informational only. No actions are available from this page.</p>
        </div>

        <div class="capability-grid">
          @for (capability of capabilities; track capability.id) {
            <article class="capability-card" [attr.aria-labelledby]="'capability-' + capability.id">
              <div class="capability-card__top">
                <span class="capability-icon" aria-hidden="true"><i class="bi" [class]="capability.icon"></i></span>
                <app-status-badge [label]="capabilityStatusLabel(capability)" variant="info" role="status"></app-status-badge>
              </div>
              <h3 [id]="'capability-' + capability.id">{{ capability.title }}</h3>
              <p>{{ capability.description }}</p>
              <ul>
                @for (example of capability.examples; track example) {
                  <li>{{ example }}</li>
                }
              </ul>
            </article>
          }
        </div>
      </section>

      <a routerLink="/" class="back-link">
        <i class="bi bi-arrow-left" aria-hidden="true"></i>
        <span>Back to QA Support Hub</span>
      </a>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; background: var(--surface-page); }
    .pos-page { width: min(100%, 1280px); box-sizing: border-box; margin: 0 auto; padding: calc(var(--navbar-height) + var(--space-6)) var(--space-6) var(--space-8); }
    .status-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-5); margin-bottom: var(--space-7); }
    .status-panel, .availability-panel { min-width: 0; padding: var(--space-5); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); }
    .status-panel { box-shadow: inset 3px 0 0 var(--state-info-fg); }
    .section-kicker { margin: 0 0 var(--space-2); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    .status-panel__heading { display: flex; align-items: center; gap: var(--space-3); }
    .status-panel__heading h2, .availability-panel h2, .section-heading h2 { margin: 0; color: var(--text-primary); font-size: var(--text-xl); line-height: var(--leading-tight); }
    .status-panel__heading h2 { margin-bottom: var(--space-2); }
    .status-panel__icon { display: grid; flex: 0 0 42px; place-items: center; width: 42px; height: 42px; border-radius: var(--radius-md); background: var(--state-info-bg); color: var(--state-info-fg); font-size: 1.2rem; }
    .status-panel p:not(.section-kicker), .availability-panel p:not(.section-kicker), .section-heading > p { margin: var(--space-3) 0 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .availability-panel { background: var(--surface-raised); }
    .availability-note { display: inline-flex; align-items: center; gap: var(--space-2); margin-top: var(--space-4); color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); }
    .capabilities-section { margin-bottom: var(--space-7); }
    .section-heading { display: flex; align-items: end; justify-content: space-between; gap: var(--space-5); margin-bottom: var(--space-4); }
    .section-heading > p { max-width: 520px; margin: 0; }
    .capability-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--space-4); }
    .capability-card { display: flex; min-width: 0; min-height: 260px; flex-direction: column; padding: var(--space-5); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); }
    .capability-card__top { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); margin-bottom: var(--space-4); }
    .capability-icon { display: grid; place-items: center; width: 36px; height: 36px; border-radius: var(--radius-md); background: var(--accent-soft); color: var(--text-accent); font-size: 1.1rem; }
    .capability-card h3 { margin: 0 0 var(--space-2); color: var(--text-primary); font-size: var(--text-lg); line-height: var(--leading-tight); }
    .capability-card p { margin: 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .capability-card ul { display: grid; gap: var(--space-2); margin: auto 0 0; padding: var(--space-4) 0 0 var(--space-4); color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .back-link { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--text-accent); font-size: var(--text-sm); font-weight: var(--weight-bold); text-decoration: none; }
    .back-link:hover { color: var(--accent-hover); }
    .back-link:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }
    @media (max-width: 1000px) { .capability-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 700px) { .pos-page { padding: calc(var(--navbar-height) + var(--space-5)) var(--space-4) var(--space-7); } .status-grid, .capability-grid { grid-template-columns: 1fr; } .section-heading { align-items: start; flex-direction: column; gap: var(--space-3); } }
  `]
})
export class PosMaintenancePlaceholderComponent {
  readonly capabilities = POS_CAPABILITIES;

  capabilityStatusLabel(capability: PosCapability): string {
    switch (capability.status) {
      case 'pending': return 'Planned';
      case 'read-only': return 'Read-only candidate';
      case 'state-changing': return 'State-changing candidate';
      case 'destructive': return 'Destructive candidate';
      case 'unavailable': return 'Unavailable';
      default: return 'Informational';
    }
  }
}
