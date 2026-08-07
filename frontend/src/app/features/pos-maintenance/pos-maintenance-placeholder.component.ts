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
        subtitle="A centralized support workspace for approved POS diagnostics, backup, configuration, service, and maintenance workflows.">
      </app-page-header>

      <section class="status-grid" aria-label="Migration status and availability">
        <article class="status-panel" aria-labelledby="migration-status-title">
          <p class="section-kicker">Migration status</p>
          <div class="status-panel__heading">
            <span class="status-panel__icon" aria-hidden="true"><i class="bi bi-hourglass-split"></i></span>
            <div>
              <h2 id="migration-status-title">Migration Pending</h2>
              <app-status-badge label="Migration Pending" variant="warning" role="status"></app-status-badge>
            </div>
          </div>
          <p>The standalone POS Maintenance Tool has not yet been supplied for review. This workspace is ready to receive the source intake, but no POS operation can run from here.</p>
        </article>

        <article class="availability-panel" aria-labelledby="available-now-title">
          <p class="section-kicker">Available now</p>
          <h2 id="available-now-title">Migration information only</h2>
          <p>Users can review the planned scope, source requirements, and readiness state. Machine, database, service, file, and command operations are not available.</p>
          <span class="availability-note"><i class="bi bi-info-circle" aria-hidden="true"></i> No operational controls are exposed.</span>
        </article>
      </section>

      <section class="capabilities-section" aria-labelledby="planned-capabilities-title">
        <div class="section-heading">
          <div>
            <p class="section-kicker">Planned capabilities</p>
            <h2 id="planned-capabilities-title">Capability areas for future review</h2>
          </div>
          <p>These are informational categories only. The actual operations, permissions, and implementation boundary require source review.</p>
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

      <section class="intake-panel" aria-labelledby="source-intake-title">
        <div class="intake-panel__intro">
          <span class="intake-panel__icon" aria-hidden="true"><i class="bi bi-folder2-open"></i></span>
          <div>
            <p class="section-kicker">Source required before migration</p>
            <h2 id="source-intake-title">Start with the original POS project</h2>
            <p>Session 11 needs the source project, build and dependency files, safe configuration samples, and required UI assets before it can assess the real maintenance surface.</p>
          </div>
        </div>
        <ul class="intake-list">
          <li><strong>Source project</strong><span>Original POS Maintenance Tool source code, solution/project files, and confirmed repository or filesystem path.</span></li>
          <li><strong>Configuration</strong><span>Safe samples, environment details, and protected settings without credentials.</span></li>
          <li><strong>Dependencies and assets</strong><span>Package files, external tools, icons, fonts, and required application assets.</span></li>
        </ul>
      </section>

      <section class="readiness-panel" aria-labelledby="readiness-title">
        <div class="section-heading section-heading--compact">
          <div>
            <p class="section-kicker">Migration readiness</p>
            <h2 id="readiness-title">Current entry state</h2>
          </div>
          <p>No percentage or inferred completion is shown before source review.</p>
        </div>
        <dl class="readiness-grid">
          @for (item of readinessItems; track item.label) {
            <div class="readiness-item">
              <dt>{{ item.label }}</dt>
              <dd><span class="readiness-value" [class]="'readiness-value--' + item.tone">{{ item.value }}</span></dd>
            </div>
          }
        </dl>
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
    .status-panel { border-color: var(--state-warning-border); box-shadow: inset 3px 0 0 var(--state-warning-fg); }
    .section-kicker { margin: 0 0 var(--space-2); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    .status-panel__heading { display: flex; align-items: center; gap: var(--space-3); }
    .status-panel__heading h2, .availability-panel h2, .section-heading h2, .intake-panel h2, .readiness-panel h2 { margin: 0; color: var(--text-primary); font-size: var(--text-xl); line-height: var(--leading-tight); }
    .status-panel__heading h2 { margin-bottom: var(--space-2); }
    .status-panel__icon, .intake-panel__icon { display: grid; flex: 0 0 42px; place-items: center; width: 42px; height: 42px; border-radius: var(--radius-md); background: var(--state-warning-bg); color: var(--state-warning-fg); font-size: 1.2rem; }
    .status-panel p:not(.section-kicker), .availability-panel p:not(.section-kicker), .section-heading > p, .intake-panel__intro p:not(.section-kicker) { margin: var(--space-3) 0 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .availability-panel { background: var(--surface-raised); }
    .availability-note { display: inline-flex; align-items: center; gap: var(--space-2); margin-top: var(--space-4); color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); }
    .capabilities-section { margin-bottom: var(--space-7); }
    .section-heading { display: flex; align-items: end; justify-content: space-between; gap: var(--space-5); margin-bottom: var(--space-4); }
    .section-heading > p { max-width: 520px; margin: 0; }
    .section-heading--compact { align-items: start; margin-bottom: var(--space-4); }
    .capability-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--space-4); }
    .capability-card { display: flex; min-width: 0; min-height: 260px; flex-direction: column; padding: var(--space-5); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); }
    .capability-card__top { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); margin-bottom: var(--space-4); }
    .capability-icon { display: grid; place-items: center; width: 36px; height: 36px; border-radius: var(--radius-md); background: var(--accent-soft); color: var(--text-accent); font-size: 1.1rem; }
    .capability-card h3 { margin: 0 0 var(--space-2); color: var(--text-primary); font-size: var(--text-lg); line-height: var(--leading-tight); }
    .capability-card p { margin: 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .capability-card ul { display: grid; gap: var(--space-2); margin: auto 0 0; padding: var(--space-4) 0 0 var(--space-4); color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .intake-panel, .readiness-panel { margin-bottom: var(--space-7); padding: var(--space-5); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); }
    .intake-panel { border-left: 3px solid var(--accent); }
    .intake-panel__intro { display: flex; gap: var(--space-4); }
    .intake-list { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--space-4); margin: var(--space-5) 0 0; padding: var(--space-4) 0 0; border-top: 1px solid var(--divider); list-style: none; }
    .intake-list li { display: grid; gap: var(--space-1); min-width: 0; }
    .intake-list strong { color: var(--text-primary); font-size: var(--text-sm); }
    .intake-list span { color: var(--text-secondary); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .readiness-panel { background: var(--surface-raised); }
    .readiness-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: var(--space-3); margin: 0; }
    .readiness-item { min-width: 0; padding: var(--space-3); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-panel); }
    .readiness-item dt { color: var(--text-secondary); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .readiness-item dd { margin: var(--space-2) 0 0; }
    .readiness-value { color: var(--text-primary); font-size: var(--text-sm); font-weight: var(--weight-bold); line-height: var(--leading-normal); }
    .readiness-value--required { color: var(--state-warning-fg); }
    .readiness-value--pending { color: var(--text-accent); }
    .readiness-value--defined { color: var(--state-success-fg); }
    .readiness-value--not-started { color: var(--text-muted); }
    .back-link { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--text-accent); font-size: var(--text-sm); font-weight: var(--weight-bold); text-decoration: none; }
    .back-link:hover { color: var(--accent-hover); }
    .back-link:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }
    @media (max-width: 1000px) { .capability-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } .readiness-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
    @media (max-width: 700px) { .pos-page { padding: calc(var(--navbar-height) + var(--space-5)) var(--space-4) var(--space-7); } .status-grid, .capability-grid, .intake-list, .readiness-grid { grid-template-columns: 1fr; } .section-heading { align-items: start; flex-direction: column; gap: var(--space-3); } .intake-panel__intro { align-items: flex-start; } .readiness-grid { gap: var(--space-2); } }
  `]
})
export class PosMaintenancePlaceholderComponent {
  readonly capabilities = POS_CAPABILITIES;
  readonly readinessItems = [
    { label: 'Source Code', value: 'Required', tone: 'required' },
    { label: 'Operation Inventory', value: 'Pending Source Review', tone: 'pending' },
    { label: 'Security Classification', value: 'Pending Source Review', tone: 'pending' },
    { label: 'Target Architecture', value: 'Defined', tone: 'defined' },
    { label: 'Implementation', value: 'Not Started', tone: 'not-started' }
  ] as const;

  capabilityStatusLabel(capability: PosCapability): string {
    switch (capability.status) {
      case 'read-only': return 'Read-only candidate';
      case 'state-changing': return 'State-changing candidate';
      case 'destructive': return 'Destructive candidate';
      case 'unavailable': return 'Unavailable';
      default: return 'Requires source review';
    }
  }
}
