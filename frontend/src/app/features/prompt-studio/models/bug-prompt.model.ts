export type BugDetailLevel = 'Concise' | 'Standard' | 'Deep';
export type BugTargetFormat = 'Generic Markdown' | 'Jira' | 'Azure DevOps';

export const BUG_DETAIL_LEVELS = ['Concise', 'Standard', 'Deep'] as const;
export const BUG_TARGET_FORMATS = ['Generic Markdown', 'Jira', 'Azure DevOps'] as const;
export const DEFAULT_BUG_DETAIL_LEVEL: BugDetailLevel = 'Standard';
export const DEFAULT_BUG_TARGET_FORMAT: BugTargetFormat = 'Generic Markdown';

export interface BugPromptInput {
  rawNotes: string;
  title: string;
  module: string;
  environment: string;
  buildVersion: string;
  preconditions: string;
  testData: string;
  steps: string;
  expectedResult: string;
  actualResult: string;
  frequency: string;
  severity: string;
  priority: string;
  impact: string;
  errorMessage: string;
  logs: string;
  attachments: string;
  relatedReference: string;
  detailLevel: BugDetailLevel;
  targetFormat: BugTargetFormat;
  includeMissingInformation: boolean;
  includeAcceptanceCriteria: boolean;
  includeRetestChecklist: boolean;
  includeRegressionScope: boolean;
}

export const EMPTY_BUG_PROMPT_INPUT: BugPromptInput = {
  rawNotes: '',
  title: '',
  module: '',
  environment: '',
  buildVersion: '',
  preconditions: '',
  testData: '',
  steps: '',
  expectedResult: '',
  actualResult: '',
  frequency: '',
  severity: '',
  priority: '',
  impact: '',
  errorMessage: '',
  logs: '',
  attachments: '',
  relatedReference: '',
  detailLevel: DEFAULT_BUG_DETAIL_LEVEL,
  targetFormat: DEFAULT_BUG_TARGET_FORMAT,
  includeMissingInformation: true,
  includeAcceptanceCriteria: true,
  includeRetestChecklist: false,
  includeRegressionScope: false
};

export const SAMPLE_BUG_PROMPT_INPUT: BugPromptInput = {
  rawNotes: 'The status filter selection is lost after refreshing the order request list. The list returns to all statuses even though the selected filter was still visible before refresh.',
  title: 'Status filter resets after refreshing the order request list',
  module: 'Order request list filtering',
  environment: 'Testing environment in a Chromium browser',
  buildVersion: 'QA build 2026.08.01',
  preconditions: 'A test user can access the order request list and the list contains requests with at least two different statuses.',
  testData: 'Non-production order requests in Pending and Completed statuses',
  steps: '1. Open the order request list.\n2. Select the Pending status filter.\n3. Confirm that only Pending requests are displayed.\n4. Refresh the browser page.\n5. Review the active filter and returned list.',
  expectedResult: 'The Pending filter remains selected after refresh and the list continues to display only Pending requests.',
  actualResult: 'The filter control appears to reset and the list displays requests from all statuses after refresh.',
  frequency: 'Always after a browser refresh while a status filter is selected',
  severity: 'Major',
  priority: 'P1',
  impact: 'QA and support users may review or act on requests outside the intended status scope.',
  errorMessage: 'No error message is displayed.',
  logs: 'Browser console reference: order-filter-refresh-console.txt',
  attachments: 'order-filter-refresh.png, order-filter-refresh-console.txt',
  relatedReference: 'QA-EXAMPLE-FILTER-001',
  detailLevel: DEFAULT_BUG_DETAIL_LEVEL,
  targetFormat: DEFAULT_BUG_TARGET_FORMAT,
  includeMissingInformation: true,
  includeAcceptanceCriteria: true,
  includeRetestChecklist: false,
  includeRegressionScope: false
};

function readText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function readBoolean(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}

function readDetailLevel(value: unknown): BugDetailLevel {
  return value === 'Concise' || value === 'Standard' || value === 'Deep' ? value : DEFAULT_BUG_DETAIL_LEVEL;
}

function readTargetFormat(value: unknown): BugTargetFormat {
  return value === 'Generic Markdown' || value === 'Jira' || value === 'Azure DevOps' ? value : DEFAULT_BUG_TARGET_FORMAT;
}

/** Supplies defaults when restoring a six-field Session 04 draft. */
export function normalizeBugPromptInput(draft: Partial<BugPromptInput> | null | undefined): BugPromptInput {
  const source = draft ?? {};

  return {
    rawNotes: readText(source.rawNotes),
    title: readText(source.title),
    module: readText(source.module),
    environment: readText(source.environment),
    buildVersion: readText(source.buildVersion),
    preconditions: readText(source.preconditions),
    testData: readText(source.testData),
    steps: readText(source.steps),
    expectedResult: readText(source.expectedResult),
    actualResult: readText(source.actualResult),
    frequency: readText(source.frequency),
    severity: readText(source.severity),
    priority: readText(source.priority),
    impact: readText(source.impact),
    errorMessage: readText(source.errorMessage),
    logs: readText(source.logs),
    attachments: readText(source.attachments),
    relatedReference: readText(source.relatedReference),
    detailLevel: readDetailLevel(source.detailLevel),
    targetFormat: readTargetFormat(source.targetFormat),
    includeMissingInformation: readBoolean(source.includeMissingInformation, EMPTY_BUG_PROMPT_INPUT.includeMissingInformation),
    includeAcceptanceCriteria: readBoolean(source.includeAcceptanceCriteria, EMPTY_BUG_PROMPT_INPUT.includeAcceptanceCriteria),
    includeRetestChecklist: readBoolean(source.includeRetestChecklist, EMPTY_BUG_PROMPT_INPUT.includeRetestChecklist),
    includeRegressionScope: readBoolean(source.includeRegressionScope, EMPTY_BUG_PROMPT_INPUT.includeRegressionScope)
  };
}
