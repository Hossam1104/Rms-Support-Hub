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
    expect(fixture.nativeElement.querySelector('#tc-reference')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-scenario-type')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-environment')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-test-data')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-steps')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-expected-mode')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-postconditions')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-cleanup')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-automation')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-regression')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('#tc-output-type')).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('.test-case-form__section')).toHaveLength(5);
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
    expect((fixture.nativeElement.querySelector('#tc-output-type') as HTMLSelectElement).value).toBe('Single Test Case');
  });

  it('restores an old draft with safe defaults for expanded fields', () => {
    localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS.testCase, JSON.stringify({
      testCaseId: 'TC-LEGACY-001',
      name: 'Legacy test case',
      steps: 'Open the screen',
      expectedResult: 'The screen is visible',
      attachments: 'legacy.png'
    }));

    const fixture = create();
    const value = fixture.componentInstance.form.getRawValue();

    expect(value.testCaseId).toBe('TC-LEGACY-001');
    expect(value.name).toBe('Legacy test case');
    expect(value.requirementReference).toBe('');
    expect(value.expectedResultMode).toBe('Final Result');
    expect(value.outputType).toBe('Single Test Case');
  });

  it('does not recreate a draft from a pending write after clear', () => {
    vi.useFakeTimers();
    try {
      const fixture = create();
      fixture.componentInstance.form.patchValue({ testData: 'Pending test data' });
      fixture.componentInstance.clearForm();
      vi.advanceTimersByTime(300);

      expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.testCase)).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it('changes generated output for expected-result mode and output type', () => {
    const fixture = create();
    fixture.componentInstance.form.setValue(SAMPLE_TEST_CASE_PROMPT_INPUT);

    fixture.componentInstance.generate();
    const single = fixture.componentInstance.generatedPrompt();

    fixture.componentInstance.form.patchValue({ expectedResultMode: 'Per Step', outputType: 'Scenario Matrix' });
    fixture.componentInstance.generate();
    const matrix = fixture.componentInstance.generatedPrompt();

    expect(matrix).not.toBe(single);
    expect(matrix).toContain('# Output Type: Scenario Matrix');
    expect(matrix).toContain('Expected Result Mode:** Per Step');
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
