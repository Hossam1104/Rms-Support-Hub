import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ModuleService } from '../../core/services/module.service';
import { EnvironmentDto } from '../../core/models';
import { ModuleCardComponent } from './module-card.component';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { BreadcrumbComponent } from '../../layout/breadcrumb/breadcrumb.component';
import { EmptyStateComponent, PageHeaderComponent } from '../../shared/ui';

/** Restyled onto the R8 token system (remediation_plan.md B24): the hero
 * now uses shared/ui's mesh page-header instead of a plain centered
 * heading. Behaviour (module load, environment selection, navigation) is
 * unchanged. */
@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, ModuleCardComponent, NavbarComponent, BreadcrumbComponent, PageHeaderComponent, EmptyStateComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="landing-container">
      <app-breadcrumb></app-breadcrumb>
      <app-page-header title="Online Order Tool" subtitle="Select an active module and environment to start building an order payload."></app-page-header>

      <section aria-label="Online Order modules">
        @if (moduleService.modules().length > 0) {
          <div class="modules-grid slide-up">
            @for (module of moduleService.modules(); track module.key) {
              <app-module-card [module]="module" (selectEnv)="onEnvironmentSelected(module.key, $event)"></app-module-card>
            }
          </div>
        } @else {
          <div class="modules-empty">
            <app-empty-state
              icon="bi-grid-3x3-gap"
              title="No Online Order modules are currently available."
              description="Modules load from the server when the workspace starts. Reload the page to try again.">
            </app-empty-state>
          </div>
        }
      </section>
    </main>
  `,
  styles: [`
    .landing-container {
      margin-top: var(--navbar-height);
      width: min(100%, 1300px);
      box-sizing: border-box;
      padding: var(--page-padding-block) var(--page-padding-inline) var(--section-gap);
      margin-left: auto;
      margin-right: auto;
    }
    /* Peer module cards stretch to one shared height (see module-card). */
    .modules-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); grid-auto-rows: 1fr; align-items: stretch; gap: var(--card-gap); }
    /* ModuleService.initialize() sets an empty list both for an empty server
       response and for a failed load, so this stays a neutral bounded surface
       rather than claiming a reason it cannot know. */
    .modules-empty { border: 1px solid var(--card-border); border-radius: var(--card-radius); background: var(--surface-panel); }
    @media (max-width: 768px) {
      .landing-container { padding: var(--page-padding-block) var(--page-padding-inline) var(--section-gap); }
      .modules-grid { grid-auto-rows: auto; }
    }
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
