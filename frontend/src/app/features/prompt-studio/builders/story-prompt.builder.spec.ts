import { StoryPromptBuilder } from './story-prompt.builder';
import { EMPTY_STORY_PROMPT_INPUT, SAMPLE_STORY_PROMPT_INPUT } from '../models/story-prompt.model';

describe('StoryPromptBuilder', () => {
  const builder = new StoryPromptBuilder();

  it('builds the deterministic foundation prompt from supplied facts', () => {
    const output = builder.build(SAMPLE_STORY_PROMPT_INPUT);

    expect(output).toBe(builder.build(SAMPLE_STORY_PROMPT_INPUT));
    expect(output).toContain('QA support engineer');
    expect(output).toContain('Review flagged order payloads before submission');
    expect(output).toContain('## Refined Title');
    expect(output).toContain('## Open Questions');
  });

  it('marks unsupported fields as needs investigation', () => {
    const output = builder.build(EMPTY_STORY_PROMPT_INPUT);

    expect(output).toContain('[NEEDS INVESTIGATION]');
  });
});
