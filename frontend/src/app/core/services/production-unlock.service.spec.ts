import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { ProductionUnlockDialogRequest, ProductionUnlockService } from './production-unlock.service';

describe('ProductionUnlockService', () => {
  let service: ProductionUnlockService;
  let api: { post: ReturnType<typeof vi.fn> };
  const request: ProductionUnlockDialogRequest = {
    moduleKey: 'upc_ecommerce',
    environmentKey: 'UPC Production',
    destination: 'order'
  };

  beforeEach(() => {
    api = { post: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        ProductionUnlockService,
        { provide: ApiService, useValue: api }
      ]
    });
    service = TestBed.inject(ProductionUnlockService);
    service.clear();
    service.close();
  });

  it('keeps only the opaque token in memory and sends no browser-held password later', () => {
    api.post.mockReturnValue(of({ token: 'opaque-test-token', expiresAt: '2099-01-01T00:00:00Z' }));

    service.unlock(request, 'TEST-ONLY-PASSWORD').subscribe();

    expect(api.post).toHaveBeenCalledWith('modules/upc_ecommerce/production-unlock', { password: 'TEST-ONLY-PASSWORD' });
    expect(service.isUnlocked('upc_ecommerce', 'UPC Production')).toBe(true);
    expect(service.mutationHeaders('upc_ecommerce', 'UPC Production')).toEqual({
      'X-SupportHub-Production-Unlock': 'opaque-test-token'
    });
    expect(localStorage.getItem('production-unlock')).toBeNull();
    expect(sessionStorage.getItem('production-unlock')).toBeNull();
  });

  it('does not unlock on wrong-password failure and exposes a generic message', () => {
    api.post.mockReturnValue(throwError(() => ({
      status: 401,
      code: 'production_unlock_failed',
      message: 'Production unlock failed.'
    })));

    service.unlock(request, 'wrong-test-password').subscribe({ error: () => undefined });

    expect(service.isUnlocked('upc_ecommerce', 'UPC Production')).toBe(false);
    expect(service.safeError({ status: 401, code: 'production_unlock_failed', message: 'Production unlock failed.' })).toBe(
      'The Production unlock could not be verified.'
    );
  });

  it('does not reuse the token for another module or environment and clears it explicitly', () => {
    api.post.mockReturnValue(of({ token: 'opaque-test-token', expiresAt: '2099-01-01T00:00:00Z' }));
    service.unlock(request, 'TEST-ONLY-PASSWORD').subscribe();

    expect(service.isUnlocked('ghc_ecommerce', 'GHC Production')).toBe(false);
    expect(service.mutationHeaders('ghc_ecommerce', 'GHC Production')).toBeUndefined();

    service.clear();
    expect(service.isUnlocked('upc_ecommerce', 'UPC Production')).toBe(false);
  });
});
