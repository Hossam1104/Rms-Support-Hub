import { Component } from '@angular/core';
import { BrandMarkComponent, EmptyStateComponent, ToolCardComponent } from '../../shared/ui';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { QaToolDefinition } from '../../core/models';
import { HubSceneComponent } from './hub-scene/hub-scene.component';
import { QA_TOOL_REGISTRY } from './tool-registry';
import { APP_ASSETS } from '../../core/config/app-assets';

@Component({
  selector: 'app-hub',
  standalone: true,
  imports: [NavbarComponent, BrandMarkComponent, ToolCardComponent, EmptyStateComponent, HubSceneComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="hub-page" aria-labelledby="hub-title">
      <section class="hub-hero">
        <!-- Decorative only: the hero reads identically without WebGL. -->
        <app-hub-scene></app-hub-scene>

        <div class="hub-hero__inner">
          <div class="hub-hero__identity">
            <app-brand-mark [src]="assets.brand.rms" alt="RMS+" size="5rem"></app-brand-mark>
            <div>
              <p class="hub-eyebrow"><span aria-hidden="true"></span>QA Engineering Workspace</p>
              <h1 id="hub-title">RMS+ Support Hub</h1>
              <p class="hub-hero__description">
                A focused workspace for QA engineering, prompt refinement, order operations, and support tooling.
              </p>
            </div>
          </div>
          <p class="hub-attribution">Built by <app-brand-mark [src]="assets.brand.dbs" alt="DBS" size="2rem" [framed]="true"></app-brand-mark></p>
          <ul class="hub-hero__signals">
            @for (tool of tools; track tool.id) {
              <li class="hub-signal" [class.hub-signal--pending]="tool.status === 'migration-pending'">
                <span class="hub-signal__dot" aria-hidden="true"></span>
                <span class="hub-signal__label">{{ tool.title }}</span>
                <span class="hub-signal__state">{{ tool.status === 'migration-pending' ? 'Coming Soon' : 'Available' }}</span>
              </li>
            }
          </ul>
        </div>
      </section>

      <div class="hub-page__inner">
        <section class="hub-tools" aria-labelledby="hub-tools-title">
          <div class="hub-tools__heading">
            <div>
              <p class="hub-tools__eyebrow">Workspace directory</p>
              <h2 id="hub-tools-title">Choose a tool</h2>
            </div>
            <p class="hub-tools__hint">Select a workspace to continue.</p>
          </div>

          @if (tools.length > 0) {
            <div class="hub-tools__grid">
              @for (tool of tools; track tool.id) {
                <app-tool-card
                  [title]="tool.title"
                  [description]="tool.description"
                  [route]="tool.route"
                  [icon]="tool.icon"
                  [accent]="tool.accent"
                  [status]="tool.status"
                  [actionLabel]="tool.actionLabel"
                  [capabilities]="tool.capabilities"
                  [availabilityMessage]="tool.availabilityMessage">
                </app-tool-card>
              }
            </div>
          } @else {
            <div class="hub-tools__empty">
              <app-empty-state
                icon="bi-grid-3x3-gap"
                title="No QA tools are currently configured.">
              </app-empty-state>
            </div>
          }
        </section>
      </div>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100%; }
    .hub-page { min-height: 100vh; padding-bottom: var(--section-gap); background: var(--surface-page); }

    /* Hero: the Three.js canvas fills this box absolutely, content sits above. */
    .hub-hero { position: relative; overflow: hidden; margin-bottom: var(--space-7); padding: calc(var(--navbar-height) + var(--space-8)) var(--space-6) var(--space-8); border-bottom: 1px solid var(--divider); isolation: isolate; }
    .hub-hero__inner { position: relative; z-index: 1; width: min(100%, 1240px); margin: 0 auto; }
    .hub-page__inner { width: min(100%, 1240px); margin: 0 auto; padding: 0 var(--page-padding-inline); }
    .hub-hero__identity { display: flex; align-items: center; gap: var(--space-5); }
    .hub-hero__identity > div { min-width: 0; }
    .hub-attribution { display: flex; align-items: center; gap: var(--space-2); margin: var(--space-5) 0 0; color: var(--text-muted); font-size: var(--text-sm); }

    .hub-eyebrow, .hub-tools__eyebrow { display: flex; align-items: center; gap: var(--space-2); margin: 0 0 var(--space-3); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    .hub-eyebrow span { width: 7px; height: 7px; border-radius: 50%; background: var(--accent); box-shadow: 0 0 0 4px var(--accent-soft); }
    .hub-hero h1 { max-width: 15ch; margin: 0; color: var(--text-primary); font-size: clamp(2.4rem, 5vw, 3.6rem); font-weight: var(--weight-heavy); letter-spacing: -.02em; line-height: 1.04; }
    .hub-hero__description { max-width: 62ch; margin: var(--space-4) 0 0; color: var(--text-secondary); font-size: var(--text-md); line-height: var(--leading-normal); }

    .hub-hero__signals { display: flex; flex-wrap: wrap; gap: var(--space-3); margin: var(--space-6) 0 0; padding: 0; list-style: none; }
    .hub-signal { display: inline-flex; align-items: center; gap: var(--space-2); padding: 6px 12px; border: 1px solid var(--card-border); border-radius: var(--radius-pill); background: var(--card-surface-quiet); font-size: var(--text-xs); }
    .hub-signal__dot { width: 6px; height: 6px; border-radius: 50%; background: var(--state-success-fg); }
    .hub-signal__label { color: var(--text-primary); font-weight: var(--weight-semibold); }
    .hub-signal__state { color: var(--state-success-fg); font-weight: var(--weight-bold); }
    .hub-signal--pending .hub-signal__dot, .hub-signal--pending .hub-signal__state { background: none; color: var(--text-muted); }
    .hub-signal--pending .hub-signal__dot { background: var(--text-muted); }

    .hub-tools__heading { display: flex; align-items: end; justify-content: space-between; gap: var(--space-5); margin-bottom: var(--space-5); }
    .hub-tools__eyebrow { margin-bottom: var(--space-1); color: var(--text-muted); }
    .hub-tools h2 { margin: 0; color: var(--text-primary); font-size: var(--text-xl); line-height: var(--leading-tight); }
    .hub-tools__hint { margin: 0; color: var(--text-muted); font-size: var(--text-sm); }

    /* Equal-height peers: 1fr rows stretch every card to the tallest one. */
    .hub-tools__grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); grid-auto-rows: 1fr; align-items: stretch; gap: var(--card-gap); }
    .hub-tools__grid > app-tool-card { height: 100%; opacity: 0; animation: hub-card-in var(--d-slow) var(--ease-out) forwards; }
    .hub-tools__grid > app-tool-card:nth-child(1) { animation-delay: 0ms; }
    .hub-tools__grid > app-tool-card:nth-child(2) { animation-delay: 70ms; }
    .hub-tools__grid > app-tool-card:nth-child(3) { animation-delay: 140ms; }
    .hub-tools__empty { border: 1px solid var(--card-border); border-radius: var(--card-radius); background: var(--surface-panel); }
    @keyframes hub-card-in {
      from { opacity: 0; transform: translateY(12px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (max-width: 1024px) {
      .hub-tools__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 680px) {
      .hub-hero { margin-bottom: var(--space-6); padding: calc(var(--navbar-height) + var(--space-6)) var(--space-4) var(--space-6); }
      .hub-page__inner { padding: 0 var(--page-padding-inline); }
      .hub-hero__identity { align-items: flex-start; gap: var(--space-3); }
      .hub-hero__signals { gap: var(--space-2); }
      .hub-tools__heading { align-items: flex-start; flex-direction: column; gap: var(--space-2); }
      .hub-tools__grid { grid-template-columns: 1fr; grid-auto-rows: auto; gap: var(--panel-gap); }
    }

    @media (prefers-reduced-motion: reduce) {
      :host-context(html:not([data-motion="full"])) .hub-tools__grid > app-tool-card { animation: none; opacity: 1; transform: none; }
    }
    :host-context(html[data-motion="reduce"]) .hub-tools__grid > app-tool-card { animation: none; opacity: 1; transform: none; }
  `]
})
export class HubComponent {
  readonly assets = APP_ASSETS;
  tools: readonly QaToolDefinition[] = QA_TOOL_REGISTRY;
}
