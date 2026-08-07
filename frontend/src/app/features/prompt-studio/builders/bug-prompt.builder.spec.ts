import { BugPromptBuilder } from './bug-prompt.builder';
import { EMPTY_BUG_PROMPT_INPUT, SAMPLE_BUG_PROMPT_INPUT } from '../models/bug-prompt.model';

describe('BugPromptBuilder', () => {
  const builder = new BugPromptBuilder();

  it('preserves supplied bug facts deterministically', () => {
    const first = builder.build(SAMPLE_BUG_PROMPT_INPUT);
    const second = builder.build(SAMPLE_BUG_PROMPT_INPUT);

    expect(first).toBe(second);
    expect(first).toContain('Discount calculation mismatch');
    expect(first).toContain('pos_checkout_calculation_error.png');
    expect(first).toContain('🐛 Bug Title:');
    expect(first).toContain('❌ Actual Result:');
  });

  it('uses explicit missing-information markers for empty fields', () => {
    const output = builder.build(EMPTY_BUG_PROMPT_INPUT);

    expect(output).toContain('{{INSERT_BUG_TITLE}}');
    expect(output).toContain('{{INSERT_STEPS_TO_REPRODUCE_OR_LEAVE_BLANK}}');
    expect(output).toContain('- **Attachments:** None provided');
  });
});
