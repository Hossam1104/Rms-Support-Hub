import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BranchOption, LookupResult } from '../models';
import { ApiService } from './api.service';

/** Typed client for the capability-gated U3 branch endpoint. Backend caching
 * is deliberately authoritative; refresh=true asks it to bypass that cache. */
@Injectable({ providedIn: 'root' })
export class BranchOptionsService {
  private api = inject(ApiService);

  list(moduleKey: string, envKey: string | null | undefined, refresh = false): Observable<LookupResult<BranchOption[]>> {
    return this.api.get<LookupResult<BranchOption[]>>(`modules/${moduleKey}/branches`, {
      envKey: envKey || undefined,
      refresh: refresh || undefined
    });
  }
}
