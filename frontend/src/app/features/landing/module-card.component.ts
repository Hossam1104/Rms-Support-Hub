import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ModuleDto, EnvironmentDto } from '../../core/models';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-module-card',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent],
  template: `
    <div class="module-card glass-card" [class.disabled]="!module.available" [style.border-left-color]="getAccentColor(module.key)">
      <div class="card-head">
        <div class="identity">
          <img [src]="getLogoUrl(module.key)" [alt]="module.label" class="module-logo" />
          <div>
            <h3 class="module-title">{{ module.label }}</h3>
            <span class="client-name">{{ module.client }}</span>
          </div>
        </div>
        <app-status-badge *ngIf="!module.available" label="Coming soon" variant="secondary"></app-status-badge>
        <app-status-badge *ngIf="module.available" label="Active Module" variant="success"></app-status-badge>
      </div>

      <div class="card-envs" *ngIf="module.available && module.environments.length > 0">
        @for (env of module.environments; track env.key) {
          <button type="button" class="env-btn glass-card" (click)="onSelectEnv(env)">
            <span class="env-icon"><i class="bi" [class]="env.icon"></i></span>
            <div class="env-meta">
              <span class="env-key">{{ env.key }}</span>
              <span class="env-desc">{{ env.description }}</span>
            </div>
            <app-status-badge [label]="env.statusLabel" [variant]="env.environment === 'Production' ? 'success' : 'info'"></app-status-badge>
          </button>
        }
      </div>

      <div class="card-footer" *ngIf="module.available">
        <a [routerLink]="getModuleRoute(module.key)" class="enter-link">
          <span>Open Module</span>
          <i class="bi bi-arrow-right"></i>
        </a>
      </div>
    </div>
  `,
  styles: [`
    .module-card { padding: 24px; display: flex; flex-direction: column; gap: 20px; border-left: 4px solid var(--primary); transition: all var(--transition-normal); }
    .module-card:hover { transform: translateY(-4px); box-shadow: var(--shadow-lg), var(--shadow-glow); }
    .module-card.disabled { opacity: 0.5; pointer-events: none; }
    .card-head { display: flex; justify-content: space-between; align-items: center; }
    .identity { display: flex; align-items: center; gap: 16px; }
    .module-logo { width: 48px; height: 48px; border-radius: var(--radius-md); background: rgba(255,255,255,0.05); padding: 4px; object-fit: contain; }
    .module-title { margin: 0; font-size: 1.15rem; font-weight: 700; color: var(--text-primary); }
    .client-name { font-size: 0.8rem; color: var(--text-muted); font-weight: 500; }
    .card-envs { display: flex; flex-direction: column; gap: 10px; }
    .env-btn { display: flex; align-items: center; gap: 12px; padding: 12px 16px; background: var(--bg-tertiary); border: 1px solid var(--glass-border); border-radius: var(--radius-md); cursor: pointer; text-align: left; transition: all var(--transition-fast); }
    .env-btn:hover { border-color: var(--primary); transform: translateX(6px); background: var(--glass-hover-bg); }
    .env-icon { font-size: 1.25rem; color: var(--primary); }
    .env-meta { flex: 1; display: flex; flex-direction: column; }
    .env-key { font-weight: 600; font-size: 0.88rem; color: var(--text-primary); }
    .env-desc { font-size: 0.75rem; color: var(--text-secondary); }
    .card-footer { display: flex; justify-content: flex-end; padding-top: 8px; }
    .enter-link { display: flex; align-items: center; gap: 8px; color: var(--primary); text-decoration: none; font-weight: 600; font-size: 0.95rem; transition: gap var(--transition-fast); }
    .enter-link:hover { gap: 12px; color: var(--primary-hover); }
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
      return ['/modules', key, 'unicommerce'];
    }
    return ['/modules', key, 'order'];
  }

  getLogoUrl(key: string): string {
    return key === 'upc_ecommerce' ? 'assets/upc_logo.svg' : 'assets/whites_logo.svg';
  }

  getAccentColor(key: string): string {
    switch (key) {
      case 'upc_ecommerce': return '#f59e0b';
      case 'ghc_unicommerce': return '#a855f7';
      default: return '#6366f1';
    }
  }
}
