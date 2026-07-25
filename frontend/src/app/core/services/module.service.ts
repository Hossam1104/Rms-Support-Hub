import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { Observable, tap, catchError, of } from 'rxjs';

export interface EnvironmentDto {
  key: string;
  environment: string;
  description: string;
  accent: string;
  cue: string;
  icon: string;
  routeLabel: string;
  visualUrl: string;
  visualAlt: string;
  available: boolean;
  statusLabel: string;
  apiUrl?: string;
}

export interface ModuleDto {
  key: string;
  label: string;
  client: string;
  available: boolean;
  environments: EnvironmentDto[];
}

const DEFAULT_MODULES: ModuleDto[] = [
  {
    key: 'ghc_ecommerce',
    label: 'GHC E-Commerce',
    client: 'GHC',
    available: true,
    environments: [
      {
        key: 'GHC Production',
        environment: 'Production',
        description: 'GHC live routing.',
        accent: 'ember',
        cue: 'Warehouse',
        icon: 'bi-box-seam',
        routeLabel: 'Live lane',
        visualUrl: 'assets/whites_logo.svg',
        visualAlt: 'GHC logo',
        available: true,
        statusLabel: 'Live',
        apiUrl: 'https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CreateAndAssignOrder'
      },
      {
        key: 'GHC Testing',
        environment: 'Testing',
        description: 'GHC QA routing.',
        accent: 'ocean',
        cue: 'Dispatch',
        icon: 'bi-truck',
        routeLabel: 'QA lane',
        visualUrl: 'assets/whites_logo.svg',
        visualAlt: 'GHC logo',
        available: true,
        statusLabel: 'Test',
        apiUrl: 'http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder'
      }
    ]
  },
  {
    key: 'upc_ecommerce',
    label: 'UPC E-Commerce',
    client: 'UPC',
    available: true,
    environments: [
      {
        key: 'UPC Production',
        environment: 'Production',
        description: 'UPC live routing.',
        accent: 'sunrise',
        cue: 'Retail Ops',
        icon: 'bi-bag-check',
        routeLabel: 'Live lane',
        visualUrl: 'assets/upc_logo.svg',
        visualAlt: 'UPC logo',
        available: true,
        statusLabel: 'Live',
        apiUrl: 'http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder'
      },
      {
        key: 'UPC Testing',
        environment: 'Testing',
        description: 'UPC QA routing.',
        accent: 'electric',
        cue: 'QA Grid',
        icon: 'bi-sliders2-vertical',
        routeLabel: 'Test lane',
        visualUrl: 'assets/upc_logo.svg',
        visualAlt: 'UPC logo',
        available: true,
        statusLabel: 'Test',
        apiUrl: 'http://10.10.9.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder'
      }
    ]
  },
  {
    key: 'ghc_unicommerce',
    label: 'GHC Uni-Commerce',
    client: 'GHC',
    available: true,
    environments: [
      {
        key: 'GHC Uni-Commerce Production',
        environment: 'Production',
        description: 'GHC Uni-Commerce live routing (pending API URL).',
        accent: 'aurora',
        cue: 'Automation',
        icon: 'bi-cpu',
        routeLabel: 'Pending lane',
        visualUrl: 'assets/whites_logo.svg',
        visualAlt: 'GHC Uni-Commerce logo',
        available: false,
        statusLabel: 'Soon'
      },
      {
        key: 'GHC Uni-Commerce Testing',
        environment: 'Testing',
        description: 'GHC Uni-Commerce QA routing (pending API URL).',
        accent: 'violet',
        cue: 'Staging',
        icon: 'bi-hourglass-split',
        routeLabel: 'Pending lane',
        visualUrl: 'assets/whites_logo.svg',
        visualAlt: 'GHC Uni-Commerce logo',
        available: false,
        statusLabel: 'Soon'
      }
    ]
  },
  {
    key: 'oms',
    label: 'OMS (Order Management)',
    client: 'OMS',
    available: false,
    environments: []
  },
  {
    key: 'call_center',
    label: 'Call Center Ordering',
    client: 'Call Center',
    available: false,
    environments: []
  }
];

@Injectable({
  providedIn: 'root'
})
export class ModuleService {
  private api = inject(ApiService);

  modules = signal<ModuleDto[]>(DEFAULT_MODULES);
  activeModule = signal<ModuleDto | null>(DEFAULT_MODULES[0]);
  activeEnvironment = signal<EnvironmentDto | null>(DEFAULT_MODULES[0].environments[0]);

  loadModules(): Observable<ModuleDto[]> {
    return this.api.get<ModuleDto[]>('modules').pipe(
      tap(mods => {
        if (mods && mods.length > 0) {
          this.modules.set(mods);
        }
      }),
      catchError(() => of(DEFAULT_MODULES))
    );
  }

  loadModuleDetails(key: string): Observable<any> {
    const foundLocal = this.modules().find(m => m.key === key);
    if (foundLocal) {
      this.activeModule.set(foundLocal);
      if (foundLocal.environments?.length > 0 && !this.activeEnvironment()) {
        this.activeEnvironment.set(foundLocal.environments[0]);
      }
    }

    return this.api.get<any>(`modules/${key}`).pipe(
      tap(res => {
        if (res?.module) {
          this.activeModule.set(res.module);
          if (res.module.environments?.length > 0 && !this.activeEnvironment()) {
            this.activeEnvironment.set(res.module.environments[0]);
          }
        }
      }),
      catchError(() => of({ module: foundLocal }))
    );
  }

  selectEnvironment(env: EnvironmentDto) {
    this.activeEnvironment.set(env);
  }
}
