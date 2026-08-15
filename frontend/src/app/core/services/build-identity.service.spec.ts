import { TestBed } from '@angular/core/testing';
import { BUILD_IDENTITY_PATH, BuildIdentity, BuildIdentityService } from './build-identity.service';

const IDENTITY: BuildIdentity = {
  schemaVersion: 1,
  environment: 'Testing',
  commit: '0123456789abcdef0123456789abcdef01234567',
  commitShort: '0123456',
  sourceState: 'clean',
  buildId: 'b8e8f1a2c3d40506070809101112131415161718192021222324252627282930ab',
  assetCount: 42,
  builtAtUtc: '2026-08-15T09:30:00Z',
  indexHtmlSha256: 'aa'.repeat(32),
  mainBundle: { file: 'main-ABCD1234.js', sha256: 'bb'.repeat(32) }
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
    stubFetch({ ok: true, status: 200, json: () => Promise.resolve({ ...IDENTITY, sourceState: 'dirty' }) });
    const service = TestBed.inject(BuildIdentityService);

    await service.load();

    expect(service.summary()).toContain('source dirty');
  });

  it('names the development server bundle instead of implying a real build', async () => {
    stubFetch({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ ...IDENTITY, environment: 'development', buildId: 'unknown', builtAtUtc: null })
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
