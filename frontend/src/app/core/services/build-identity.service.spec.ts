import { TestBed } from '@angular/core/testing';
import { BUILD_IDENTITY_PATH, BuildIdentity, BuildIdentityService, isBuildIdentity } from './build-identity.service';

const IDENTITY: BuildIdentity = {
  schemaVersion: 1,
  environment: 'Testing',
  commit: '0123456789abcdef0123456789abcdef01234567',
  commitShort: '0123456',
  sourceState: 'clean',
  buildId: 'b8e8f1a2c3d40506070809101112131415161718192021222324252627282930',
  assetCount: 42,
  builtAtUtc: '2026-08-15T09:30:00Z',
  indexHtmlSha256: 'aa'.repeat(32),
  mainBundle: { file: 'main-ABCD1234.js', sha256: 'bb'.repeat(32) }
};

const DEVELOPMENT_IDENTITY: BuildIdentity = {
  schemaVersion: 1,
  environment: 'development',
  commit: 'unknown',
  commitShort: 'unknown',
  sourceState: 'unknown',
  buildId: 'unknown',
  assetCount: 0,
  builtAtUtc: null,
  indexHtmlSha256: 'unknown',
  mainBundle: null
};

function stubFetch(response: Partial<Response> & { json?: () => Promise<unknown> }) {
  const fetchMock = vi.fn(() => Promise.resolve(response as Response));
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('BuildIdentityService', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads the served identity document same-origin without credentials or caching', async () => {
    const fetchMock = stubFetch({ ok: true, status: 200, json: () => Promise.resolve(IDENTITY) });
    const service = TestBed.inject(BuildIdentityService);

    await service.load();

    expect(fetchMock).toHaveBeenCalledWith(BUILD_IDENTITY_PATH, { cache: 'no-store', credentials: 'omit' });
    expect(BUILD_IDENTITY_PATH).toBe('/build-identity.json');
    expect(service.identity()?.buildId).toBe(IDENTITY.buildId);
    expect(service.unavailableReason()).toBeNull();
  });

  it('fetches once even when several callers ask for the identity', async () => {
    const fetchMock = stubFetch({ ok: true, status: 200, json: () => Promise.resolve(IDENTITY) });
    const service = TestBed.inject(BuildIdentityService);

    await Promise.all([service.load(), service.load(), service.load()]);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('summarises the served build without exposing a filesystem path', async () => {
    stubFetch({ ok: true, status: 200, json: () => Promise.resolve(IDENTITY) });
    const service = TestBed.inject(BuildIdentityService);

    await service.load();
    const summary = service.summary();

    expect(summary).toContain('Testing');
    expect(summary).toContain('0123456');
    expect(summary).toContain(IDENTITY.buildId.slice(0, 12));
    expect(summary).toContain('42 assets');
    expect(summary).toContain('2026-08-15T09:30:00Z');
    expect(summary).not.toMatch(/[A-Za-z]:\\|\/Users\/|ProgramData/);
  });

  it('flags a dirty source tree in the summary', async () => {
    stubFetch({ ok: true, status: 200, json: () => Promise.resolve({ ...IDENTITY, sourceState: 'modified' }) });
    const service = TestBed.inject(BuildIdentityService);

    await service.load();

    expect(service.summary()).toContain('source modified');
  });

  it('names the development server bundle instead of implying a real build', async () => {
    stubFetch({
      ok: true,
      status: 200,
      json: () => Promise.resolve(DEVELOPMENT_IDENTITY)
    });
    const service = TestBed.inject(BuildIdentityService);

    await service.load();

    expect(service.summary()).toContain('Development server bundle');
  });

  it('reports an unavailable identity instead of throwing when the document is missing', async () => {
    stubFetch({ ok: false, status: 404 });
    const service = TestBed.inject(BuildIdentityService);

    expect(await service.load()).toBeNull();
    expect(service.identity()).toBeNull();
    expect(service.summary()).toContain('HTTP 404');
  });

  it('rejects a malformed identity document', async () => {
    stubFetch({ ok: true, status: 200, json: () => Promise.resolve({ schemaVersion: 1 }) });
    const service = TestBed.inject(BuildIdentityService);

    expect(await service.load()).toBeNull();
    expect(service.summary()).toContain('malformed document');
  });

  it('validates every production identity field and rejects unknown fields', () => {
    expect(isBuildIdentity(IDENTITY)).toBe(true);
    expect(isBuildIdentity(DEVELOPMENT_IDENTITY)).toBe(true);

    const malformedDocuments: unknown[] = [
      { ...IDENTITY, schemaVersion: '1' },
      { ...IDENTITY, schemaVersion: 2 },
      { ...IDENTITY, environment: 'Preview' },
      { ...IDENTITY, commit: 'not-a-commit' },
      { ...IDENTITY, commitShort: '0000000' },
      { ...IDENTITY, sourceState: 'dirty' },
      { ...IDENTITY, buildId: 'A'.repeat(64) },
      { ...IDENTITY, assetCount: '42' },
      { ...IDENTITY, assetCount: 0 },
      { ...IDENTITY, assetCount: 100001 },
      { ...IDENTITY, builtAtUtc: '2026-08-15T09:30:00+00:00' },
      { ...IDENTITY, builtAtUtc: 'not-a-timestamp' },
      { ...IDENTITY, builtAtUtc: new Date(Date.now() + 10 * 60 * 1000).toISOString() },
      { ...IDENTITY, indexHtmlSha256: 'not-a-hash' },
      { ...IDENTITY, mainBundle: null },
      { ...IDENTITY, mainBundle: { file: '../main-ABCD1234.js', sha256: 'bb'.repeat(32) } },
      { ...IDENTITY, mainBundle: { file: 'main-ABCD1234.js?cache=1', sha256: 'bb'.repeat(32) } },
      { ...IDENTITY, mainBundle: { file: 'main-ABCD1234.css', sha256: 'bb'.repeat(32) } },
      { ...IDENTITY, mainBundle: { file: 'main-ABCD1234.js', sha256: 'not-a-hash' } },
      { ...IDENTITY, unexpected: true }
    ];

    for (const malformed of malformedDocuments) {
      expect(isBuildIdentity(malformed)).toBe(false);
    }
  });

  it('accepts the development sentinel only in its exact non-built shape', () => {
    expect(isBuildIdentity({ ...DEVELOPMENT_IDENTITY, sourceState: 'clean' })).toBe(false);
    expect(isBuildIdentity({ ...DEVELOPMENT_IDENTITY, mainBundle: { file: 'main-dev.js', sha256: 'aa'.repeat(32) } })).toBe(false);
  });

  it('survives a failed request', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.reject(new Error('network down')))
    );
    const service = TestBed.inject(BuildIdentityService);

    expect(await service.load()).toBeNull();
    expect(service.summary()).toContain('request failed');
  });

  it('describes an unloaded identity without claiming a build', () => {
    const service = TestBed.inject(BuildIdentityService);

    expect(service.summary()).toBe('Build identity has not been loaded.');
  });
});
