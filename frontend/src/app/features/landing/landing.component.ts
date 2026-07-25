import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ModuleService } from '../../core/services/module.service';
import { EnvironmentDto } from '../../core/models';
import { ModuleCardComponent } from './module-card.component';
import { NavbarComponent } from '../../layout/navbar/navbar.component';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, ModuleCardComponent, NavbarComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="landing-container">
      <section class="hero-section fade-in-up">
        <h1 class="hero-title">Choose a Module</h1>
        <p class="hero-subtitle">Select an active module and environment to start building an order payload.</p>
      </section>

      <section class="modules-grid slide-up">
        @for (module of moduleService.modules(); track module.key) {
          <app-module-card [module]="module" (selectEnv)="onEnvironmentSelected(module.key, $event)"></app-module-card>
        }
      </section>
    </main>
  `,
  styles: [`
    .landing-container {
      margin-top: var(--navbar-height);
      padding: 60px 40px;
      max-width: 1300px;
      margin-left: auto;
      margin-right: auto;
    }
    .hero-section { text-align: center; margin-bottom: 48px; }
    .hero-title { font-size: 2.75rem; font-weight: 800; background: var(--primary-gradient); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .hero-subtitle { font-size: 1.1rem; color: var(--text-secondary); max-width: 600px; margin: 12px auto 0; }
    .modules-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(360px, 1fr)); gap: 28px; }
  `]
})
export class LandingComponent {
  // ModuleService.modules() is already populated by the provideAppInitializer
  // in app.config.ts before this component (or any route) can render.
  moduleService = inject(ModuleService);
  private router = inject(Router);

  onEnvironmentSelected(moduleKey: string, env: EnvironmentDto) {
    this.moduleService.selectEnvironment(env);
    const targetTab = moduleKey === 'ghc_unicommerce' ? 'unicommerce' : 'order';
    this.router.navigate(['/modules', moduleKey, targetTab]);
  }
}
