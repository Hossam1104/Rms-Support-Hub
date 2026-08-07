import { BugPromptBuilder } from './bug-prompt.builder';
import {
  BugPromptInput,
  EMPTY_BUG_PROMPT_INPUT,
  SAMPLE_BUG_PROMPT_INPUT
} from '../models/bug-prompt.model';

describe('BugPromptBuilder', () => {
  const builder = new BugPromptBuilder();

  it('preserves supplied bug facts deterministically', () => {
    const first = builder.build(SAMPLE_BUG_PROMPT_INPUT);
    const second = builder.build(SAMPLE_BUG_PROMPT_INPUT);

    expect(first).toBe(second);
    expect(first).toContain('Status filter resets');
    expect(first).toContain('order-filter-refresh.png');
    expect(first).toContain('CONFIRMED FACT');
    expect(first).toContain('Potential Cause to Validate');
  });

  it('uses explicit missing-information markers for empty fields', () => {
    const output = builder.build(EMPTY_BUG_PROMPT_INPUT);

    expect(output).toContain('- **Existing Bug Title:** [NEEDS INVESTIGATION]');
    expect(output).toContain('- **Steps to Reproduce:** [NEEDS INVESTIGATION]');
    expect(output).toContain('Do not invent or silently assume an environment');
  });

  it('preserves multiline facts and supplied severity and priority', () => {
    const input: BugPromptInput = {
      ...SAMPLE_BUG_PROMPT_INPUT,
      steps: 'Open the screen\nObserve the warning',
      severity: 'Blocker',
      priority: 'P0'
    };

    const output = builder.build(input);

    expect(output).toContain('  Open the screen\n  Observe the warning');
    expect(output).toContain('- **Severity:** Blocker');
    expect(output).toContain('- **Priority:** P0');
    expect(output).toContain('Severity and Priority were supplied');
    expect(output).not.toContain('Severity was not supplied');
  });

  it('adds suggestions only when severity or priority is missing', () => {
    const output = builder.build({
      ...SAMPLE_BUG_PROMPT_INPUT,
      severity: '',
      priority: ''
    });

    expect(output).toContain('Severity was not supplied');
    expect(output).toContain('Priority was not supplied');
    expect(output).toContain('[Suggested]');
  });

  it('supports all detail levels without changing the stable input order', () => {
    const concise = builder.build({ ...SAMPLE_BUG_PROMPT_INPUT, detailLevel: 'Concise' });
    const standard = builder.build({ ...SAMPLE_BUG_PROMPT_INPUT, detailLevel: 'Standard' });
    const deep = builder.build({ ...SAMPLE_BUG_PROMPT_INPUT, detailLevel: 'Deep' });

    expect(concise).toContain('Detail level: Concise');
    expect(standard).toContain('Detail level: Standard');
    expect(deep).toContain('Detail level: Deep');
    expect(deep).toContain('contradiction checks');
    expect(standard.indexOf('Existing Bug Title')).toBeLessThan(standard.indexOf('Steps to Reproduce'));
    expect(standard.indexOf('Steps to Reproduce')).toBeLessThan(standard.indexOf('Expected Result'));
    expect(standard.indexOf('Expected Result')).toBeLessThan(standard.indexOf('Actual Result'));
  });

  it('supports each formatting target through shared output sections', () => {
    expect(builder.build({ ...SAMPLE_BUG_PROMPT_INPUT, targetFormat: 'Generic Markdown' })).toContain('🐛 Bug Title');
    expect(builder.build({ ...SAMPLE_BUG_PROMPT_INPUT, targetFormat: 'Jira' })).toContain('Target Format: Jira');
    expect(builder.build({ ...SAMPLE_BUG_PROMPT_INPUT, targetFormat: 'Azure DevOps' })).toContain('Target Format: Azure DevOps');
  });

  it('honors optional output-section toggles', () => {
    const output = builder.build({
      ...SAMPLE_BUG_PROMPT_INPUT,
      includeMissingInformation: false,
      includeAcceptanceCriteria: false,
      includeRetestChecklist: true,
      includeRegressionScope: true
    });

    expect(output).not.toContain('### Missing Information');
    expect(output).not.toContain('### Fix Acceptance Criteria');
    expect(output).toContain('### Retest Checklist');
    expect(output).toContain('### Regression Scope');
  });

  it('does not instruct unsupported root cause as fact and preserves evidence references', () => {
    const output = builder.build(SAMPLE_BUG_PROMPT_INPUT);

    expect(output).not.toContain('Root Cause:');
    expect(output).toContain('order-filter-refresh-console.txt');
    expect(output).toContain('QA-EXAMPLE-FILTER-001');
  });
});
