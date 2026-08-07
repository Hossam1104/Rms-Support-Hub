import { TestCasePromptBuilder } from './test-case-prompt.builder';
import { EMPTY_TEST_CASE_PROMPT_INPUT, SAMPLE_TEST_CASE_PROMPT_INPUT } from '../models/test-case-prompt.model';

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
  });
});
