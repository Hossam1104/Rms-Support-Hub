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

const SHA256_PATTERN = /^[0-9a-f]{64}$/;
const COMMIT_PATTERN = /^[0-9a-f]{40}$/;
const MAIN_BUNDLE_PATTERN = /^main-[A-Za-z0-9]+\.js$/;
const APPROVED_ENVIRONMENTS = new Set(['Testing', 'Production']);
const MAX_ASSET_COUNT = 100_000;
const IDENTITY_FIELDS = [
  'schemaVersion',
  'environment',
  'commit',
  'commitShort',
  'sourceState',
  'buildId',
  'assetCount',
  'builtAtUtc',
  'indexHtmlSha256',
  'mainBundle'
] as const;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function hasExactFields(value: Record<string, unknown>, fields: readonly string[]): boolean {
  const actual = Object.keys(value);
  return actual.length === fields.length && fields.every(field => Object.prototype.hasOwnProperty.call(value, field));
}

function isDevelopmentPlaceholder(value: Record<string, unknown>): boolean {
  return hasExactFields(value, IDENTITY_FIELDS)
    && value['schemaVersion'] === 1
    && value['environment'] === 'development'
    && value['commit'] === 'unknown'
    && value['commitShort'] === 'unknown'
    && value['sourceState'] === 'unknown'
    && value['buildId'] === 'unknown'
    && value['assetCount'] === 0
    && value['builtAtUtc'] === null
    && value['indexHtmlSha256'] === 'unknown'
    && value['mainBundle'] === null;
}

function isUtcTimestamp(value: unknown): value is string {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$/.test(value)) return false;
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) && timestamp <= Date.now() + 5 * 60 * 1000;
}

/**
 * Validates the complete non-secret identity contract before it reaches the
 * operator-facing diagnostics panel. The development placeholder is the only
 * intentionally non-built identity and must match its exact sentinel shape.
 */
export function isBuildIdentity(value: unknown): value is BuildIdentity {
  if (!isRecord(value)) return false;
  if (isDevelopmentPlaceholder(value)) return true;
  if (!hasExactFields(value, IDENTITY_FIELDS)) return false;

  const schemaVersion = value['schemaVersion'];
  const environment = value['environment'];
  const commit = value['commit'];
  const commitShort = value['commitShort'];
  const sourceState = value['sourceState'];
  const buildId = value['buildId'];
  const assetCount = value['assetCount'];
  const builtAtUtc = value['builtAtUtc'];
  const indexHtmlSha256 = value['indexHtmlSha256'];
  const mainBundle = value['mainBundle'];

  if (schemaVersion !== 1) return false;
  if (typeof environment !== 'string' || !APPROVED_ENVIRONMENTS.has(environment)) return false;
  if (typeof commit !== 'string' || !COMMIT_PATTERN.test(commit)) return false;
  if (typeof commitShort !== 'string' || commitShort !== commit.slice(0, 7)) return false;
  if (sourceState !== 'clean' && sourceState !== 'modified') return false;
  if (typeof buildId !== 'string' || !SHA256_PATTERN.test(buildId)) return false;
  if (typeof assetCount !== 'number' || !Number.isInteger(assetCount) || assetCount <= 0 || assetCount > MAX_ASSET_COUNT) return false;
  if (!isUtcTimestamp(builtAtUtc)) return false;
  if (typeof indexHtmlSha256 !== 'string' || !SHA256_PATTERN.test(indexHtmlSha256)) return false;

  if (!isRecord(mainBundle) || !hasExactFields(mainBundle, ['file', 'sha256'])) return false;
  const mainFile = mainBundle['file'];
  const mainHash = mainBundle['sha256'];
  return typeof mainFile === 'string'
    && MAIN_BUNDLE_PATTERN.test(mainFile)
    && typeof mainHash === 'string'
    && SHA256_PATTERN.test(mainHash);
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

      const parsed: unknown = await response.json();
      if (!isBuildIdentity(parsed)) {
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
