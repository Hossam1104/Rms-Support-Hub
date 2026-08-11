import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  ModuleDto,
  EnvironmentDto,
  EnvironmentHealthMap,
  EnvironmentHealthState,
  environmentHealthKey
} from '../../core/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { BrandMarkComponent, UiButtonComponent, UiCardComponent } from '../../shared/ui';
import { APP_ASSETS } from '../../core/config/app-assets';

/**
 * Online Order module card. It now follows the hub tool card's reading order -
 * mark and status on one row, then title, client, capability chips, and a
 * pinned footer action - so both dashboards present a module the same way. It
 * stays its own component because it also carries live environment selection.
 *
 * Presentation only: module load, capability data, environment selection, and
 * route targets are unchanged.
 */
@Component({
  selector: 'app-module-card',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, BrandMarkComponent, UiButtonComponent, UiCardComponent],
  template: `
    <ui-card variant="raised" class="module-card" [class]="getAccentClass(module.key)" [disabled]="!module.available">
      <div class="module-card__content">
        <div class="module-card__header">
          @if (getLogoUrl(module.key); as logoUrl) {
            <app-brand-mark [src]="logoUrl" [alt]="getLogoAlt(module.key)" size="46px" [framed]="true"></app-brand-mark>
          } @else {
            <span class="module-card__fallback" aria-hidden="true"><i class="bi bi-layers-half"></i></span>
          }
          <app-status-badge
            [label]="module.available ? 'Active Module' : 'Coming Soon'"
            [variant]="module.available ? 'success' : 'secondary'"
            role="status"></app-status-badge>
        </div>

        <h3 class="module-card__title">{{ module.label }}</h3>
        <p class="module-card__client">{{ module.client }}</p>

        <div class="module-card__capabilities" *ngIf="supportLabels.length" aria-label="Module support">
          <span class="module-card__capability" *ngFor="let label of supportLabels">
            <i class="bi" [class]="capabilityIcon(label)" aria-hidden="true"></i>
            <span>{{ label }}</span>
          </span>
        </div>

        <div class="module-card__envs" *ngIf="module.available && module.environments.length > 0">
          <p class="module-card__label">Environments</p>
          @for (env of module.environments; track env.key) {
            <ui-button variant="secondary" size="sm" class="env-btn" [ariaLabel]="'Select environment ' + env.key" (pressed)="onSelectEnv(env)">
              <span class="env-content">
                <span class="env-icon"><i class="bi" [class]="env.icon" aria-hidden="true"></i></span>
                <span class="env-meta">
                  <span class="env-key">{{ env.key }}</span>
                  <span class="env-desc">{{ env.description }}</span>
                </span>
                <span class="env-status">
                  <!-- Lane first, reachability second. The lane badge is config
                       and never changes with the probe result. -->
                  <app-status-badge [label]="env.statusLabel" [variant]="env.environment === 'Production' ? 'success' : 'info'" role="status"></app-status-badge>
                  <span class="env-health" [class]="'env-health--' + healthOf(env)" role="status">
                    <i class="bi" [class]="healthIcon(env)" aria-hidden="true"></i>
                    <span>{{ healthLabel(env) }}</span>
                  </span>
                </span>
              </span>
            </ui-button>
          }
        </div>

        <div class="module-card__footer">
          <p class="module-card__availability" *ngIf="!module.available">Planned module. Its environments appear here once routing is configured.</p>
          <a *ngIf="module.available" [routerLink]="getModuleRoute(module.key)" class="module-card__action">
            <span>Open Module</span>
            <i class="bi bi-arrow-up-right" aria-hidden="true"></i>
          </a>
        </div>
      </div>
    </ui-card>
  `,
  styles: [`
    :host { display: block; min-width: 0; height: 100%; }

    /* Identity: the same two-stop accent contract the hub tool card uses, so
     * peer grids on both dashboards light their top edge from one pair. */
    .module-card--amber { --card-accent-from: var(--tool-amber-from); --card-accent-to: var(--tool-amber-to); }
    .module-card--info { --card-accent-from: var(--tool-info-from); --card-accent-to: var(--tool-info-to); }
    .module-card--brand { --card-accent-from: var(--tool-brand-from); --card-accent-to: var(--tool-brand-to); }
    .module-card--teal { --card-accent-from: var(--tool-teal-from); --card-accent-to: var(--tool-teal-to); }

    /* Equal-height peers: the article stretches and the footer is pinned with
     * margin-top:auto, so Open Module lines up across cards with different
     * environment counts. */
    :host ::ng-deep ui-card.module-card { display: block; height: 100%; }
    :host ::ng-deep .module-card .ui-card {
      display: flex;
      height: 100%;
      flex-direction: column;
      border-color: var(--card-border);
      border-radius: var(--card-radius);
      background: var(--card-surface);
      box-shadow: var(--card-shadow);
      transition: border-color var(--transition-fast), box-shadow var(--transition-normal), transform var(--transition-normal);
    }
    :host ::ng-deep .module-card .ui-card::before { inset: 0 0 auto 0; width: auto; height: 2px; background: linear-gradient(90deg, var(--card-accent-from), var(--card-accent-to)); opacity: .5; transition: opacity var(--transition-fast); }
    :host ::ng-deep .module-card .ui-card:hover { transform: translateY(var(--card-lift)); border-color: var(--card-border-hover); box-shadow: var(--card-shadow-hover); }
    :host ::ng-deep .module-card .ui-card:hover::before { opacity: 1; }
    :host ::ng-deep .module-card .ui-card__body { display: flex; flex: 1; padding: var(--card-padding); }
    :host ::ng-deep .module-card .ui-card--disabled, :host ::ng-deep .module-card .ui-card--disabled:hover { transform: none; box-shadow: var(--card-shadow); }

    .module-card__content { display: flex; width: 100%; min-width: 0; flex-direction: column; }
    .module-card__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); margin-bottom: var(--card-gap); }
    /* "Active Module" is two words: keep the pill on one line and let the
       title below take the wrapping instead. */
    :host ::ng-deep .module-card__header .status-badge { flex-shrink: 0; white-space: nowrap; }
    .module-card__fallback { display: grid; width: 46px; height: 46px; place-items: center; border: 1px solid var(--card-border); border-radius: var(--radius-md); background: var(--surface-interactive); color: var(--text-muted); font-size: 1.35rem; }
    .module-card__title { margin: 0 0 var(--space-1); color: var(--text-primary); font-size: var(--text-lg); font-weight: var(--weight-bold); line-height: var(--leading-tight); overflow-wrap: anywhere; }
    .module-card__client { margin: 0; color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); letter-spacing: .04em; text-transform: uppercase; }

    .module-card__capabilities { display: flex; flex-wrap: wrap; gap: var(--space-2); margin-top: var(--card-gap); }
    .module-card__capability { display: inline-flex; align-items: center; gap: var(--space-2); padding: 5px 10px; border: 1px solid var(--card-border); border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: var(--text-xs); line-height: 1.25; }
    .module-card__capability i { color: var(--text-accent); font-size: .75rem; }

    .module-card__envs { display: flex; flex-direction: column; gap: var(--space-2); margin-top: var(--card-gap); }
    .module-card__label { margin: 0 0 var(--space-1); color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    :host ::ng-deep ui-button.env-btn { display: block; width: 100%; }
    :host ::ng-deep ui-button.env-btn .ui-button { width: 100%; min-height: 58px; justify-content: flex-start; text-align: left; border-color: var(--card-border); border-radius: var(--radius-md); background: var(--surface-interactive); }
    :host ::ng-deep ui-button.env-btn .ui-button__projected { overflow: visible; text-overflow: clip; white-space: normal; }
    :host ::ng-deep ui-button.env-btn .ui-button:hover:not(:disabled) { transform: translateX(4px); border-color: var(--card-accent-from); background: var(--surface-hover); }
    .env-content { display: flex; align-items: center; width: 100%; gap: var(--space-3); }
    .env-icon { flex: 0 0 auto; color: var(--card-accent-from); font-size: 1.15rem; }
    .env-meta { display: flex; min-width: 0; flex: 1; flex-direction: column; }
    .env-key { color: var(--text-primary); font-size: var(--text-sm); font-weight: var(--weight-bold); overflow-wrap: anywhere; }
    .env-desc { color: var(--text-secondary); font-size: var(--text-xs); overflow-wrap: anywhere; }

    /* Two independent facts stacked, never merged into one pill. */
    .env-status { display: flex; flex: 0 0 auto; align-items: flex-end; flex-direction: column; gap: 4px; }
    .env-health { display: inline-flex; align-items: center; gap: 4px; color: var(--text-muted); font-size: .68rem; font-weight: var(--weight-semibold); white-space: nowrap; }
    .env-health i { font-size: .7rem; }
    .env-health--reachable { color: var(--state-success-fg); }
    .env-health--unreachable { color: var(--state-danger-fg); }

    .module-card__footer { margin-top: auto; }
    .module-card__availability { margin: 0; padding-top: var(--card-gap); color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .module-card__action { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); margin-top: var(--card-gap); padding-top: var(--card-gap); border-top: 1px solid var(--divider); color: var(--text-accent); font-size: var(--text-sm); font-weight: var(--weight-bold); text-decoration: none; }
    .module-card__action i { transition: transform var(--transition-fast); }
    .module-card__action:hover i { transform: translate(var(--card-action-shift), var(--card-icon-lift)); }
    .module-card__action:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }

    @media (prefers-reduced-motion: reduce) {
      :host-context(html:not([data-motion="full"])) ::ng-deep .module-card .ui-card:hover,
      :host-context(html:not([data-motion="full"])) .module-card__action:hover i { transform: none; }
    }
    :host-context(html[data-motion="reduce"]) ::ng-deep .module-card .ui-card:hover,
    :host-context(html[data-motion="reduce"]) .module-card__action:hover i { transform: none; }
  `]
})
export class ModuleCardComponent {
  readonly assets = APP_ASSETS;
  @Input() module!: ModuleDto;
  /** Endpoint reachability keyed by `environmentHealthKey`; absent entries are
   * unknown, which is not the same claim as unreachable. */
  @Input() health: EnvironmentHealthMap = new Map();
  /** True while the probe sweep is still in flight, so an absent entry reads
   * as "Checking" rather than "Unknown". */
  @Input() healthPending = false;
  @Output() selectEnv = new EventEmitter<EnvironmentDto>();

