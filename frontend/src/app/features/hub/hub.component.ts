import { Component, computed, inject } from '@angular/core';
import { BrandMarkComponent, EmptyStateComponent, ToolCardComponent } from '../../shared/ui';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { QaToolDefinition } from '../../core/models';
import { MotionService } from '../../core/services/motion.service';
import { HubSceneComponent } from './hub-scene/hub-scene.component';
import { QA_TOOL_REGISTRY } from './tool-registry';
import { ThemeService } from '../../core/services/theme.service';
import { APP_ASSETS, themedAsset } from '../../core/config/app-assets';

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
          <div class="hub-hero__copy">
            <!-- Product and company lockups read as peers: one plate, one
                 shared height, one optical centerline. -->
            <div class="hub-hero__lockups">
              <app-brand-mark [src]="rmsMark()" alt="RMS+" size="2.75rem" width="10.27rem"></app-brand-mark>
              <span class="hub-hero__rule" aria-hidden="true"></span>
              <app-brand-mark [src]="dbsMark()" alt="Digital Business Systems" size="2.75rem" width="8.14rem"></app-brand-mark>
            </div>

            <h1 id="hub-title">RMS+ Support Hub</h1>
            <p class="hub-hero__description">QA, order, and support tooling in one workspace.</p>

            <div class="hub-hero__meta" aria-label="Workspace status">
              <div class="hub-meta__item">
                <i class="bi bi-check2-circle hub-meta__icon" aria-hidden="true"></i>
                <span class="hub-meta__copy">
                  <span class="hub-meta__label">Workspace status</span>
                  <strong>Ready</strong>
                </span>
              </div>
              <div class="hub-meta__item">
                <i class="bi bi-shield-check hub-meta__icon" aria-hidden="true"></i>
                <span class="hub-meta__copy">
                  <span class="hub-meta__label">Default lane</span>
                  <strong>Testing</strong>
                </span>
              </div>
            </div>

          </div>

          <aside class="hub-hero__rail" aria-label="Tool availability">
            <div class="hub-rail__header">
              <span class="hub-rail__title"><i class="bi bi-grid-3x3-gap" aria-hidden="true"></i>Tool availability</span>
              <span class="hub-rail__count">{{ tools.length }} surfaces</span>
            </div>
            <ul class="hub-hero__signals">
              @for (tool of tools; track tool.id) {
                <li
                  class="hub-signal"
                  [class.hub-signal--pending]="tool.status === 'migration-pending'"
                  [class.hub-signal--readonly]="tool.status === 'read-only'">
                  <span class="hub-signal__icon" aria-hidden="true"><i class="bi" [class]="tool.icon"></i></span>
                  <span class="hub-signal__copy">
                    <span class="hub-signal__label">{{ tool.title }}</span>
                    <span class="hub-signal__state">
                      <span class="hub-signal__dot" aria-hidden="true"></span>
                      {{ tool.status === 'migration-pending' ? 'Coming Soon' : tool.status === 'read-only' ? 'Read-only' : 'Available' }}
                    </span>
                  </span>
                </li>
              }
            </ul>
          </aside>
        </div>
      </section>

      <div class="hub-page__inner">
        <section class="hub-tools" aria-labelledby="hub-tools-title">
          <div class="hub-tools__heading">
            <div>
              <p class="hub-tools__eyebrow"><i class="bi bi-command" aria-hidden="true"></i>Workspace directory</p>
              <h2 id="hub-tools-title">Choose a tool</h2>
            </div>
            <p class="hub-tools__hint"><i class="bi bi-arrow-down-right" aria-hidden="true"></i>Select a workspace to continue.</p>
          </div>

          @if (tools.length > 0) {
            <div class="hub-tools__grid" [class.hub-tools__grid--motion]="!motion.reducedMotion()">
              @for (tool of tools; track tool.id) {
                <app-tool-card
                  [title]="tool.title"
                  [description]="tool.description"
                  [route]="tool.route"
                  [externalRoute]="tool.externalRoute || null"
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

    /* Two bands in one viewport: a fixed hero carrying both brand lockups and
       an elastic tool directory, so the page never needs to scroll. */
    .hub-page { display: flex; min-height: 100dvh; flex-direction: column; background: var(--surface-page); }

    .hub-hero { position: relative; flex: 0 0 auto; overflow: hidden; padding: calc(var(--navbar-height) + var(--space-4)) var(--page-padding-inline) var(--space-5); border-bottom: 1px solid var(--divider); isolation: isolate; }
    .hub-hero__inner { position: relative; z-index: 1; display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(280px, .8fr); align-items: stretch; gap: clamp(var(--space-5), 4vw, var(--space-7)); width: min(100%, 1240px); margin: 0 auto; }
    .hub-page__inner { display: flex; flex: 1; min-height: 0; width: min(100%, 1240px); margin: 0 auto; padding: var(--section-gap) var(--page-padding-inline); }
    .hub-tools { display: flex; flex: 1; flex-direction: column; }
    .hub-hero__copy { display: flex; min-width: 0; flex-direction: column; justify-content: center; }
    .hub-hero__lockups { display: flex; align-self: start; align-items: center; flex-wrap: wrap; gap: var(--space-4); margin-bottom: var(--space-4); padding: var(--space-3) var(--space-4); border: 1px solid var(--card-border); border-radius: var(--radius-lg); background: var(--card-sheen), var(--surface-interactive); }
    .hub-hero__rule { width: 1px; height: 2.25rem; background: var(--divider); }
    .hub-tools__eyebrow { display: flex; align-items: center; gap: var(--space-2); margin: 0 0 var(--space-1); color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    .hub-tools__eyebrow i { font-size: .9rem; }
    .hub-hero h1 { max-width: 15ch; margin: 0; color: var(--text-primary); font-size: clamp(2rem, 4vw, 2.9rem); font-weight: var(--weight-heavy); letter-spacing: -.035em; line-height: 1.02; }
    .hub-hero__description { max-width: 56ch; margin: var(--space-3) 0 0; color: var(--text-secondary); font-size: var(--text-md); line-height: var(--leading-normal); }

    .hub-hero__meta { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-3); max-width: 560px; margin-top: var(--space-4); }
    .hub-meta__item { display: flex; align-items: center; gap: var(--space-3); min-width: 0; padding-top: var(--space-3); border-top: 1px solid var(--divider); }
    .hub-meta__icon { flex: 0 0 auto; color: var(--text-accent); font-size: 1.15rem; }
    .hub-meta__copy { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
    .hub-meta__label { overflow: hidden; color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); letter-spacing: .04em; text-overflow: ellipsis; text-transform: uppercase; white-space: nowrap; }
    .hub-meta__copy strong { color: var(--text-primary); font-size: var(--text-sm); }

    .hub-hero__rail { align-self: stretch; min-width: 0; padding: var(--panel-padding); border: 1px solid var(--card-border); border-radius: var(--card-radius); background: var(--card-surface-quiet); box-shadow: var(--card-shadow); }
    .hub-rail__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); padding-bottom: var(--panel-padding-compact); border-bottom: 1px solid var(--divider); }
    .hub-rail__title { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--text-primary); font-size: var(--text-sm); font-weight: var(--weight-bold); }
    .hub-rail__count { color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); white-space: nowrap; }
    .hub-hero__signals { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
    .hub-signal { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: var(--space-3); padding: var(--space-3) 0; border-bottom: 1px solid var(--divider); font-size: var(--text-xs); }
    .hub-signal:last-child { border-bottom: 0; padding-bottom: 0; }
    .hub-signal__icon { display: grid; width: 30px; height: 30px; place-items: center; border: 1px solid var(--card-border); border-radius: var(--radius-sm); background: var(--surface-interactive); color: var(--text-accent); }
    .hub-signal__copy { display: flex; min-width: 0; flex-direction: column; gap: 3px; }
    .hub-signal__label { overflow: hidden; color: var(--text-primary); font-weight: var(--weight-semibold); text-overflow: ellipsis; white-space: nowrap; }
    .hub-signal__state { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--state-success-fg); font-weight: var(--weight-bold); }
    .hub-signal__dot { width: 5px; height: 5px; border-radius: 50%; background: currentColor; }
    .hub-signal--pending .hub-signal__state, .hub-signal--pending .hub-signal__icon { color: var(--text-muted); }
    .hub-signal--readonly .hub-signal__state, .hub-signal--readonly .hub-signal__icon { color: var(--state-info-fg); }

    .hub-tools__heading { display: flex; align-items: end; justify-content: space-between; gap: var(--space-5); margin-bottom: var(--space-4); }
    .hub-tools h2 { margin: 0; color: var(--text-primary); font-size: var(--text-xl); line-height: var(--leading-tight); }
    .hub-tools__hint { margin: 0; color: var(--text-muted); font-size: var(--text-sm); }

    .hub-tools__grid { display: grid; flex: 1; grid-template-columns: repeat(3, minmax(0, 1fr)); grid-auto-rows: minmax(0, 1fr); align-items: stretch; gap: var(--card-gap); }
    .hub-tools__grid > app-tool-card { height: 100%; }
    .hub-tools__grid--motion > app-tool-card { opacity: 0; animation: hub-card-in var(--d-slow) var(--ease-out) forwards; }
    .hub-tools__grid > app-tool-card:nth-child(1) { animation-delay: 0ms; }
    .hub-tools__grid > app-tool-card:nth-child(2) { animation-delay: 70ms; }
    .hub-tools__grid > app-tool-card:nth-child(3) { animation-delay: 140ms; }
    .hub-tools__empty { display: grid; flex: 1; place-items: center; border: 1px solid var(--card-border); border-radius: var(--card-radius); background: var(--surface-panel); }

    @keyframes hub-card-in {
      from { opacity: 0; transform: translateY(12px); }
      to { opacity: 1; transform: translateY(0); }
    }

    @media (min-width: 1025px) and (min-height: 720px) {
      .hub-page { height: 100dvh; overflow: hidden; }
    }

    @media (max-width: 1024px) {
      .hub-tools__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 780px) {
      .hub-hero__inner { grid-template-columns: 1fr; gap: var(--space-5); }
    }
    @media (max-width: 680px) {
      .hub-hero { padding: calc(var(--navbar-height) + var(--space-4)) var(--space-4) var(--space-5); }
      .hub-hero__rule { display: none; }
      .hub-hero__meta { gap: var(--space-2); }
      .hub-tools__heading { align-items: flex-start; flex-direction: column; gap: var(--space-2); }
      .hub-tools__grid { grid-template-columns: 1fr; grid-auto-rows: auto; gap: var(--panel-gap); }
    }

  `]
})
export class HubComponent {
  readonly assets = APP_ASSETS;
  readonly motion = inject(MotionService);
  private readonly theme = inject(ThemeService);
  tools: readonly QaToolDefinition[] = QA_TOOL_REGISTRY;

  /** Both lockups ship per colourway; follow the active theme. */
  readonly rmsMark = computed(() => themedAsset(this.assets.brand.rms, this.theme.theme()));
  readonly dbsMark = computed(() => themedAsset(this.assets.brand.dbs, this.theme.theme()));
}
