import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ToolCardComponent } from '../../shared/ui';
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
});
