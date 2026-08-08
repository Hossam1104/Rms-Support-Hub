import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CancelRequestDialogComponent } from './cancel-request-dialog.component';
import { ResendRequestDialogComponent } from './resend-request-dialog.component';

describe('Order request dialog close controls', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CancelRequestDialogComponent, ResendRequestDialogComponent]
    }).compileComponents();
  });

  function closeButton<T>(fixture: ComponentFixture<T>): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.close-btn') as HTMLButtonElement;
  }

  it('names the cancel dialog icon-only close button', () => {
    const fixture = TestBed.createComponent(CancelRequestDialogComponent);
    fixture.detectChanges();

    expect(closeButton(fixture).getAttribute('aria-label')).toBe('Close');
    expect(closeButton(fixture).type).toBe('button');
  });

  it('names the resend dialog icon-only close button', () => {
    const fixture = TestBed.createComponent(ResendRequestDialogComponent);
    fixture.detectChanges();

    expect(closeButton(fixture).getAttribute('aria-label')).toBe('Close');
    expect(closeButton(fixture).type).toBe('button');
  });
});
