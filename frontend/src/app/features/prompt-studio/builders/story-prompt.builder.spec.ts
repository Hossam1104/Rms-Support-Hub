import { StoryPromptBuilder } from './story-prompt.builder';
import {
  EMPTY_STORY_PROMPT_INPUT,
  SAMPLE_STORY_PROMPT_INPUT,
  StoryPromptInput,
  normalizeStoryPromptInput
} from '../models/story-prompt.model';

describe('StoryPromptBuilder', () => {
  const builder = new StoryPromptBuilder();

  function withInput(overrides: Partial<StoryPromptInput>): StoryPromptInput {
    return { ...SAMPLE_STORY_PROMPT_INPUT, ...overrides };
  }

  it('builds deterministic output and preserves supplied facts without mutation', () => {
    const input = { ...SAMPLE_STORY_PROMPT_INPUT };
    const before = { ...input };

    const output = builder.build(input);

    expect(output).toBe(builder.build(input));
    expect(output).toContain(input.actor);
    expect(output).toContain(input.businessGoal);
    expect(output).toContain(input.businessRules);
    expect(output).toContain(input.desiredBehavior);
    expect(input).toEqual(before);
  });

  it('keeps assumptions labeled and supplies clarification guidance for missing values', () => {
    const output = builder.build(EMPTY_STORY_PROMPT_INPUT);

    expect(output).toContain('Provided Assumption');
    expect(output).toContain('Do not promote a Provided Assumption');
    expect(output).toContain('[NEEDS CLARIFICATION]');
    expect(output).toContain('If Out of Scope was not provided, mark it as not defined');
  });

  it.each([
    ['Checklist', '# Acceptance Criteria Format: Checklist'],
    ['Given / When / Then', '# Acceptance Criteria Format: Given / When / Then'],
    ['Both', '# Acceptance Criteria Format: Both']
  ] as const)('supports the %s acceptance criteria style', (style, marker) => {
    const output = builder.build(withInput({ acceptanceCriteriaStyle: style }));

    expect(output).toContain(marker);
    expect(output).toContain('observable');
    expect(output).toContain('testable');
  });

  it('requests focused Given / When / Then semantics', () => {
    const output = builder.build(withInput({ acceptanceCriteriaStyle: 'Given / When / Then' }));

    expect(output).toContain('Given = known precondition');
    expect(output).toContain('When = one user or system action');
    expect(output).toContain('Then = one observable result');
  });

  it.each([
    ['Concise', 'focused acceptance criteria'],
    ['Standard', 'negative and boundary acceptance criteria'],
    ['Deep', 'data integrity']
  ] as const)('changes instructions for %s detail', (detailLevel, marker) => {
    const output = builder.build(withInput({ detailLevel }));

    expect(output).toContain(`Detail level: ${detailLevel}`);
    expect(output).toContain(marker);
  });

  it.each([
    ['Generic Markdown', '# Target Format: Generic Markdown'],
    ['Jira', '# Target Format: Jira'],
    ['Azure DevOps', '# Target Format: Azure DevOps']
  ] as const)('supports the %s target format without API integration', (targetFormat, marker) => {
    const output = builder.build(withInput({ targetFormat }));

    expect(output).toContain(marker);
    expect(output).toContain('do not call Jira or Azure DevOps APIs');
  });

  it('includes each enabled optional output section and can omit them', () => {
    const enabled = builder.build(withInput({
      includeOpenQuestions: true,
      includeQaImpact: true,
      includeDefinitionOfReady: true,
      includeSuggestedTestCoverage: true
    }));
    const omitted = builder.build(withInput({
      includeOpenQuestions: false,
      includeQaImpact: false,
      includeDefinitionOfReady: false,
      includeSuggestedTestCoverage: false
    }));

    expect(enabled).toContain('### Open Questions');
    expect(enabled).toContain('### QA Impact');
    expect(enabled).toContain('### Definition of Ready');
    expect(enabled).toContain('### Suggested Test Coverage');
    expect(omitted).not.toContain('### Open Questions');
    expect(omitted).not.toContain('### QA Impact');
    expect(omitted).not.toContain('### Definition of Ready');
    expect(omitted).not.toContain('### Suggested Test Coverage');
  });

  it('keeps the primary sections in a stable order', () => {
    const output = builder.build(SAMPLE_STORY_PROMPT_INPUT);
    const sections = [
      '# Role',
      '# Source of Truth and Safety',
      '# Supplied Story Information',
      '# Refinement Instructions',
      '# Acceptance Criteria Format: Both',
      '# Validation and Non-Functional Guidance',
      '# Final Response Contract',
      '# Target Format: Generic Markdown',
      '# Optional Output Sections'
    ];
    const positions = sections.map(section => output.indexOf(section));

    expect(positions.every(position => position >= 0)).toBe(true);
    expect(positions).toEqual([...positions].sort((left, right) => left - right));
  });

  it('does not invent performance values or security requirements', () => {
    const output = builder.build(EMPTY_STORY_PROMPT_INPUT);

    expect(output).toContain('Do not invent error messages, roles, permissions, thresholds, response-time values');
    expect(output).toContain('When performance matters but no target is supplied');
    expect(output).not.toContain('2 seconds');
    expect(output).not.toContain('500 ms');
    expect(output).not.toContain('All users must authenticate');
  });

  it('normalizes the smaller Session 04 draft with expanded defaults', () => {
    const normalized = normalizeStoryPromptInput({
      title: 'Legacy title',
      actor: 'Legacy actor',
      desiredBehavior: 'Legacy desired behavior'
    });

    expect(normalized.title).toBe('Legacy title');
    expect(normalized.actor).toBe('Legacy actor');
    expect(normalized.desiredBehavior).toBe('Legacy desired behavior');
    expect(normalized.detailLevel).toBe('Standard');
    expect(normalized.acceptanceCriteriaStyle).toBe('Both');
    expect(normalized.includeOpenQuestions).toBe(true);
    expect(normalized.includeDefinitionOfReady).toBe(false);
  });
});
