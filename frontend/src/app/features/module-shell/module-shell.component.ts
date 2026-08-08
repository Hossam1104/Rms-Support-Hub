import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ModuleService } from '../../core/services/module.service';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { SidebarComponent } from '../../layout/sidebar/sidebar.component';
import { BreadcrumbComponent } from '../../layout/breadcrumb/breadcrumb.component';
import { SidebarStateService } from '../../core/services/sidebar-state.service';

@Component({
  selector: 'app-module-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, NavbarComponent, SidebarComponent, BreadcrumbComponent],
  template: `
    <app-navbar></app-navbar>

    <div class="shell-layout" [style.--sidebar-width]="sidebarOffset()">
      <app-sidebar
        [moduleKey]="moduleKey()"
        [moduleLabel]="moduleService.activeModule()?.label || 'Online Order Module'"
        [clientName]="moduleService.activeModule()?.client || ''">
      </app-sidebar>

      <main class="main-content">
        <app-breadcrumb
          [moduleLabel]="moduleService.activeModule()?.label || 'Online Order Module'"
          [moduleRoute]="moduleRoute()"
          [currentTab]="currentTab()">
        </app-breadcrumb>
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .shell-layout { display: flex; width: 100%; min-width: 0; }
    .main-content {
      margin-top: var(--navbar-height);
      margin-left: var(--sidebar-width);
      flex: 0 0 calc(100vw - var(--sidebar-width));
      width: calc(100vw - var(--sidebar-width));
      max-width: calc(100vw - var(--sidebar-width));
      padding: var(--page-padding-block) var(--page-padding-inline);
      transition: margin-left var(--transition-normal);
      min-height: calc(100vh - var(--navbar-height));
      min-width: 0;
      overflow-x: hidden;
    }
    @media (max-width: 768px) {
      .main-content {
        margin-left: var(--sidebar-collapsed-width);
        flex-basis: calc(100vw - var(--sidebar-collapsed-width));
        width: calc(100vw - var(--sidebar-collapsed-width));
        max-width: calc(100vw - var(--sidebar-collapsed-width));
        padding: var(--page-padding-block) var(--page-padding-inline) var(--section-gap);
      }
    }
  `]
})
export class ModuleShellComponent implements OnInit {
  moduleService = inject(ModuleService);
  readonly sidebarState = inject(SidebarStateService);
  private route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  moduleKey = signal<string>('');
  readonly currentTab = signal('');
  readonly sidebarOffset = computed(() => this.sidebarState.collapsed() ? 'var(--sidebar-collapsed-width)' : 'var(--sidebar-expanded-width)');
  readonly moduleRoute = computed(() => {
    const key = this.moduleKey();
    if (!key) return [];
    const defaultWorkspace = this.currentTab() === 'Invoice Builder' ? 'unicommerce' : 'order';
    return ['/tools/online-orders/modules', key, defaultWorkspace];
  });

  ngOnInit() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const key = params.get('key') || '';
      this.moduleKey.set(key);
      if (key) {
        // errorEnvelopeInterceptor already surfaces failures via a toast;
        // activeModule/activeEnvironment fall back to the summary already
        // loaded by ModuleService.initialize() (see loadModuleDetails).
        this.moduleService.loadModuleDetails(key).subscribe({ error: () => { } });
      }
    });
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.updateCurrentTab());
    this.updateCurrentTab();
  }

  private updateCurrentTab() {
    let activeRoute = this.route.firstChild;
    let label = '';
    while (activeRoute) {
      const routeLabel = activeRoute.snapshot.data['breadcrumb'];
      if (typeof routeLabel === 'string') label = routeLabel;
      activeRoute = activeRoute.firstChild;
    }
    this.currentTab.set(label);
  }
}
