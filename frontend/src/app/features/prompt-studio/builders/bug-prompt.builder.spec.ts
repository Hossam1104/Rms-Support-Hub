import { BugPromptBuilder } from './bug-prompt.builder';
import { BugPromptInput, EMPTY_BUG_PROMPT_INPUT, SAMPLE_BUG_PROMPT_INPUT } from '../models/bug-prompt.model';

describe('BugPromptBuilder', () => {
  const builder = new BugPromptBuilder();

  it('uses the fixed eleven-heading contract in order', () => {
    const output = builder.build(SAMPLE_BUG_PROMPT_INPUT);
    const headings = [
      '🐛 Bug Title', '📦 Module / Feature', '🌐 Environment', '⚙️ Application / Build Version',
      '🔧 Preconditions', '👣 Steps to Reproduce', '✅ Expected Result', '❌ Actual Result',
      '🚨 Severity', '📌 Priority', '📎 Evidence/Attachments'
    ];
    const positions = headings.map(heading => output.indexOf(heading));

    expect(positions.every(position => position >= 0)).toBe(true);
    expect(positions).toEqual([...positions].sort((left, right) => left - right));
    expect(output).toContain('no additional report headings');
    expect(output).not.toContain('Business Impact');
    expect(output).not.toContain('Root Cause');
    expect(output).not.toContain('Fix Acceptance Criteria');
  });

  it('preserves confirmed facts and produces deterministic output', () => {
    const first = builder.build(SAMPLE_BUG_PROMPT_INPUT);

    expect(first).toBe(builder.build(SAMPLE_BUG_PROMPT_INPUT));
    expect(first).toContain('Status filter resets after refreshing the order request list');
    expect(first).toContain('order-filter-refresh.png');
    expect(first).toContain('atomic, reproducible actions');
    expect(first).toContain('High');
    expect(first).toContain('P1');
  });

  it('keeps missing facts inline and does not invent a root cause', () => {
    const output = builder.build(EMPTY_BUG_PROMPT_INPUT);

    expect(output).toContain('- **Environment:** [NEEDS INVESTIGATION]');
    expect(output).toContain('- **Application / Build Version:** [NEEDS INVESTIGATION]');
    expect(output).toContain('Never invent environment, version');
    expect(output).not.toContain('Root Cause:');
  });

  it('preserves multiline steps and supplied severity and priority', () => {
    const input: BugPromptInput = {
      ...SAMPLE_BUG_PROMPT_INPUT,
      steps: 'Open the screen\nObserve the warning',
      severity: 'Critical',
      priority: 'P0'
    };
    const output = builder.build(input);

    expect(output).toContain('  Open the screen\n  Observe the warning');
    expect(output).toContain('- **Severity:** Critical');
    expect(output).toContain('- **Priority:** P0');
  });
});
