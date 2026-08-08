import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ToolCardComponent } from '../../shared/ui';
import { MotionService } from '../../core/services/motion.service';
import { PromptStudioComponent } from './prompt-studio.component';

@Component({
  standalone: true,
  selector: 'app-navbar',
  template: ''
})
class StubNavbarComponent {}

describe('PromptStudioComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PromptStudioComponent],
      providers: [provideRouter([])]
    }).overrideComponent(PromptStudioComponent, {
      set: {
        imports: [StubNavbarComponent, ToolCardComponent]
      }
    }).compileComponents();
  });

  it('renders exactly three generator cards with native routes', () => {
    const fixture = TestBed.createComponent(PromptStudioComponent);
    fixture.detectChanges();

    const cards = Array.from(fixture.nativeElement.querySelectorAll('app-tool-card')) as HTMLElement[];
    expect(cards).toHaveLength(3);
    expect(cards.map(card => card.querySelector('.tool-card__title')?.textContent?.trim())).toEqual([
      'Bug Refinement',
      'Story Refinement',
      'Test Case Generation'
    ]);
    expect(cards.map(card => card.querySelector('.tool-card__icon')?.className)).toEqual([
      'bi tool-card__icon bi-bug',
      'bi tool-card__icon bi-journal-text',
      'bi tool-card__icon bi-check2-square'
    ]);
    expect(cards.every(card => card.querySelectorAll('.tool-card__capability').length === 3)).toBe(true);
    expect(cards.map(card => card.querySelector('a')?.getAttribute('href'))).toEqual([
      '/tools/prompt-studio/bugs',
      '/tools/prompt-studio/stories',
      '/tools/prompt-studio/test-cases'
    ]);
    expect(cards.every(card => card.querySelector('.tool-card__status')?.textContent?.trim() === 'Available')).toBe(true);
  });

  it('keeps the landing free of generator keyboard shortcuts', () => {
    const fixture = TestBed.createComponent(PromptStudioComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.prompt-preview')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('.tool-card__link')).toHaveLength(3);
  });

  it('gates the stagger through the shared motion preference', () => {
    const fixture = TestBed.createComponent(PromptStudioComponent);
    const motion = TestBed.inject(MotionService);

    motion.setPreference('reduce');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.prompt-studio__grid')?.classList.contains('prompt-studio__grid--motion')).toBe(false);

    motion.setPreference('full');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.prompt-studio__grid')?.classList.contains('prompt-studio__grid--motion')).toBe(true);

    motion.setPreference('system');
  });
});
