import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { NEVER } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { ProductionUnlockService } from '../../core/services/production-unlock.service';
import { ProductionUnlockDialogComponent } from './production-unlock-dialog.component';

describe('ProductionUnlockDialogComponent', () => {
  const request = {
    moduleKey: 'upc_ecommerce',
    environmentKey: 'UPC Production',
    destination: 'order' as const
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductionUnlockDialogComponent],
      providers: [
        { provide: ApiService, useValue: { post: vi.fn(() => NEVER) } },
        { provide: ProductionUnlockService, useClass: ProductionUnlockService },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    }).compileComponents();
  });

  it('ignores backdrop/Cancel close while unlock verification is in flight', () => {
    const fixture = TestBed.createComponent(ProductionUnlockDialogComponent);
    const service = TestBed.inject(ProductionUnlockService);
    service.open(request);
    fixture.componentInstance.password = 'TEST-ONLY-PASSWORD';
    fixture.detectChanges();

    fixture.componentInstance.submit();
    fixture.componentInstance.close();

    expect(fixture.componentInstance.submitting()).toBe(true);
    expect(service.dialog()).toEqual(request);
    expect(fixture.componentInstance.password).toBe('TEST-ONLY-PASSWORD');
  });

  it('clears the password on a genuine cancellation', () => {
    const fixture = TestBed.createComponent(ProductionUnlockDialogComponent);
    const service = TestBed.inject(ProductionUnlockService);
    service.open(request);
    fixture.componentInstance.password = 'TEST-ONLY-PASSWORD';

    fixture.componentInstance.close();

    expect(service.dialog()).toBeNull();
    expect(fixture.componentInstance.password).toBe('');
  });
});
