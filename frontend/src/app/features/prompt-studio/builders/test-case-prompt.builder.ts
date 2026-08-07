import { TestCasePromptInput } from '../models/test-case-prompt.model';

function formatTestCasePromptBlock(label: string, value: string): string {
  const trimmed = value.trim();
  if (!trimmed) return `- **${label}:** NULL [Generate & Suggest based on screenshots/evidence]`;
  if (!trimmed.includes('\n')) return `- **${label}:** ${trimmed}`;

  const formattedLines = trimmed
    .split('\n')
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => `  ${line}`)
    .join('\n');

  return `- **${label}:**\n${formattedLines}`;
}

export type TestCaseQualityWarningKind = 'Missing information' | 'Vague steps' | 'Vague expected result' | 'Duplicate steps' | 'No observable outcome' | 'Step/result mismatch';

export interface TestCaseQualityWarning {
  kind: TestCaseQualityWarningKind;
  message: string;
}

const MISSING_INFORMATION_FIELDS: Array<[keyof TestCasePromptInput, string]> = [
  ['testCaseId', 'Test Case ID'],
  ['requirementReference', 'Requirement / Story Reference'],
  ['scenarioType', 'Scenario Category'],
  ['name', 'Test Case Name'],
  ['targetSection', 'Module / Target Section'],
  ['environment', 'Environment'],
  ['priority', 'Priority'],
  ['preconditions', 'Preconditions'],
  ['testData', 'Test Data'],
  ['steps', 'Steps'],
  ['expectedResult', 'Expected Result'],
  ['postconditions', 'Postconditions'],
  ['cleanup', 'Cleanup'],
  ['attachments', 'Attachments / Evidence'],
  ['automationCandidacy', 'Automation Candidacy'],
  ['regressionTag', 'Regression Tag']
];

const VAGUE_LANGUAGE = /\b(?:properly|correctly|appropriately|normally|as expected|the thing|something|some data|valid data|invalid data|do the action|perform action|check it|verify it|works|handled)\b/i;
const OBSERVABLE_LANGUAGE = /\b(?:display|displayed|show|shown|visible|contains|equals|matches|remains|stays|redirect|navigat|enabled|disabled|created|updated|deleted|saved|returned|listed|recorded|logged|status|message|error|warning|receipt|download|focus|selected|cleared|blocked|rejected|approved|synchronized|printed)\w*\b/i;

function splitLines(value: string): string[] {
  return value
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean);
}

function normalizeStep(value: string): string {
  return value
    .replace(/^\s*(?:\d+[.)]|[-*])\s*/, '')
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function hasVagueLanguage(value: string): boolean {
  return VAGUE_LANGUAGE.test(value);
}

export function analyzeTestCaseInput(input: TestCasePromptInput): TestCaseQualityWarning[] {
  const warnings: TestCaseQualityWarning[] = [];
  const missingFields = MISSING_INFORMATION_FIELDS
    .filter(([field]) => !input[field].trim())
    .map(([, label]) => label);

  if (missingFields.length) {
    warnings.push({
      kind: 'Missing information',
      message: `Provide or explicitly confirm: ${missingFields.join(', ')}.`
    });
  }

  if (hasVagueLanguage(input.steps)) {
    warnings.push({
      kind: 'Vague steps',
      message: 'Replace vague wording with one atomic action and the target control or state.'
    });
  }

  if (hasVagueLanguage(input.expectedResult)) {
    warnings.push({
      kind: 'Vague expected result',
      message: 'The expected result uses vague wording. State the observable value, message, state, record, or navigation outcome.'
    });
  }

  const steps = splitLines(input.steps);
  const normalizedSteps = steps.map(normalizeStep).filter(Boolean);
  const duplicateSteps = normalizedSteps.filter((step, index) => normalizedSteps.indexOf(step) !== index);
  if (duplicateSteps.length) {
    warnings.push({
      kind: 'Duplicate steps',
      message: 'Duplicate execution steps were detected. Keep each step atomic and remove repeated actions.'
    });
  }

  if (input.expectedResult.trim() && !OBSERVABLE_LANGUAGE.test(input.expectedResult)) {
    warnings.push({
      kind: 'No observable outcome',
      message: 'The expected result does not contain an obvious observable outcome. Add a measurable or visible result.'
    });
  }

  if (input.expectedResultMode === 'Per Step' && steps.length > 1) {
    const expectedResults = splitLines(input.expectedResult);
    if (expectedResults.length !== steps.length) {
      warnings.push({
        kind: 'Step/result mismatch',
        message: `Per-step mode has ${steps.length} steps but ${expectedResults.length} expected-result lines. Align the two lists.`
      });
    }
  }

  return warnings;
}

