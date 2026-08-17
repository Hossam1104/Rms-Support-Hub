import { HttpClient } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { errorEnvelopeInterceptor } from './error-envelope.interceptor';
import { ToastService } from '../services/toast.service';

describe('errorEnvelopeInterceptor', () => {
  let http: HttpClient;
  let requests: HttpTestingController;
  let toast: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorEnvelopeInterceptor])),
        provideHttpClientTesting(),
        ToastService
      ]
    });

    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
    toast = TestBed.inject(ToastService);
  });

  afterEach(() => requests.verify());

  it('unwraps a policy error and renders its safe server message', () => {
    let received: any;
    http.get('/api/modules/upc_ecommerce/endpoint').subscribe({ error: error => received = error });

    const request = requests.expectOne('/api/modules/upc_ecommerce/endpoint');
    request.flush(
      { error: { code: 'environment_not_allowed', message: 'The selected environment is not allowed by this deployment.', details: null } },
      { status: 403, statusText: 'Forbidden' }
    );

    expect(received).toMatchObject({
      status: 403,
      code: 'environment_not_allowed',
      message: 'The selected environment is not allowed by this deployment.'
    });
    expect(received.details).toBeNull();
    expect(toast.toasts()[0].message).toBe('The selected environment is not allowed by this deployment.');
  });

  it('does not surface a raw downstream payload when the server sends a safe error', () => {
    let received: any;
    http.get('/api/modules/upc_ecommerce/test-endpoint').subscribe({ error: error => received = error });

    const request = requests.expectOne('/api/modules/upc_ecommerce/test-endpoint');
    request.flush(
      { error: { code: 'downstream_unreachable', message: 'The configured downstream environment could not be reached.', details: null } },
      { status: 502, statusText: 'Bad Gateway' }
    );

    expect(received.code).toBe('downstream_unreachable');
    expect(received.message).not.toContain('connectionString');
    expect(received.message).not.toContain('attacker.example');
    expect(toast.toasts()[0].message).toBe('The configured downstream environment could not be reached.');
  });
});
