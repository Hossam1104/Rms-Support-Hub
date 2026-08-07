import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SAMPLE_STORY_PROMPT_INPUT } from '../models/story-prompt.model';
import { StoryRefinerComponent } from './story-refiner.component';

describe('StoryRefinerComponent', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [StoryRefinerComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('renders the story foundation form', () => {
    const fixture = TestBed.createComponent(StoryRefinerComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#story-raw')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-goal')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Story Refinement');
  });

  it('generates the deterministic foundation prompt', () => {
    const fixture = TestBed.createComponent(StoryRefinerComponent);
    fixture.componentInstance.form.setValue(SAMPLE_STORY_PROMPT_INPUT);
    fixture.detectChanges();

    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(fixture.componentInstance.generatedPrompt()).toContain('Review flagged order payloads before submission');
    expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('## Refined Title');
  });
});
