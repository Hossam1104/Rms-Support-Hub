import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ThemeService } from '../../core/services/theme.service';
import { ModuleService } from '../../core/services/module.service';
import { EnvBadgeComponent } from '../../shared/ui';
import { EnvironmentDto } from '../../core/models';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, EnvBadgeComponent],
  template: `
    <header class="navbar glass-panel">
      <div class="navbar-brand">
        <span class="brand-title">Online Order Tool</span>
        <span class="brand-subtitle">Multi-Client Order System</span>
      </div>
      <div class="navbar-actions">
        <app-env-badge
          *ngIf="moduleService.activeModule() as m"
          [environment]="moduleService.activeEnvironment()"
          [options]="m.environments"
          (select)="onSelectEnvironment($event)">
        </app-env-badge>
        <button type="button" class="btn-action glass-card" (click)="toggleDocs()">
          <i class="bi bi-book"></i>
          <span>Docs</span>
        </button>
        <button type="button" class="btn-action glass-card" (click)="themeService.toggleTheme()">
          <i class="bi" [class.bi-sun-fill]="themeService.theme() === 'dark'" [class.bi-moon-fill]="themeService.theme() === 'light'"></i>
          <span>{{ themeService.theme() === 'dark' ? 'Light Mode' : 'Dark Mode' }}</span>
        </button>
      </div>
    </header>
  `,
  styles: [`
    .navbar {
      height: var(--navbar-height);
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      z-index: 1000;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0 24px;
      box-shadow: var(--shadow-sm);
    }
    .navbar-brand {
      display: flex;
      flex-direction: column;
    }
    .brand-title {
      font-size: 1.25rem;
      font-weight: 700;
      background: var(--primary-gradient);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .brand-subtitle {
      font-size: 0.75rem;
      color: var(--text-muted);
    }
    .navbar-actions {
      display: flex;
      gap: 12px;
    }
    .btn-action {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 14px;
      font-size: 0.85rem;
      color: var(--text-primary);
      cursor: pointer;
      border-radius: var(--radius-pill);
    }
  `]
})
export class NavbarComponent {
  themeService = inject(ThemeService);
  moduleService = inject(ModuleService);

  toggleDocs() {
    alert('Project Documentation available in docs/ folder');
  }

  onSelectEnvironment(env: EnvironmentDto) {
    this.moduleService.selectEnvironment(env);
  }
}
