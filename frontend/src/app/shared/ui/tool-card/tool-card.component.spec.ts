import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ToolCardComponent } from './tool-card.component';

describe('ToolCardComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ToolCardComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('keeps status, capability, and action icons decorative while exposing text labels', () => {
    const fixture = TestBed.createComponent(ToolCardComponent);
    fixture.componentRef.setInput('title', 'QA Prompt Studio');
    fixture.componentRef.setInput('route', '/tools/prompt-studio');
    fixture.componentRef.setInput('capabilities', ['Bug Refinement', 'Story Refinement', 'Test Cases']);
    fixture.componentRef.setInput('actionLabel', 'Open Prompt Studio');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.tool-card__status-icon')?.classList.contains('bi-check2-circle')).toBe(true);
    expect(host.querySelectorAll('.tool-card__capability')).toHaveLength(3);
    expect(host.querySelector('.tool-card__capability-icon')?.classList.contains('bi-bug')).toBe(true);
    expect(host.querySelectorAll('[aria-hidden="true"]')).not.toHaveLength(0);
    expect(host.querySelector('.tool-card__action')?.textContent).toContain('Open Prompt Studio');
    expect(host.querySelector('.tool-card__action i')?.classList.contains('bi-arrow-up-right')).toBe(true);
  });

  it('uses a non-success status icon for a Coming Soon card', () => {
    const fixture = TestBed.createComponent(ToolCardComponent);
    fixture.componentRef.setInput('title', 'POS Maintenance Tool');
    fixture.componentRef.setInput('status', 'migration-pending');
    fixture.componentRef.setInput('route', '/tools/pos-maintenance');
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.tool-card__status')?.textContent).toContain('Coming Soon');
    expect(host.querySelector('.tool-card__status-icon')?.classList.contains('bi-hourglass-split')).toBe(true);
  });
});