export class TestCasePromptBuilder {
  build(input: TestCasePromptInput): string {
    return [
      this.buildRole(),
      this.buildSourceOfTruth(),
      this.buildInputData(input),
      this.buildQualityWarnings(input),
      this.buildInferenceInstructions(),
      this.buildOutputContract(input),
      this.buildFormatInstructions(input.outputType, input.expectedResultMode)
    ].join('\n\n');
  }

  private buildRole(): string {
    return `# Role
Act as a Principal Software Quality Assurance Engineer. Analyze the attached user interface screenshot(s), other evidence, and supplied metadata to generate or refine a production-ready manual QA test case. Keep the result deterministic, explicit, and useful for execution and review.`;
  }

  private buildSourceOfTruth(): string {
    return `# Source of Truth and Safety
- Attached screenshots and evidence represent the actual user interface and system state for the screenshot-inference use case.
- Non-empty metadata values are supplied constraints. Preserve their meaning and exact references.
- Empty fields are missing information, not permission to invent requirements, data, permissions, environments, API behavior, database behavior, thresholds, or expected values.
- When evidence supports a candidate, label it exactly with \"✨ [Suggested]\" and explain it under \"Suggestion Rationale\".
- Keep confirmed facts, supplied assumptions, suggested values, and unresolved questions visibly distinct.`;
  }

  private buildInputData(input: TestCasePromptInput): string {
    const attachments = input.attachments.trim() || 'None specified (Refer to standard visual upload)';

    return `# Metadata Input Configuration
${formatTestCasePromptBlock('Test Case ID', input.testCaseId)}
${formatTestCasePromptBlock('Requirement / Story Reference', input.requirementReference)}
${formatTestCasePromptBlock('Scenario Category', input.scenarioType)}
${formatTestCasePromptBlock('Test Case Name', input.name)}
${formatTestCasePromptBlock('Module / Target Section', input.targetSection)}
${formatTestCasePromptBlock('Environment', input.environment)}
${formatTestCasePromptBlock('Priority', input.priority)}
${formatTestCasePromptBlock('Preconditions', input.preconditions)}
${formatTestCasePromptBlock('Test Data', input.testData)}
${formatTestCasePromptBlock('Steps to Execute', input.steps)}
${formatTestCasePromptBlock('Expected Result', input.expectedResult)}
- **Expected Result Mode:** ${input.expectedResultMode}
${formatTestCasePromptBlock('Postconditions', input.postconditions)}
${formatTestCasePromptBlock('Cleanup', input.cleanup)}
- **Attachments / Evidence References:** ${attachments}
${formatTestCasePromptBlock('Automation Candidacy', input.automationCandidacy)}
${formatTestCasePromptBlock('Regression Tag', input.regressionTag)}
- **Output Type:** ${input.outputType}`;
  }

  private buildQualityWarnings(input: TestCasePromptInput): string {
    const warnings = analyzeTestCaseInput(input);
    const lines = warnings.length
      ? warnings.map(warning => `- **${warning.kind}:** ${warning.message}`)
      : ['- No deterministic warnings were detected from the supplied text.'];

    return `# Missing-Information and Quality Warnings
Review these warnings before finalizing the test case. Warnings guide clarification and do not block generation.
${lines.join('\n')}`;
  }

