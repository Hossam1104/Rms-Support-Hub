import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ThemeService } from '../../core/services/theme.service';
import { MotionPreference, MotionService } from '../../core/services/motion.service';
import { ModuleService } from '../../core/services/module.service';
import { BrandMarkComponent, EnvBadgeComponent, UiButtonComponent, UiIconButtonComponent } from '../../shared/ui';
import { EnvironmentDto } from '../../core/models';
import { APP_ASSETS } from '../../core/config/app-assets';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink, BrandMarkComponent, EnvBadgeComponent, UiButtonComponent, UiIconButtonComponent],
  template: `
    <header class="navbar">
      <a routerLink="/" class="navbar-brand" aria-label="Return to RMS+ Support Hub">
        <app-brand-mark class="navbar-logo" [src]="assets.brand.rms" size="2.5rem" [decorative]="true"></app-brand-mark>
        <span class="brand-copy">
          <span class="brand-title">RMS+ Support Hub</span>
          <span class="brand-subtitle">Unified QA & Support Workspace</span>
        </span>
      </a>
      <div class="navbar-actions">
        <app-env-badge class="navbar-environment"
          *ngIf="moduleService.activeModule() as m"
          [environment]="moduleService.activeEnvironment()"
          [options]="m.environments"
          (select)="onSelectEnvironment($event)">
        </app-env-badge>
        <ui-icon-button class="motion-toggle" size="sm"
          icon="bi-universal-access"
          [active]="motionService.preference() !== 'system'"
          [ariaLabel]="motionToggleLabel()"
          [title]="motionToggleLabel()"
          (pressed)="motionService.cyclePreference()">
        </ui-icon-button>
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
      z-index: var(--z-navbar);
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0 var(--space-5);
      background: var(--surface-panel);
      border-bottom: 1px solid var(--border-subtle);
      box-shadow: var(--shadow-sm);
    }
    .navbar-brand {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      border-radius: var(--radius-sm);
      text-decoration: none;
    }
    .navbar-logo { flex: 0 0 auto; }
    .brand-copy { display: flex; flex-direction: column; min-width: 0; }
    .navbar-brand:focus-visible {
      outline: none;
      box-shadow: var(--focus-ring);
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
      gap: var(--space-3);
    }
    .theme-toggle { color: var(--text-secondary); }
    @media (max-width: 560px) {
      .navbar { padding-inline: var(--space-3); }
      .brand-subtitle { display: none; }
      :host ::ng-deep .navbar-environment { display: none; }
      :host ::ng-deep .theme-toggle .ui-button__projected { display: none; }
    }
  `]
})
export class NavbarComponent {
  readonly assets = APP_ASSETS;
  themeService = inject(ThemeService);
  motionService = inject(MotionService);
  moduleService = inject(ModuleService);

  private readonly motionLabels: Record<MotionPreference, string> = {
    system: 'Motion: follows system preference. Activate to reduce motion.',
    reduce: 'Motion: reduced. Activate for full motion.',
    full: 'Motion: full. Activate to follow the system preference.'
  };

  motionToggleLabel = computed(() => this.motionLabels[this.motionService.preference()]);

  onSelectEnvironment(env: EnvironmentDto) {
    this.moduleService.selectEnvironment(env);
  }
}
