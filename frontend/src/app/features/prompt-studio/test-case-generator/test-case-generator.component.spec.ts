import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY_TEST_CASE_PROMPT_INPUT, SAMPLE_TEST_CASE_PROMPT_INPUT } from '../models/test-case-prompt.model';
import { PROMPT_STUDIO_DRAFT_KEYS } from '../services/prompt-storage.service';
import { TestCaseGeneratorComponent } from './test-case-generator.component';

describe('TestCaseGeneratorComponent', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [TestCaseGeneratorComponent],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  function create() {
    const fixture = TestBed.createComponent(TestCaseGeneratorComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the typed test-case form and counters', () => {
    const fixture = create();

    expect(fixture.nativeElement.querySelector('#tc-id')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-scenario-type')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-steps')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('0 chars');
  });

  it('loads, persists, restores, and clears the safe sample draft', () => {
    const fixture = create();

    fixture.componentInstance.loadSample();
    expect(fixture.componentInstance.form.getRawValue()).toEqual(SAMPLE_TEST_CASE_PROMPT_INPUT);
    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.testCase)).toBe(JSON.stringify(SAMPLE_TEST_CASE_PROMPT_INPUT));

    const restored = create();
    expect(restored.componentInstance.form.getRawValue()).toEqual(SAMPLE_TEST_CASE_PROMPT_INPUT);

    restored.componentInstance.clearForm();
    expect(restored.componentInstance.form.getRawValue()).toEqual(EMPTY_TEST_CASE_PROMPT_INPUT);
    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.testCase)).toBeNull();
  });

  it('renders restored select values instead of falling back to the first option', () => {
    const fixture = create();
    fixture.componentInstance.loadSample();
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('#tc-priority') as HTMLSelectElement).value).toBe('P1 (Critical)');
    expect((fixture.nativeElement.querySelector('#tc-scenario-type') as HTMLSelectElement).value).toBe('Happy Path');
  });

  it('generates the deterministic screenshot-inference prompt', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_TEST_CASE_PROMPT_INPUT);

    fixture.componentInstance.generate();
    fixture.detectChanges();

    expect(fixture.componentInstance.generatedPrompt()).toContain('TC-POS-042');
    expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('Suggestion Rationale');
  });
});
