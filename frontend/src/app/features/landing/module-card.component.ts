import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ModuleDto, EnvironmentDto } from '../../core/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BrandMarkComponent, UiButtonComponent, UiCardComponent } from '../../shared/ui';
import { APP_ASSETS } from '../../core/config/app-assets';

/**
 * Online Order module card. Shares the `--card-*` geometry, border, and hover
 * language with the hub tool card, but stays its own component because it
 * carries live environment selection rather than a single navigation action.
 *
 * Presentation only: module load, capability data, environment selection, and
 * route targets are unchanged.
 */
@Component({
  selector: 'app-module-card',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, BrandMarkComponent, UiButtonComponent, UiCardComponent],
  template: `
    <ui-card variant="raised" class="module-card" [disabled]="!module.available" [style.--module-accent]="getAccentColor(module.key)">
      <div class="card-head" uiCardHeader>
        <div class="identity">
          @if (getLogoUrl(module.key); as logoUrl) {
            <app-brand-mark class="module-logo" [src]="logoUrl" [alt]="getLogoAlt(module.key)" size="46px" [framed]="true"></app-brand-mark>
          } @else {
            <span class="module-logo-fallback" aria-hidden="true"><i class="bi bi-layers-half"></i></span>
          }
          <div class="identity__text">
            <h3 class="module-title">{{ module.label }}</h3>
            <span class="client-name">{{ module.client }}</span>
          </div>
        </div>
        <app-status-badge *ngIf="!module.available" label="Coming Soon" variant="secondary" role="status"></app-status-badge>
        <app-status-badge *ngIf="module.available" label="Active Module" variant="success" role="status"></app-status-badge>
      </div>

      <div class="card-support" *ngIf="supportLabels.length" aria-label="Module support">
        <span class="support-chip" *ngFor="let label of supportLabels">{{ label }}</span>
      </div>

      <div class="card-envs" *ngIf="module.available && module.environments.length > 0">
        @for (env of module.environments; track env.key) {
          <ui-button variant="secondary" size="sm" class="env-btn" [ariaLabel]="'Select environment ' + env.key" (pressed)="onSelectEnv(env)">
            <span class="env-content">
              <span class="env-icon"><i class="bi" [class]="env.icon" aria-hidden="true"></i></span>
              <span class="env-meta">
              <span class="env-key">{{ env.key }}</span>
              <span class="env-desc">{{ env.description }}</span>
              </span>
              <app-status-badge [label]="env.statusLabel" [variant]="env.environment === 'Production' ? 'success' : 'info'" role="status"></app-status-badge>
            </span>
          </ui-button>
        }
      </div>

      <div class="card-footer" uiCardFooter *ngIf="module.available">
        <a [routerLink]="getModuleRoute(module.key)" class="enter-link">
          <span>Open Module</span>
          <i class="bi bi-arrow-right" aria-hidden="true"></i>
        </a>
      </div>
    </ui-card>
  `,
  styles: [`
    :host { display: block; min-width: 0; height: 100%; }

    /* Equal-height peers: the article stretches and the body absorbs the slack,
     * so the Open Module action lines up across cards with different
     * environment counts. */
    :host ::ng-deep .module-card .ui-card {
      display: flex;
      height: 100%;
      flex-direction: column;
      overflow: visible;
      border-color: var(--card-border);
      border-radius: var(--card-radius);
      background: var(--card-surface);
      box-shadow: var(--card-shadow);
      transition: border-color var(--transition-fast), box-shadow var(--transition-normal), transform var(--transition-normal);
    }
    :host ::ng-deep .module-card .ui-card:hover { transform: translateY(var(--card-lift)); border-color: var(--card-border-hover); box-shadow: var(--card-shadow-hover); }
    :host ::ng-deep .module-card .ui-card::before { inset: 0 0 auto 0; width: auto; height: 2px; background: var(--module-accent); opacity: .55; }
    :host ::ng-deep .module-card .ui-card__body { display: flex; flex: 1; flex-direction: column; gap: var(--card-gap); }
    :host ::ng-deep .module-card .ui-card--disabled:hover { transform: none; box-shadow: var(--card-shadow); }

    .card-head { display: flex; justify-content: space-between; align-items: center; gap: var(--space-3); }
    .identity { display: flex; min-width: 0; align-items: center; gap: var(--space-4); }
    .identity__text { min-width: 0; }
    .module-logo { flex: 0 0 46px; }
    .module-logo-fallback { display: inline-flex; width: 46px; height: 46px; flex: 0 0 46px; align-items: center; justify-content: center; border: 1px solid var(--card-border); border-radius: var(--radius-md); background: var(--surface-interactive); color: var(--text-muted); font-size: 1.35rem; }
    .module-title { margin: 0; color: var(--text-primary); font-size: var(--text-lg); font-weight: var(--weight-bold); line-height: var(--leading-tight); overflow-wrap: anywhere; }
    .client-name { color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); }

    .card-support { display: flex; flex-wrap: wrap; gap: var(--space-2); }
    .support-chip { padding: 5px 10px; border: 1px solid var(--card-border); border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: var(--text-xs); line-height: 1.25; }

    /* Environment list absorbs the leftover height so the footer stays pinned. */
    .card-envs { display: flex; flex: 1; flex-direction: column; gap: var(--space-3); }
    :host ::ng-deep ui-button.env-btn { display: block; width: 100%; }
    :host ::ng-deep ui-button.env-btn .ui-button { width: 100%; min-height: 68px; justify-content: flex-start; text-align: left; border-color: var(--card-border); border-radius: var(--radius-md); background: var(--surface-interactive); }
    :host ::ng-deep ui-button.env-btn .ui-button__projected { overflow: visible; text-overflow: clip; white-space: normal; }
    :host ::ng-deep ui-button.env-btn .ui-button:hover:not(:disabled) { transform: translateX(4px); border-color: var(--module-accent); background: var(--surface-hover); }
    .env-content { display: flex; align-items: center; width: 100%; gap: var(--space-3); }
    .env-icon { flex: 0 0 auto; font-size: 1.25rem; color: var(--module-accent); }
    .env-meta { display: flex; min-width: 0; flex: 1; flex-direction: column; }
    .env-key { color: var(--text-primary); font-size: 0.88rem; font-weight: var(--weight-bold); overflow-wrap: anywhere; }
    .env-desc { color: var(--text-secondary); font-size: var(--text-xs); overflow-wrap: anywhere; }

    .card-footer { display: flex; justify-content: flex-end; padding-top: var(--space-2); }
    .enter-link { display: inline-flex; align-items: center; gap: var(--space-2); color: var(--text-accent); text-decoration: none; font-size: var(--text-sm); font-weight: var(--weight-bold); }
    .enter-link i { transition: transform var(--transition-fast); }
    .enter-link:hover i { transform: translateX(3px); }
    .enter-link:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }

    @media (prefers-reduced-motion: reduce) {
      :host-context(html:not([data-motion="full"])) ::ng-deep .module-card .ui-card:hover,
      :host-context(html:not([data-motion="full"])) .enter-link:hover i { transform: none; }
    }
    :host-context(html[data-motion="reduce"]) ::ng-deep .module-card .ui-card:hover,
    :host-context(html[data-motion="reduce"]) .enter-link:hover i { transform: none; }
  `]
})
export class ModuleCardComponent {
  readonly assets = APP_ASSETS;
  @Input() module!: ModuleDto;
  @Output() selectEnv = new EventEmitter<EnvironmentDto>();

