import { Injectable, signal } from '@angular/core';

/**
 * Non-secret identity of the frontend bundle currently being served.
 *
 * The document is emitted into the build output by
 * `frontend/scripts/build-identity.mjs` and fetched same-origin, so what the
 * operator reads here is the identity of the bytes their browser actually
 * loaded -- not a value compiled in from a possibly stale source tree. It
 * deliberately carries no filesystem path, hostname, or credential.
 */
export interface BuildIdentity {
  schemaVersion: number;
  environment: string;
  commit: string;
  commitShort: string;
  sourceState: string;
  buildId: string;
  assetCount: number;
  builtAtUtc: string | null;
  indexHtmlSha256: string;
  mainBundle: { file: string; sha256: string } | null;
}

export const BUILD_IDENTITY_PATH = '/build-identity.json' as const;

@Injectable({ providedIn: 'root' })
export class BuildIdentityService {
  /** `null` until loaded; stays `null` when the document is unavailable. */
  readonly identity = signal<BuildIdentity | null>(null);
  readonly unavailableReason = signal<string | null>(null);

  private loading: Promise<BuildIdentity | null> | null = null;

  /** Loads once per document; repeat callers share the first in-flight fetch. */
  load(): Promise<BuildIdentity | null> {
    this.loading ??= this.fetchIdentity();
    return this.loading;
  }

  /** Compact operator-facing summary; never throws and never shows a path. */
  summary(): string {
    const identity = this.identity();
    if (!identity) return this.unavailableReason() ?? 'Build identity has not been loaded.';
    if (identity.buildId === 'unknown') {
      return 'Development server bundle: no immutable build identity is emitted by `ng serve`.';
    }

    const built = identity.builtAtUtc ? ` built ${identity.builtAtUtc}` : '';
    const state = identity.sourceState === 'clean' ? '' : ` (source ${identity.sourceState})`;
    return `${identity.environment} · commit ${identity.commitShort}${state} · build ${identity.buildId.slice(0, 12)} · ${identity.assetCount} assets${built}`;
  }

  private async fetchIdentity(): Promise<BuildIdentity | null> {
    try {
      const response = await fetch(BUILD_IDENTITY_PATH, { cache: 'no-store', credentials: 'omit' });
      if (!response.ok) {
        this.unavailableReason.set(`Build identity is unavailable (HTTP ${response.status}).`);
        return null;
      }

      const parsed = (await response.json()) as BuildIdentity;
      if (typeof parsed?.buildId !== 'string' || typeof parsed?.commit !== 'string') {
        this.unavailableReason.set('Build identity is unavailable (malformed document).');
        return null;
      }

      this.unavailableReason.set(null);
      this.identity.set(parsed);
      return parsed;
    } catch {
      this.unavailableReason.set('Build identity is unavailable (request failed).');
      return null;
    }
  }
}