  healthOf(env: EnvironmentDto): EnvironmentHealthState {
    return this.health.get(environmentHealthKey(this.module.key, env.key)) ?? 'unknown';
  }

  healthLabel(env: EnvironmentDto): string {
    switch (this.healthOf(env)) {
      case 'reachable': return 'Reachable';
      case 'unreachable': return 'Unreachable';
      case 'unconfigured': return 'No endpoint';
      default: return this.healthPending ? 'Checking' : 'Unknown';
    }
  }

  healthIcon(env: EnvironmentDto): string {
    switch (this.healthOf(env)) {
      case 'reachable': return 'bi-broadcast-pin';
      case 'unreachable': return 'bi-plug';
      case 'unconfigured': return 'bi-dash-circle';
      default: return this.healthPending ? 'bi-arrow-repeat' : 'bi-question-circle';
    }
  }

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

  /** Maps to the hub tool card's accent names so one grid language covers both
   * dashboards; unmapped modules fall back to the neutral teal. */
  getAccentClass(key: string): string {
    switch (key) {
      case 'upc_ecommerce': return 'module-card--amber';
      case 'ghc_ecommerce': return 'module-card--info';
      case 'ghc_unicommerce': return 'module-card--brand';
      default: return 'module-card--teal';
    }
  }

  capabilityIcon(label: string): string {
    switch (label) {
      case 'Item lookup': return 'bi-search';
      case 'Consumer lookup': return 'bi-person-badge';
      case 'Order Requests': return 'bi-inboxes';
      case 'Cancel': return 'bi-x-circle';
      default: return 'bi-arrow-repeat';
    }
  }
}
