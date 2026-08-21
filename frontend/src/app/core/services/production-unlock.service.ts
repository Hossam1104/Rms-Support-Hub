import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { ApiError } from '../models';

export const PRODUCTION_UNLOCK_HEADER = 'X-SupportHub-Production-Unlock';

export interface ProductionUnlockResponse {
  token: string;
  expiresAt: string;
}

export interface ProductionUnlockDialogRequest {
  moduleKey: string;
  environmentKey: string;
  destination: 'order' | 'unicommerce' | 'order-requests';
}

interface UnlockContext {
  moduleKey: string;
  environmentKey: string;
  token: string;
  expiresAt: number;
}

/** Holds only the opaque, short-lived unlock token in application memory.
 * Nothing in this service writes the token or password to browser storage. */
@Injectable({ providedIn: 'root' })
export class ProductionUnlockService {
  private readonly api = inject(ApiService);
  private context: UnlockContext | null = null;

  readonly dialog = signal<ProductionUnlockDialogRequest | null>(null);

  open(request: ProductionUnlockDialogRequest): void {
    this.dialog.set(request);
  }

  close(): void {
    this.dialog.set(null);
  }

  isUnlocked(moduleKey: string, environmentKey: string): boolean {
    const context = this.context;
    if (!context || context.expiresAt <= Date.now()) {
      this.context = null;
      return false;
    }

    return context.moduleKey === moduleKey && context.environmentKey === environmentKey;
  }

  /** Returns the server-required header only for a live in-memory context.
   * Testing calls receive no unlock header. */
  mutationHeaders(moduleKey: string, environmentKey: string): Record<string, string> | undefined {
    if (!this.isUnlocked(moduleKey, environmentKey)) return undefined;
    return { [PRODUCTION_UNLOCK_HEADER]: this.context!.token };
  }

  unlock(request: ProductionUnlockDialogRequest, password: string): Observable<ProductionUnlockResponse> {
    return this.api.post<ProductionUnlockResponse>(
      `modules/${request.moduleKey}/production-unlock`,
      { password }
    ).pipe(
      tap(response => {
        if (!response?.token || !response.expiresAt) return;
        this.context = {
          moduleKey: request.moduleKey,
          environmentKey: request.environmentKey,
          token: response.token,
          expiresAt: Date.parse(response.expiresAt)
        };
      })
    );
  }

  /** Environment/module changes explicitly clear the browser-held context.
   * A page refresh also clears it because this service is not persisted. */
  clear(): void {
    this.context = null;
  }

  safeError(error: ApiError | undefined): string {
    if (error?.code === 'production_unlock_unavailable') {
      return 'Production unlock is not provisioned on this server.';
    }
    return error?.code === 'production_unlock_failed'
      ? 'The Production unlock could not be verified.'
      : error?.message || 'The Production unlock could not be completed.';
  }
}