  private buildInferenceInstructions(): string {
    return `# Screenshot and Evidence Inference Instructions
If a field is marked as \"NULL [Generate & Suggest based on screenshots/evidence]\", inspect the screen structure, state indicators, controls, data fields, headers, validation messages, and supplied evidence to:
1. Suggest only values supported by visible evidence.
2. Recommend a structured ID using a category prefix when the ID is missing, such as TC-HP-###, TC-NEG-###, TC-EDGE-###, TC-SEC-###, TC-PERF-###, TC-ACC-###, TC-I18N-###, or TC-DB-###.
3. Convert visible flows into atomic numbered steps and observable expected outcomes.
4. Deduce a logical module or target section only when the evidence supports it.
5. Prefix every inferred value with \"✨ [Suggested]\" and explain why it was suggested.
6. Keep missing or contradictory information in a Missing Information section instead of silently resolving it.`;
  }

  private buildOutputContract(input: TestCasePromptInput): string {
    const expectedResultInstruction = input.expectedResultMode === 'Per Step'
      ? 'Pair every atomic step with exactly one observable expected result. Preserve the step order and flag any missing pairing.'
      : 'Provide one final observable expected result after the complete step sequence. Do not invent per-step outcomes that were not supplied or evidenced.';

    return `# Output Contract
Return only the requested ${input.outputType} test-case content in a clean Markdown-friendly structure. Do not call external services, Jira, Azure DevOps, or spreadsheet APIs.
- Expected result mode: ${input.expectedResultMode}. ${expectedResultInstruction}
- Mark inferred values with \"✨ [Suggested]\".
- Include Missing Information and Quality Warnings when warnings are present.
- Include a \"Suggestion Rationale\" section at the end for every inferred value.
- Keep actions atomic, remove duplicate steps, replace vague wording, and require an observable outcome.`;
  }

  private buildFormatInstructions(outputType: TestCasePromptInput['outputType'], expectedResultMode: TestCasePromptInput['expectedResultMode']): string {
    if (outputType === 'Scenario Matrix') {
      return `# Output Type: Scenario Matrix
Create a Markdown scenario matrix with one row per relevant scenario. Use columns for ID, requirement/story reference, category, name, module/target, environment, priority, preconditions, test data, steps, ${expectedResultMode === 'Per Step' ? 'expected result per step' : 'final expected result'}, postconditions, cleanup, attachments, automation candidacy, regression tag, and missing information.`;
    }

    if (outputType === 'Jira-friendly') {
      return `# Output Type: Jira-friendly
Format one test case for direct Jira description pasting. Use clear labels for Summary, Requirement / Story Reference, Scenario Category, Environment, Priority, Preconditions, Test Data, Steps, Expected Result, Postconditions, Cleanup, Attachments, Automation Candidacy, Regression Tag, Missing Information, and Suggestion Rationale.`;
    }

    if (outputType === 'Spreadsheet-friendly') {
      return `# Output Type: Spreadsheet-friendly
Produce a Markdown table or delimiter-safe tabular block with stable columns for ID, requirement/story reference, category, name, module/target, environment, priority, preconditions, test data, steps, ${expectedResultMode === 'Per Step' ? 'expected result per step' : 'final expected result'}, postconditions, cleanup, attachments, automation candidacy, regression tag, and warnings. Do not generate an actual spreadsheet file.`;
    }

    return `# Output Type: Single Test Case
Format one complete test case with these stable sections: 📋 **Test Case Details**, Requirement / Story Reference, Scenario Category, Environment, Priority, Preconditions, Test Data, Steps to Execute, ${expectedResultMode === 'Per Step' ? 'Expected Result per Step' : 'Expected Result'}, Postconditions, Cleanup, Attachments & References, Automation Candidacy, Regression Tag, Missing Information, and 💡 **Suggestion Rationale**.`;
  }
}
