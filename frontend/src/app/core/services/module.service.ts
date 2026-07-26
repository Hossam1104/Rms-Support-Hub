import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom, Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { ModuleDto, EnvironmentDto, OrderDraft } from '../models';

/** GET /api/modules/{key} response body. */
export interface ModuleDetailResponse {
  module: ModuleDto;
  state: OrderDraft;
}

const ENV_STORAGE_PREFIX = 'onlineOrderTool.activeEnvironment.';

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
      this.restoreEnvironment(foundLocal);
    }

    return this.api.get<ModuleDetailResponse>(`modules/${key}`).pipe(
      tap(res => {
        if (res?.module) {
          this.activeModule.set(res.module);
          this.restoreEnvironment(res.module);
        }
      })
    );
  }

  /** Persists per module (U1, UI_Rework_Plan.md D4) so a Testing choice on
   * one module never leaks into another module's default. */
  selectEnvironment(env: EnvironmentDto) {
    this.activeEnvironment.set(env);
    const moduleKey = this.activeModule()?.key;
    if (moduleKey) {
      localStorage.setItem(ENV_STORAGE_PREFIX + moduleKey, env.key);
    }
  }

  /** The default lane is the module's flagged IsDefault environment --
   * never environments[0], which used to resolve to Production purely
   * because of dictionary insertion order (D4). The operator's own choice,
   * persisted per module in localStorage, wins as long as it still names a
   * real environment on this module -- otherwise it's discarded rather than
   * silently applied to the wrong module. */
  private restoreEnvironment(module: ModuleDto) {
    const storedKey = localStorage.getItem(ENV_STORAGE_PREFIX + module.key);
    const stored = storedKey ? module.environments.find(e => e.key === storedKey) : undefined;
    const fallbackDefault = module.environments.find(e => e.isDefault);
    this.activeEnvironment.set(stored ?? fallbackDefault ?? module.environments[0] ?? null);
  }
}
