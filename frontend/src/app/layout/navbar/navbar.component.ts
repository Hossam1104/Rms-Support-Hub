import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ThemeService } from '../../core/services/theme.service';
import { ModuleService } from '../../core/services/module.service';
import { EnvBadgeComponent, UiButtonComponent } from '../../shared/ui';
import { EnvironmentDto } from '../../core/models';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, EnvBadgeComponent, UiButtonComponent],
  template: `
    <header class="navbar">
      <div class="navbar-brand">
        <span class="brand-title">Online Order Tool</span>
        <span class="brand-subtitle">Multi-Client Order System</span>
      </div>
      <div class="navbar-actions">
        <app-env-badge class="navbar-environment"
          *ngIf="moduleService.activeModule() as m"
          [environment]="moduleService.activeEnvironment()"
          [options]="m.environments"
          (select)="onSelectEnvironment($event)">
        </app-env-badge>
        <ui-button class="theme-toggle" variant="ghost" size="sm"
          [icon]="themeService.theme() === 'dark' ? 'bi-sun-fill' : 'bi-moon-fill'"
          [ariaLabel]="themeService.theme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'"
          (pressed)="themeService.toggleTheme()">
          {{ themeService.theme() === 'dark' ? 'Light Mode' : 'Dark Mode' }}
        </ui-button>
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
      background: var(--surface-panel);
      border-bottom: 1px solid var(--border-subtle);
      box-shadow: var(--shadow-sm);
    }
    .navbar-brand {
      display: flex;
      flex-direction: column;
    }
    .brand-title {
      font-size: 1.25rem;
      font-weight: 700;
      background: var(--grad-brand);
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
    .theme-toggle { color: var(--text-secondary); }
    @media (max-width: 560px) {
      .navbar { padding-inline: 14px; }
      .brand-subtitle { display: none; }
      :host ::ng-deep .navbar-environment { display: none; }
      :host ::ng-deep .theme-toggle .ui-button__projected { display: none; }
    }
  `]
})
export class NavbarComponent {
  themeService = inject(ThemeService);
  moduleService = inject(ModuleService);

  onSelectEnvironment(env: EnvironmentDto) {
    this.moduleService.selectEnvironment(env);
  }
}
