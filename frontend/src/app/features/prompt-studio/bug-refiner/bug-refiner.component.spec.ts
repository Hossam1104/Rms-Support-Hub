import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY_BUG_PROMPT_INPUT, SAMPLE_BUG_PROMPT_INPUT } from '../models/bug-prompt.model';
import { PROMPT_STUDIO_DRAFT_KEYS } from '../services/prompt-storage.service';
import { BugRefinerComponent } from './bug-refiner.component';

describe('BugRefinerComponent', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [BugRefinerComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  function create() {
    const fixture = TestBed.createComponent(BugRefinerComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders all logical bug sections and output controls', () => {
    const fixture = create();

    expect(fixture.nativeElement.querySelector('#bug-raw-notes')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-title')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-module')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-environment')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-build-version')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-test-data')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-steps')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-frequency')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-severity')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-priority')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-impact')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-error-message')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-logs')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-attachments')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-related-reference')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-detail-level')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-target-format')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]').length).toBe(4);
    expect(fixture.nativeElement.textContent).toContain('Complete the form and generate a prompt to preview it here.');
  });

  it('loads the safe sample, persists all expanded fields immediately, and clears the draft', () => {
    const fixture = create();

    fixture.componentInstance.loadSample();
    expect(fixture.componentInstance.form.getRawValue()).toEqual(SAMPLE_BUG_PROMPT_INPUT);
    expect(fixture.componentInstance.stepsCounter()).toBe(`${SAMPLE_BUG_PROMPT_INPUT.steps.length} chars`);
    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug)).toBe(JSON.stringify(SAMPLE_BUG_PROMPT_INPUT));

    const restored = create();
    expect(restored.componentInstance.form.getRawValue()).toEqual(SAMPLE_BUG_PROMPT_INPUT);

    restored.componentInstance.clearForm();
    expect(restored.componentInstance.form.getRawValue()).toEqual(EMPTY_BUG_PROMPT_INPUT);
    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug)).toBeNull();
  });

  it('restores an old Session 04 six-field draft with safe expanded defaults', () => {
    localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS.bug, JSON.stringify({
      title: 'Legacy title',
      preconditions: 'Legacy setup',
      steps: 'Legacy step',
      expectedResult: 'Legacy expected',
      actualResult: 'Legacy actual',
      attachments: 'legacy.png'
    }));

    const fixture = create();
    const value = fixture.componentInstance.form.getRawValue();

    expect(value.title).toBe('Legacy title');
    expect(value.steps).toBe('Legacy step');
    expect(value.attachments).toBe('legacy.png');
    expect(value.rawNotes).toBe('');
    expect(value.detailLevel).toBe('Standard');
    expect(value.targetFormat).toBe('Generic Markdown');
    expect(value.includeMissingInformation).toBe(true);
  });

  it('debounces normal expanded draft changes into the existing namespaced key', () => {
    vi.useFakeTimers();
    try {
      const fixture = create();
      fixture.componentInstance.form.patchValue({ module: 'Filtering', logs: 'logs-reference.txt' });

      expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug)).toBeNull();
      vi.advanceTimersByTime(300);

      expect(JSON.parse(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug) as string)).toMatchObject({
        module: 'Filtering',
        logs: 'logs-reference.txt'
      });
    } finally {
      vi.useRealTimers();
    }
  });

  it('generates a deterministic prompt and exposes copy and both downloads', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_BUG_PROMPT_INPUT);

    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(fixture.componentInstance.generatedPrompt()).toContain('Status filter resets');
    expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('🐛 Bug Title');
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Copy generated prompt"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as Markdown"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as plain text"]')).toBeTruthy();
  });

  it('changes the generated prompt for detail level and target format selections', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_BUG_PROMPT_INPUT);

    fixture.componentInstance.generate();
    const standard = fixture.componentInstance.generatedPrompt();

    fixture.componentInstance.form.controls.detailLevel.setValue('Deep');
    fixture.componentInstance.form.controls.targetFormat.setValue('Jira');
    fixture.componentInstance.generate();
    const deepJira = fixture.componentInstance.generatedPrompt();

    expect(deepJira).not.toBe(standard);
    expect(deepJira).toContain('Detail level: Deep');
    expect(deepJira).toContain('Target Format: Jira');
  });

  it('applies detail-level optional-section defaults while keeping them configurable', () => {
    const fixture = create();
    const controls = fixture.componentInstance.form.controls;

    controls.detailLevel.setValue('Deep');
    expect(controls.includeAcceptanceCriteria.value).toBe(true);
    expect(controls.includeRetestChecklist.value).toBe(true);
    expect(controls.includeRegressionScope.value).toBe(true);

    controls.includeRegressionScope.setValue(false);
    expect(controls.includeRegressionScope.value).toBe(false);

    controls.detailLevel.setValue('Concise');
    expect(controls.includeAcceptanceCriteria.value).toBe(false);
    expect(controls.includeRetestChecklist.value).toBe(false);
  });

  it('generates from Ctrl+Enter and Cmd+Enter through the workspace shortcut', () => {
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
