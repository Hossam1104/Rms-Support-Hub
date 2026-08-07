import { TestCasePromptBuilder } from './test-case-prompt.builder';
import {
  EMPTY_TEST_CASE_PROMPT_INPUT,
  SAMPLE_TEST_CASE_PROMPT_INPUT,
  TEST_CASE_OUTPUT_TYPES,
  TEST_CASE_SCENARIO_TYPES,
  TestCasePromptInput,
  normalizeTestCasePromptInput
} from '../models/test-case-prompt.model';
import { analyzeTestCaseInput } from './test-case-prompt.builder';

describe('TestCasePromptBuilder', () => {
  const builder = new TestCasePromptBuilder();

  it('preserves supplied metadata and deterministic output', () => {
    const output = builder.build(SAMPLE_TEST_CASE_PROMPT_INPUT);

    expect(output).toBe(builder.build(SAMPLE_TEST_CASE_PROMPT_INPUT));
    expect(output).toContain('TC-POS-042');
    expect(output).toContain('Happy Path');
    expect(output).toContain('📋 **Test Case Details**');
    expect(output).toContain('💡 **Suggestion Rationale**');
  });

  it('marks missing fields for screenshot-based inference', () => {
    const output = builder.build(EMPTY_TEST_CASE_PROMPT_INPUT);

    expect(output).toContain('NULL [Generate & Suggest based on screenshots/evidence]');
    expect(output).toContain('None specified (Refer to standard visual upload)');
    expect(output).toContain('Missing-Information and Quality Warnings');
    expect(output).toContain('Attachments / Evidence');
  });

  it.each([
    ['Single Test Case', '# Output Type: Single Test Case'],
    ['Scenario Matrix', '# Output Type: Scenario Matrix'],
    ['Jira-friendly', '# Output Type: Jira-friendly'],
    ['Spreadsheet-friendly', '# Output Type: Spreadsheet-friendly']
  ] as const)('supports the %s output type', (outputType, marker) => {
    const output = builder.build({ ...SAMPLE_TEST_CASE_PROMPT_INPUT, outputType });

    expect(output).toContain(marker);
    expect(output).toContain(outputType);
    if (outputType === 'Spreadsheet-friendly') expect(output).toContain('Do not generate an actual spreadsheet file');
  });

  it('supports every requested scenario category', () => {
    expect(TEST_CASE_SCENARIO_TYPES).toEqual(expect.arrayContaining([
      'Happy',
      'Negative',
      'Edge/Boundary',
      'UI',
      'Navigation/State',
      'Security',
      'Data Integrity',
      'Performance',
      'Accessibility',
      'Localization',
      'Recovery'
    ]));
  });

  it('changes the contract for per-step expected results', () => {
    const output = builder.build({ ...SAMPLE_TEST_CASE_PROMPT_INPUT, expectedResultMode: 'Per Step' });

    expect(output).toContain('Expected Result Mode:** Per Step');
    expect(output).toContain('Pair every atomic step with exactly one observable expected result');
    expect(output).toContain('Expected Result per Step');
  });

  it('reports vague, duplicate, and misaligned execution information', () => {
    const input: TestCasePromptInput = {
      ...SAMPLE_TEST_CASE_PROMPT_INPUT,
      steps: '1. Click it properly\n2. Click it properly',
      expectedResult: 'It works',
      expectedResultMode: 'Per Step'
    };

    const warnings = analyzeTestCaseInput(input);

    expect(warnings.map(warning => warning.kind)).toEqual(expect.arrayContaining([
      'Vague steps',
      'Vague expected result',
      'Duplicate steps',
      'No observable outcome',
      'Step/result mismatch'
    ]));
    expect(builder.build(input)).toContain('Replace vague wording');
  });

  it('normalizes legacy drafts without inventing user values', () => {
    const normalized = normalizeTestCasePromptInput({
      testCaseId: 'TC-LEGACY-001',
      scenarioType: 'Happy Path',
      name: 'Legacy test case',
      targetSection: 'Legacy module',
      steps: 'Open the screen',
      expectedResult: 'The screen is visible',
      attachments: 'legacy.png'
    });

    expect(normalized.testCaseId).toBe('TC-LEGACY-001');
    expect(normalized.scenarioType).toBe('Happy Path');
    expect(normalized.targetSection).toBe('Legacy module');
    expect(normalized.requirementReference).toBe('');
    expect(normalized.outputType).toBe('Single Test Case');
  });
});
