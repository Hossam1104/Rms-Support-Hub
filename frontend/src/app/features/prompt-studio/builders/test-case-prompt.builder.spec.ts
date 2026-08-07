import { TestCasePromptBuilder } from './test-case-prompt.builder';
import {
    EMPTY_TEST_CASE_PROMPT_INPUT,
    SAMPLE_TEST_CASE_PROMPT_INPUT,
    TestCasePromptInput,
    normalizeTestCasePromptInput
} from '../models/test-case-prompt.model';

describe('TestCasePromptBuilder', () => {
    const builder = new TestCasePromptBuilder();
    const headings = [
        '🧪 Test Case Title',
        '📦 Module / Feature',
        '🎯 Scenario / Objective',
        '🔧 Preconditions',
        '🧾 Test Data',
        '👣 Steps',
        '✅ Expected Result',
        '🚨 Priority',
        '📎 Evidence / Attachments'
    ];

    it('uses the fixed nine-heading contract in order', () => {
        const output = builder.build(SAMPLE_TEST_CASE_PROMPT_INPUT);
        const positions = headings.map(heading => output.indexOf(heading));

        expect(positions.every(position => position >= 0)).toBe(true);
        expect(positions).toEqual([...positions].sort((left, right) => left - right));
        expect(output).toContain('no additional test-case headings');
        expect(output).not.toContain('Automation Candidacy');
        expect(output).not.toContain('Scenario Matrix');
        expect(output).not.toContain('Suggestion Rationale');
    });

    it('preserves scenario facts and requests atomic observable execution', () => {
        const output = builder.build(SAMPLE_TEST_CASE_PROMPT_INPUT);

        expect(output).toBe(builder.build(SAMPLE_TEST_CASE_PROMPT_INPUT));
        expect(output).toContain(SAMPLE_TEST_CASE_PROMPT_INPUT.title);
        expect(output).toContain(SAMPLE_TEST_CASE_PROMPT_INPUT.scenario);
        expect(output).toContain('ordered, atomic, reproducible actions');
        expect(output).toContain('Expected Result observable');
        expect(output).toContain('pos_payment_terminal_approved.png');
    });

    it('keeps missing test data inline and labels evidence-supported inference', () => {
        const output = builder.build(EMPTY_TEST_CASE_PROMPT_INPUT);

        expect(output).toContain('- **Test Data:** [NEEDS INVESTIGATION]');
        expect(output).toContain('include it in the relevant field and mark it [Inferred]');
        expect(output).not.toContain('Inference:');
        expect(output).not.toContain('Quality Warnings');
    });

    it('normalizes legacy semantic fields and ignores retired options', () => {
        const normalized = normalizeTestCasePromptInput({
            name: 'Legacy test case',
            targetSection: 'Legacy module',
            scenarioType: 'Happy Path',
            steps: 'Open the screen',
            expectedResult: 'The screen is visible',
            outputType: 'Scenario Matrix',
            expectedResultMode: 'Per Step'
        } as Partial<TestCasePromptInput> & Record<string, unknown>);

        expect(normalized.title).toBe('Legacy test case');
        expect(normalized.module).toBe('Legacy module');
        expect(normalized.scenario).toBe('Happy Path');
        expect(normalized.expectedResult).toBe('The screen is visible');
        expect(normalized).not.toHaveProperty('outputType');
        expect(normalized).not.toHaveProperty('expectedResultMode');
    });
});
