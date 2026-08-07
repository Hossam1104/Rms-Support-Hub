import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { UiIconButtonComponent } from './ui-icon-button/ui-icon-button.component';
import { ToolCardComponent } from './tool-card/tool-card.component';
import { CountUpDirective } from '../directives/count-up.directive';

@Component({
  standalone: true,
  imports: [CountUpDirective],
  template: `<span [appCountUp]="target"></span>`
})
class CountUpHostComponent {
  target = 0;
}

describe('Session 02 design-system primitives', () => {
  beforeEach(async () => {
    if (!window.requestAnimationFrame) {
      Object.defineProperty(window, 'requestAnimationFrame', { writable: true, configurable: true, value: () => 0 });
      Object.defineProperty(window, 'cancelAnimationFrame', { writable: true, configurable: true, value: () => undefined });
    }
    await TestBed.configureTestingModule({
      imports: [UiIconButtonComponent, ToolCardComponent, CountUpHostComponent]
    }).compileComponents();
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-motion');
    vi.restoreAllMocks();
  });

  it('icon button requires an accessible label and exposes toggle state', () => {
    const fixture = TestBed.createComponent(UiIconButtonComponent);
    fixture.componentRef.setInput('icon', 'bi-universal-access');
    fixture.componentRef.setInput('ariaLabel', 'Motion: reduced');
    fixture.componentRef.setInput('active', true);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Motion: reduced');
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(button.querySelector('i.bi-universal-access')).toBeTruthy();
  });

  it('icon button emits on click and ignores disabled activation', () => {
    const fixture = TestBed.createComponent(UiIconButtonComponent);
    fixture.componentRef.setInput('icon', 'bi-gear');
    fixture.componentRef.setInput('ariaLabel', 'Settings');
    const pressed: number[] = [];
    fixture.componentInstance.pressed.subscribe(() => pressed.push(1));
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    button.click();
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    button.click();

    expect(pressed).toEqual([1]);
    expect(button.disabled).toBe(true);
  });

  it('tool card renders title, description, and status badge', () => {
    const fixture = TestBed.createComponent(ToolCardComponent);
    fixture.componentRef.setInput('title', 'Prompt Studio');
    fixture.componentRef.setInput('description', 'Generate test cases.');
    fixture.componentRef.setInput('status', 'migration-pending');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.tool-card__title')?.textContent).toContain('Prompt Studio');
    expect(el.querySelector('.tool-card__description')?.textContent).toContain('Generate test cases.');
    const status = el.querySelector('.tool-card__status') as HTMLElement;
    expect(status.textContent?.trim()).toBe('Migration Pending');
    expect(status.classList.contains('tool-card__status--pending')).toBe(true);
  });

  it('tool card activates from click, Enter, and Space with a focusable role', () => {
    const fixture = TestBed.createComponent(ToolCardComponent);
    fixture.componentRef.setInput('title', 'Online Orders');
    const activations: number[] = [];
    fixture.componentInstance.activated.subscribe(() => activations.push(1));
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.ui-card') as HTMLElement;
    expect(card.getAttribute('role')).toBe('button');
    expect(card.getAttribute('tabindex')).toBe('0');

    card.click();
    card.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    card.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));

    expect(activations).toEqual([1, 1, 1]);
  });

  it('tool card blocks pointer and keyboard activation while disabled', () => {
    const fixture = TestBed.createComponent(ToolCardComponent);
    fixture.componentRef.setInput('title', 'POS Maintenance');
    fixture.componentRef.setInput('disabled', true);
    const activations: number[] = [];
    fixture.componentInstance.activated.subscribe(() => activations.push(1));
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.ui-card') as HTMLElement;
    expect(card.getAttribute('tabindex')).toBeNull();
    expect(card.getAttribute('aria-disabled')).toBe('true');

    card.click();
    card.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));

    expect(activations).toEqual([]);
  });

  it('count-up honors an explicit reduce override over the system no-preference', () => {
    document.documentElement.setAttribute('data-motion', 'reduce');
    const fixture = TestBed.createComponent(CountUpHostComponent);
    fixture.componentInstance.target = 1234;
    fixture.detectChanges();

    // Reduced motion renders the final value synchronously, without a frame.
    expect(fixture.nativeElement.querySelector('span').textContent).toBe('1,234');
  });

  it('count-up honors an explicit full override over the system reduce preference', () => {
    document.documentElement.setAttribute('data-motion', 'full');
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      configurable: true,
      value: vi.fn((query: string) => ({ matches: true, media: query }))
    });

    const fixture = TestBed.createComponent(CountUpHostComponent);
    fixture.componentInstance.target = 1234;
    fixture.detectChanges();

    // Full motion animates instead of jumping to the final value, so the
    // span is still empty until the first animation frame runs.
    expect(fixture.nativeElement.querySelector('span').textContent).toBe('');

    Reflect.deleteProperty(window, 'matchMedia');
  });
});
