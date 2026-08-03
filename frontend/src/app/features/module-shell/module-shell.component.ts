import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterOutlet } from '@angular/router';
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
        [moduleLabel]="moduleService.activeModule()?.label || ''"
        [clientName]="moduleService.activeModule()?.client || ''">
      </app-sidebar>

      <main class="main-content">
        <app-breadcrumb [moduleLabel]="moduleService.activeModule()?.label || ''"></app-breadcrumb>
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .shell-layout { display: flex; }
    .main-content {
      margin-top: var(--navbar-height);
      margin-left: var(--sidebar-width);
      flex: 1;
      padding: 30px;
      transition: margin-left var(--transition-normal);
      min-height: calc(100vh - var(--navbar-height));
    }
    @media (max-width: 768px) {
      .main-content { margin-left: 0; padding: 20px 16px 32px; }
    }
  `]
})
export class ModuleShellComponent implements OnInit {
  moduleService = inject(ModuleService);
  readonly sidebarState = inject(SidebarStateService);
  private route = inject(ActivatedRoute);

  moduleKey = signal<string>('');
  readonly sidebarOffset = computed(() => this.sidebarState.collapsed() ? 'var(--sidebar-collapsed-width)' : 'var(--sidebar-expanded-width)');

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      const key = params.get('key') || '';
      this.moduleKey.set(key);
      if (key) {
        // errorEnvelopeInterceptor already surfaces failures via a toast;
        // activeModule/activeEnvironment fall back to the summary already
        // loaded by ModuleService.initialize() (see loadModuleDetails).
        this.moduleService.loadModuleDetails(key).subscribe({ error: () => {} });
      }
    });
  }
}
