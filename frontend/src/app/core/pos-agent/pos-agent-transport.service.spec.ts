import {
  HttpInterceptorFn,
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { POS_AGENT_ORIGIN, POS_AGENT_PATHS } from './pos-agent.constants';
import { PosAgentTransportService } from './pos-agent-transport.service';

describe('PosAgentTransportService', () => {
  let service: PosAgentTransportService;
  let httpTesting: HttpTestingController;
  let interceptorCalls: number;

  beforeEach(() => {
    interceptorCalls = 0;
    const countingInterceptor: HttpInterceptorFn = (request, next) => {
      interceptorCalls += 1;
      return next(request);
    };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([countingInterceptor])),
        provideHttpClientTesting(),
        PosAgentTransportService
      ]
    });

    service = TestBed.inject(PosAgentTransportService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpTesting.verify());

  it('uses the fixed anonymous health URL without credentials or the Hub interceptor chain', async () => {
    const result = firstValueFrom(service.getLive());
    const request = httpTesting.expectOne(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.live}`);

    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBe(false);
    expect(request.request.headers.get('Accept')).toBe('application/json');
    request.flush({ status: 'live' });

    await expect(result).resolves.toEqual({ status: 'live' });
    expect(interceptorCalls).toBe(0);
  });

  it('uses credentialed session transport at the fixed Agent URL', async () => {
    const result = firstValueFrom(service.getSession());
    const request = httpTesting.expectOne(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.session}`);

    expect(request.request.method).toBe('GET');
    expect(request.request.withCredentials).toBe(true);
    request.flush({
      principalName: 'TESTDOMAIN\\admin-user',
      isAuthorized: true,
      agentVersion: '1.0.0',
      apiVersion: '1.0',
      supportedApiVersions: ['1.0']
    });

    await expect(result).resolves.toMatchObject({ isAuthorized: true, apiVersion: '1.0' });
  });

  it('posts only the operation identifier and keeps credentials enabled for issuance', async () => {
    const result = firstValueFrom(service.issueMutationToken('integration.test-mutation'));
    const request = httpTesting.expectOne(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.mutationToken}`);

    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.body).toEqual({ operationId: 'integration.test-mutation' });
    request.flush({ token: 'opaque-test-token', expiresAtUtc: '2026-08-11T12:05:00Z' });

    await expect(result).resolves.toEqual({
      token: 'opaque-test-token',
      expiresAtUtc: '2026-08-11T12:05:00Z'
    });
  });

  it('classifies status zero conservatively without inferring a transport cause', async () => {
    const result = firstValueFrom(service.getReady());
    const request = httpTesting.expectOne(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.ready}`);
    request.error(new ProgressEvent('error'));

    await expect(result).rejects.toMatchObject({
      kind: 'transportUnavailableOrBlocked',
      status: 0
    });
  });

  it('preserves safe problem codes while classifying origin and server failures', async () => {
    const originResult = firstValueFrom(service.getSession());
    const originRequest = httpTesting.expectOne(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.session}`);
    originRequest.flush(
      { type: 'about:blank', title: 'The request origin is not accepted.', status: 403, code: 'origin_rejected' },
      { status: 403, statusText: 'Forbidden' }
    );

    await expect(originResult).rejects.toMatchObject({
      kind: 'originRejected',
      status: 403,
      code: 'origin_rejected'
    });

    const serverResult = firstValueFrom(service.getReady());
    const serverRequest = httpTesting.expectOne(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.ready}`);
    serverRequest.flush(null, { status: 503, statusText: 'Service Unavailable' });

    await expect(serverResult).rejects.toMatchObject({
      kind: 'agentServerError',
      status: 503
    });
  });
});
