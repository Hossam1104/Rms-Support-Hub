import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY_BUG_PROMPT_INPUT, SAMPLE_BUG_PROMPT_INPUT } from '../models/bug-prompt.model';
import { PROMPT_STUDIO_HISTORY_KEY } from '../services/prompt-history.service';
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

  it('renders useful source fields without retired output controls', () => {
    const fixture = create();

    expect(fixture.nativeElement.querySelector('#bug-raw-notes')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-title')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-module')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-environment')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-build-version')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-steps')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-severity')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-priority')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-attachments')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-test-data')).toBeNull();
    expect(fixture.nativeElement.querySelector('#bug-detail-level')).toBeNull();
    expect(fixture.nativeElement.querySelector('#bug-target-format')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]').length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('Complete the form and generate a prompt to preview it here.');
  });

  it('associates counters with their Prompt Studio controls without a live region', () => {
    const fixture = create();

    for (const controlId of ['bug-raw-notes', 'bug-steps', 'bug-expected', 'bug-actual']) {
      const control = fixture.nativeElement.querySelector(`#${controlId}`) as HTMLElement;
      const describedBy = control.getAttribute('aria-describedby');

      expect(describedBy).toBeTruthy();
      expect(fixture.nativeElement.querySelector(`[id="${describedBy}"]`)).toBeTruthy();
    }

    const qualityPanel = fixture.nativeElement.querySelector('.quality-panel');
    expect(qualityPanel.getAttribute('aria-live')).toBeNull();
    expect(qualityPanel.querySelector('.quality-panel__findings')).toBeTruthy();
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
    expect(value.attachments).toBe('legacy.png');
  });

  it('debounces simplified draft changes into the existing namespaced key', () => {
    vi.useFakeTimers();
    try {
      const fixture = create();
      fixture.componentInstance.form.patchValue({ module: 'Filtering', attachments: 'screenshot.png' });

      expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug)).toBeNull();
      vi.advanceTimersByTime(300);

      expect(JSON.parse(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug) as string)).toMatchObject({
        module: 'Filtering',
        attachments: 'screenshot.png'
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

  it('shows advisory quality and manages generated prompt history', () => {
    const fixture = create();

    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Prompt Quality');
    expect(fixture.nativeElement.textContent).toContain('advisory only');
    expect(fixture.nativeElement.textContent).toContain('Recent Prompts');
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Clear prompt history"]')).toBeTruthy();

    const workspace = fixture.debugElement.children[0].componentInstance;
    const record = workspace.history.records()[0];
    expect(JSON.parse(localStorage.getItem(PROMPT_STUDIO_HISTORY_KEY) as string)[0]).toEqual(record);

    workspace.openHistory(record);
    expect(fixture.componentInstance.generatedPrompt()).toBe(record.prompt);

    workspace.history.delete(record);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Recent Prompts');

    fixture.componentInstance.generate();
    fixture.detectChanges();
    workspace.history.clear();
    fixture.detectChanges();
    expect(workspace.history.records()).toEqual([]);
    expect(localStorage.getItem(PROMPT_STUDIO_HISTORY_KEY)).toBe('[]');
  });
});