  /** Display-only summary of the live capability flags already on ModuleDto;
   * routing and guarding still read the flags themselves. */
  get supportLabels(): string[] {
    const capabilities = this.module?.capabilities;
    if (!capabilities) return [];
    return [
      capabilities.itemLookup ? 'Item lookup' : '',
      capabilities.consumerLookup ? 'Consumer lookup' : '',
      capabilities.orderRequests ? 'Order Requests' : '',
      capabilities.cancel ? 'Cancel' : '',
      capabilities.resend ? 'Resend' : ''
    ].filter(Boolean);
  }

  onSelectEnv(env: EnvironmentDto) {
    this.selectEnv.emit(env);
  }

  getModuleRoute(key: string): string[] {
    if (key === 'ghc_unicommerce') {
      return ['/tools/online-orders/modules', key, 'unicommerce'];
    }
    return ['/tools/online-orders/modules', key, 'order'];
  }

  getLogoUrl(key: string): string {
    return this.assets.modules.byKey[key] ?? '';
  }

  getLogoAlt(key: string): string {
    return this.assets.modules.altByKey[key] ?? '';
  }

  getAccentColor(key: string): string {
    switch (key) {
      case 'upc_ecommerce': return 'var(--accent-amber)';
      case 'ghc_unicommerce': return 'var(--accent-violet)';
      default: return 'var(--accent-indigo)';
    }
  }
}
