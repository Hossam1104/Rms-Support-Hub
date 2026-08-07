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

  it('renders the typed bug form and empty prompt preview', () => {
    const fixture = create();

    expect(fixture.nativeElement.querySelector('#bug-title')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#bug-steps')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Complete the form and generate a prompt to preview it here.');
  });

  it('loads safe sample data, persists it immediately, and clears the stored draft', () => {
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

  it('generates a deterministic prompt and exposes it to the preview', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_BUG_PROMPT_INPUT);

    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(fixture.componentInstance.generatedPrompt()).toContain('Discount calculation mismatch');
    expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('🐛 Bug Title:');
    expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Copy generated prompt"]')).toBeTruthy();
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
