import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ModuleService } from '../../core/services/module.service';
import { EnvironmentDto, ModuleDto } from '../../core/models';
import { ModuleCardComponent } from './module-card.component';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { BreadcrumbComponent } from '../../layout/breadcrumb/breadcrumb.component';
import { EmptyStateComponent, PageHeaderComponent } from '../../shared/ui';

/** Restyled onto the R8 token system (remediation_plan.md B24): the hero
 * now uses shared/ui's mesh page-header instead of a plain centered
 * heading. The hero carries a workspace summary and the grid is introduced by
 * a directory heading, matching the Hub dashboard's reading order. Behaviour
 * (module load, environment selection, navigation) is unchanged. */
@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, ModuleCardComponent, NavbarComponent, BreadcrumbComponent, PageHeaderComponent, EmptyStateComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="landing-container">
      <app-breadcrumb></app-breadcrumb>
      <app-page-header title="Online Order Tool" subtitle="Select an active module and environment to start building an order payload.">
        <!-- Fills the hero's action slot with the same stat language the Hub
             hero uses, instead of leaving half the band empty. -->
        <div class="landing-stats" aria-label="Module summary">
          <div class="landing-stat">
            <i class="bi bi-check2-circle" aria-hidden="true"></i>
            <span class="landing-stat__copy">
              <span class="landing-stat__label">Active modules</span>
              <strong>{{ activeCount() }} of {{ moduleService.modules().length }}</strong>
            </span>
          </div>
          <div class="landing-stat">
            <i class="bi bi-hdd-network" aria-hidden="true"></i>
            <span class="landing-stat__copy">
              <span class="landing-stat__label">Environments</span>
              <strong>{{ environmentCount() }} routes</strong>
            </span>
          </div>
        </div>
      </app-page-header>

      <section aria-label="Online Order modules">
        <div class="landing-heading">
          <div>
            <p class="landing-eyebrow"><i class="bi bi-grid-3x3-gap" aria-hidden="true"></i>Module directory</p>
            <h2 class="landing-heading__title">Choose a module</h2>
          </div>
          <p class="landing-heading__hint"><i class="bi bi-arrow-down-right" aria-hidden="true"></i>Pick an environment to start a payload.</p>
        </div>

        @if (orderedModules().length > 0) {
          <div class="modules-grid slide-up">
            @for (module of orderedModules(); track module.key) {
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
      width: min(100%, 1240px);
      box-sizing: border-box;
      padding: var(--page-padding-block) var(--page-padding-inline) var(--section-gap);
      margin-left: auto;
      margin-right: auto;
    }

    .landing-stats { display: flex; flex-wrap: wrap; gap: var(--space-5); }
    .landing-stat { display: flex; min-width: 0; align-items: center; gap: var(--space-3); }
    .landing-stat > i { flex: 0 0 auto; color: var(--text-accent); font-size: 1.15rem; }
    .landing-stat__copy { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
    .landing-stat__label { color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); letter-spacing: .04em; text-transform: uppercase; white-space: nowrap; }
    .landing-stat__copy strong { color: var(--text-primary); font-size: var(--text-sm); white-space: nowrap; }

    .landing-heading { display: flex; align-items: end; justify-content: space-between; gap: var(--space-5); margin-bottom: var(--card-gap); }
    .landing-eyebrow { display: flex; align-items: center; gap: var(--space-2); margin: 0 0 var(--space-1); color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    .landing-eyebrow i { font-size: .9rem; }
    .landing-heading__title { margin: 0; color: var(--text-primary); font-size: var(--text-xl); line-height: var(--leading-tight); }
    .landing-heading__hint { margin: 0; color: var(--text-muted); font-size: var(--text-sm); }

    /* Fixed columns match the Hub tool grid, so both dashboards break at the
       same widths. Rows size to their own content: a trailing row of Coming
       Soon cards must not inherit the height of a row full of environments. */
    .modules-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); grid-auto-rows: auto; align-items: stretch; gap: var(--card-gap); }
    /* ModuleService.initialize() sets an empty list both for an empty server
       response and for a failed load, so this stays a neutral bounded surface
       rather than claiming a reason it cannot know. */
    .modules-empty { border: 1px solid var(--card-border); border-radius: var(--card-radius); background: var(--surface-panel); }

    @media (max-width: 1024px) {
      .modules-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 768px) {
      .landing-stats { gap: var(--space-4); }
      .landing-heading { align-items: flex-start; flex-direction: column; gap: var(--space-2); }
      .modules-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class LandingComponent {
  // ModuleService.modules() is already populated by the provideAppInitializer
  // in app.config.ts before this component (or any route) can render.
  moduleService = inject(ModuleService);
  private router = inject(Router);

  /** Available modules lead the grid; the sort is stable, so the server's own
   * order still decides the sequence within each group. */
  readonly orderedModules = computed(() =>
    [...this.moduleService.modules()].sort((a, b) => Number(b.available === true) - Number(a.available === true)));

  readonly activeCount = computed(() =>
    this.moduleService.modules().filter(module => module.available).length);

  readonly environmentCount = computed(() =>
    this.moduleService.modules().reduce((total, module) => total + this.environmentsOf(module).length, 0));

  onEnvironmentSelected(moduleKey: string, env: EnvironmentDto) {
    const module = this.moduleService.modules().find(item => item.key === moduleKey);
    this.moduleService.selectEnvironment(env, module);
    const targetTab = moduleKey === 'ghc_unicommerce' ? 'unicommerce' : 'order';
    this.router.navigate(['/tools/online-orders/modules', moduleKey, targetTab]);
  }

  private environmentsOf(module: ModuleDto): EnvironmentDto[] {
    return module.environments ?? [];
  }
}
