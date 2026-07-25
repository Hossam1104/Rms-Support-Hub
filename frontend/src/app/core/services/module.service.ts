import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { ModuleDto, EnvironmentDto, OrderDraft } from '../models';

/** GET /api/modules/{key} response body. */
export interface ModuleDetailResponse {
  module: ModuleDto;
  state: OrderDraft;
}

/**
 * The module list used to be ~90 lines of hardcoded environments, logos and
 * URLs duplicating the backend's own module registry (remediation_plan.md
 * B25). It is now loaded once from GET /api/modules via `initialize()`,
 * called from a provideAppInitializer in app.config.ts before the app
 * renders -- there is no hardcoded fallback list, so this service and the
 * backend can never drift apart again.
 */
@Injectable({
  providedIn: 'root'
})
export class ModuleService {
  private api = inject(ApiService);

  modules = signal<ModuleDto[]>([]);
  activeModule = signal<ModuleDto | null>(null);
  activeEnvironment = signal<EnvironmentDto | null>(null);

  async initialize(): Promise<void> {
    try {
      const modules = await firstValueFrom(this.api.get<ModuleDto[]>('modules'));
      this.modules.set(modules ?? []);
    } catch (err) {
      console.error('Failed to load /api/modules at startup.', err);
      this.modules.set([]);
    }
  }

  loadModuleDetails(key: string): Observable<ModuleDetailResponse> {
    const foundLocal = this.modules().find(m => m.key === key);
    if (foundLocal) {
      this.activeModule.set(foundLocal);
      if (foundLocal.environments.length > 0 && !this.activeEnvironment()) {
        this.activeEnvironment.set(foundLocal.environments[0]);
      }
    }

    return this.api.get<ModuleDetailResponse>(`modules/${key}`).pipe(
      tap(res => {
        if (res?.module) {
          this.activeModule.set(res.module);
          if (res.module.environments.length > 0 && !this.activeEnvironment()) {
            this.activeEnvironment.set(res.module.environments[0]);
          }
        }
      })
    );
  }

  selectEnvironment(env: EnvironmentDto) {
    this.activeEnvironment.set(env);
  }
}
