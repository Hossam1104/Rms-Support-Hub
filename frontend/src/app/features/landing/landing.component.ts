import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ModuleService } from '../../core/services/module.service';
import { EnvironmentDto } from '../../core/models';
import { ModuleCardComponent } from './module-card.component';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { PageHeaderComponent } from '../../shared/ui';

/** Restyled onto the R8 token system (remediation_plan.md B24): the hero
 * now uses shared/ui's mesh page-header instead of a plain centered
 * heading. Behaviour (module load, environment selection, navigation) is
 * unchanged. */
@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, ModuleCardComponent, NavbarComponent, PageHeaderComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="landing-container">
      <app-page-header title="Choose a Module" subtitle="Select an active module and environment to start building an order payload."></app-page-header>

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
      padding: 40px 40px 60px;
      max-width: 1300px;
      margin-left: auto;
      margin-right: auto;
    }
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
    this.router.navigate(['/tools/online-orders/modules', moduleKey, targetTab]);
  }
}
