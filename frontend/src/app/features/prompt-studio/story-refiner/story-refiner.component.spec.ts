import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  EMPTY_STORY_PROMPT_INPUT,
  SAMPLE_STORY_PROMPT_INPUT,
  StoryPromptInput
} from '../models/story-prompt.model';
import { PROMPT_STUDIO_DRAFT_KEYS } from '../services/prompt-storage.service';
import { StoryRefinerComponent } from './story-refiner.component';

describe('StoryRefinerComponent', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [StoryRefinerComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  function create() {
    const fixture = TestBed.createComponent(StoryRefinerComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders all logical Story sections, controls, and counters', () => {
    const fixture = create();

    expect(fixture.nativeElement.querySelector('#story-raw')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-title')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-actor')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-goal')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-problem')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-current')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-desired')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-scope')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-out-of-scope')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-rules')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-dependencies')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-assumptions')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-ux-references')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-api-data')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-security')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-performance')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-accessibility')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-acceptance-style')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-detail-level')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#story-target-format')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('.story-form__section')).toHaveLength(6);
    expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]')).toHaveLength(4);
    expect(fixture.componentInstance.rawStoryCounter()).toBe('0 chars');
    expect(fixture.componentInstance.desiredBehaviorCounter()).toBe('0 chars');
    expect(fixture.componentInstance.businessRulesCounter()).toBe('0 chars');
    expect(fixture.nativeElement.textContent).toContain('Story Refinement');
  });

  it('loads the safe sample, persists all expanded fields immediately, and clears the draft', () => {
    const fixture = create();

    fixture.componentInstance.loadSample();

    expect(fixture.componentInstance.form.getRawValue()).toEqual(SAMPLE_STORY_PROMPT_INPUT);
    expect(fixture.componentInstance.businessRulesCounter()).toBe(`${SAMPLE_STORY_PROMPT_INPUT.businessRules.length} chars`);
    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBe(JSON.stringify(SAMPLE_STORY_PROMPT_INPUT));

    const restored = create();
    expect(restored.componentInstance.form.getRawValue()).toEqual(SAMPLE_STORY_PROMPT_INPUT);

    restored.componentInstance.clearForm();
    expect(restored.componentInstance.form.getRawValue()).toEqual(EMPTY_STORY_PROMPT_INPUT);
    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBeNull();
  });

  it('restores an old Session 04 draft with safe expanded defaults', () => {
    localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS.story, JSON.stringify({
      title: 'Legacy story title',
      actor: 'Legacy actor',
      businessGoal: 'Legacy goal',
      desiredBehavior: 'Legacy desired behavior'
    } satisfies Partial<StoryPromptInput>));

    const fixture = create();
    const value = fixture.componentInstance.form.getRawValue();

    expect(value.title).toBe('Legacy story title');
    expect(value.actor).toBe('Legacy actor');
    expect(value.businessGoal).toBe('Legacy goal');
    expect(value.desiredBehavior).toBe('Legacy desired behavior');
    expect(value.rawStory).toBe('');
    expect(value.acceptanceCriteriaStyle).toBe('Both');
    expect(value.detailLevel).toBe('Standard');
    expect(value.targetFormat).toBe('Generic Markdown');
    expect(value.includeOpenQuestions).toBe(true);
  });

  it('debounces normal expanded draft changes into the existing namespaced key', () => {
    vi.useFakeTimers();
    try {
      const fixture = create();
      fixture.componentInstance.form.patchValue({ scope: 'Review scope', performanceConsiderations: 'No target supplied' });

      expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBeNull();
      vi.advanceTimersByTime(300);

      expect(JSON.parse(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story) as string)).toMatchObject({
        scope: 'Review scope',
        performanceConsiderations: 'No target supplied'
      });
    } finally {
      vi.useRealTimers();
    }
  });

  it('does not recreate a draft from a pending write after clear', () => {
    vi.useFakeTimers();
    try {
      const fixture = create();
      fixture.componentInstance.form.patchValue({ title: 'Pending draft' });
      fixture.componentInstance.clearForm();
      vi.advanceTimersByTime(300);

      expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('generates a deterministic prompt and exposes copy and both downloads', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_STORY_PROMPT_INPUT);

    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(fixture.componentInstance.generatedPrompt()).toContain('Review order requests before approval');
    expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('# Final Response Contract');
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Copy generated prompt"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as Markdown"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as plain text"]')).toBeTruthy();
  });

  it('changes generated output for detail, acceptance style, and target format', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_STORY_PROMPT_INPUT);

    fixture.componentInstance.generate();
    const standard = fixture.componentInstance.generatedPrompt();

    fixture.componentInstance.form.controls.detailLevel.setValue('Deep');
    fixture.componentInstance.form.controls.acceptanceCriteriaStyle.setValue('Checklist');
    fixture.componentInstance.form.controls.targetFormat.setValue('Jira');
    fixture.componentInstance.generate();
    const deepJira = fixture.componentInstance.generatedPrompt();

    expect(deepJira).not.toBe(standard);
    expect(deepJira).toContain('Detail level: Deep');
    expect(deepJira).toContain('# Acceptance Criteria Format: Checklist');
    expect(deepJira).toContain('# Target Format: Jira');
  });

  it('applies detail-level optional defaults while keeping toggles configurable', () => {
    const fixture = create();
    const controls = fixture.componentInstance.form.controls;

    controls.detailLevel.setValue('Deep');
    expect(controls.includeQaImpact.value).toBe(true);
    expect(controls.includeDefinitionOfReady.value).toBe(true);
    expect(controls.includeSuggestedTestCoverage.value).toBe(true);

    controls.includeDefinitionOfReady.setValue(false);
    expect(controls.includeDefinitionOfReady.value).toBe(false);

    controls.detailLevel.setValue('Concise');
    expect(controls.includeQaImpact.value).toBe(false);
    expect(controls.includeDefinitionOfReady.value).toBe(false);
    expect(controls.includeSuggestedTestCoverage.value).toBe(false);
  });

  it('updates large-field counters synchronously', () => {
    const fixture = create();

    fixture.componentInstance.form.patchValue({
      rawStory: 'raw',
      desiredBehavior: 'desired behavior',
      businessRules: 'rule one\nrule two'
    });

    expect(fixture.componentInstance.rawStoryCounter()).toBe('3 chars');
    expect(fixture.componentInstance.desiredBehaviorCounter()).toBe('16 chars');
    expect(fixture.componentInstance.businessRulesCounter()).toBe('17 chars');
  });

  it('generates from Ctrl+Enter and Cmd+Enter through the shared workspace shortcut', () => {
    const fixture = create();
    const workspace = fixture.debugElement.children[0].componentInstance;
    const ctrlEvent = new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true, cancelable: true });
    const cmdEvent = new KeyboardEvent('keydown', { key: 'Enter', metaKey: true, cancelable: true });

    workspace.onGenerateShortcut(ctrlEvent);
    const first = fixture.componentInstance.generatedPrompt();
    workspace.onGenerateShortcut(cmdEvent);

    expect(ctrlEvent.defaultPrevented).toBe(true);
    expect(cmdEvent.defaultPrevented).toBe(true);
    expect(first).toContain('# Role');
    expect(fixture.componentInstance.generatedPrompt()).toBe(first);
  });
});
