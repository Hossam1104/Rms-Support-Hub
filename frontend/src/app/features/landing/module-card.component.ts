import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ModuleDto, EnvironmentDto } from '../../core/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { UiButtonComponent, UiCardComponent } from '../../shared/ui';

@Component({
  selector: 'app-module-card',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, UiButtonComponent, UiCardComponent],
  template: `
    <ui-card variant="raised" class="module-card" [disabled]="!module.available" [style.--module-accent]="getAccentColor(module.key)">
      <div class="card-head" uiCardHeader>
        <div class="identity">
          <img [src]="getLogoUrl(module.key)" [alt]="module.label" class="module-logo" />
          <div>
            <h3 class="module-title">{{ module.label }}</h3>
            <span class="client-name">{{ module.client }}</span>
          </div>
        </div>
        <app-status-badge *ngIf="!module.available" label="Coming Soon" variant="secondary" role="status"></app-status-badge>
        <app-status-badge *ngIf="module.available" label="Active Module" variant="success" role="status"></app-status-badge>
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

      <div class="card-footer" *ngIf="module.available">
        <a [routerLink]="getModuleRoute(module.key)" class="enter-link">
          <span>Open Module</span>
          <i class="bi bi-arrow-right"></i>
        </a>
      </div>
    </ui-card>
  `,
  styles: [`
    .module-card { border-left: 4px solid var(--module-accent); }
    :host ::ng-deep .module-card .ui-card { overflow: visible; }
    .card-head { display: flex; justify-content: space-between; align-items: center; }
    .identity { display: flex; align-items: center; gap: 16px; }
    .module-logo { width: 48px; height: 48px; border-radius: var(--radius-md); background: var(--surface-selected); padding: 4px; object-fit: contain; }
    .module-title { margin: 0; font-size: 1.15rem; font-weight: 700; color: var(--text-primary); }
    .client-name { font-size: 0.8rem; color: var(--text-muted); font-weight: 500; }
    .card-envs { display: flex; flex-direction: column; gap: 10px; }
    :host ::ng-deep ui-button.env-btn { display: block; width: 100%; }
    :host ::ng-deep ui-button.env-btn .ui-button { width: 100%; min-height: 68px; justify-content: flex-start; text-align: left; border-color: var(--border-subtle); background: var(--surface-interactive); }
    :host ::ng-deep ui-button.env-btn .ui-button__projected { overflow: visible; text-overflow: clip; white-space: normal; }
    :host ::ng-deep ui-button.env-btn .ui-button:hover:not(:disabled) { transform: translateX(4px); border-color: var(--module-accent); background: var(--surface-hover); }
    .env-content { display: flex; align-items: center; width: 100%; gap: 12px; }
    .env-icon { flex: 0 0 auto; font-size: 1.25rem; color: var(--module-accent); }
    .env-meta { display: flex; min-width: 0; flex: 1; flex-direction: column; }
    .env-key { color: var(--text-primary); font-size: 0.88rem; font-weight: 700; overflow-wrap: anywhere; }
    .env-desc { color: var(--text-secondary); font-size: 0.75rem; overflow-wrap: anywhere; }
    .card-footer { display: flex; justify-content: flex-end; padding-top: 8px; }
    .enter-link { display: flex; align-items: center; gap: 8px; color: var(--text-accent); text-decoration: none; font-weight: 700; font-size: 0.95rem; transition: gap var(--transition-fast); }
    .enter-link:hover { gap: 12px; color: var(--accent-hover); }
  `]
})
export class ModuleCardComponent {
  @Input() module!: ModuleDto;
  @Output() selectEnv = new EventEmitter<EnvironmentDto>();

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
    return key === 'upc_ecommerce' ? 'assets/upc_logo.svg' : 'assets/whites_logo.svg';
  }

  getAccentColor(key: string): string {
    switch (key) {
      case 'upc_ecommerce': return 'var(--accent-amber)';
      case 'ghc_unicommerce': return 'var(--accent-violet)';
      default: return 'var(--accent-indigo)';
    }
  }
}
