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

    it('renders the canonical source form without retired configuration controls', () => {
        const fixture = create();

        expect(fixture.nativeElement.querySelector('#tc-reference')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-title')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-module')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-scenario')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-preconditions')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-test-data')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-steps')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-expected')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-priority')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-attachments')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#tc-output-type')).toBeNull();
        expect(fixture.nativeElement.querySelector('#tc-expected-mode')).toBeNull();
        expect(fixture.nativeElement.querySelector('#tc-automation')).toBeNull();
        expect(fixture.nativeElement.querySelector('#tc-regression')).toBeNull();
        expect(fixture.nativeElement.querySelectorAll('.test-case-form__section')).toHaveLength(4);
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

    it('restores legacy semantic fields while ignoring retired options', () => {
        localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS.testCase, JSON.stringify({
            testCaseId: 'TC-LEGACY-001',
            name: 'Legacy test case',
            targetSection: 'Legacy module',
            scenarioType: 'Happy Path',
            steps: 'Open the screen',
            expectedResult: 'The screen is visible',
            outputType: 'Scenario Matrix'
        }));

        const fixture = create();
        const value = fixture.componentInstance.form.getRawValue();

        expect(value.title).toBe('Legacy test case');
        expect(value.module).toBe('Legacy module');
        expect(value.scenario).toBe('Happy Path');
        expect(value.expectedResult).toBe('The screen is visible');
        expect(value).not.toHaveProperty('outputType');
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

    it('generates the deterministic prompt and exposes copy and download actions', () => {
        const fixture = create();
        fixture.componentInstance.form.setValue(SAMPLE_TEST_CASE_PROMPT_INPUT);

        fixture.componentInstance.generate();
        fixture.detectChanges();

        expect(fixture.componentInstance.generatedPrompt()).toContain('POS-PAYMENTS-042');
        expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('🧪 Test Case Title');
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Copy generated prompt"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as Markdown"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as plain text"]')).toBeTruthy();
    });
});
