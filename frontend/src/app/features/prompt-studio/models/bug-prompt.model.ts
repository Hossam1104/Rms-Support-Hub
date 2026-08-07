export interface BugPromptInput {
  rawNotes: string;
  title: string;
  module: string;
  environment: string;
  buildVersion: string;
  preconditions: string;
  steps: string;
  expectedResult: string;
  actualResult: string;
  severity: string;
  priority: string;
  attachments: string;
}

export const EMPTY_BUG_PROMPT_INPUT: BugPromptInput = {
  rawNotes: '',
  title: '',
  module: '',
  environment: '',
  buildVersion: '',
  preconditions: '',
  steps: '',
  expectedResult: '',
  actualResult: '',
  severity: '',
  priority: '',
  attachments: '',
};

export const SAMPLE_BUG_PROMPT_INPUT: BugPromptInput = {
  rawNotes: 'The status filter selection is lost after refreshing the order request list. The list returns to all statuses even though the selected filter was still visible before refresh.',
  title: 'Status filter resets after refreshing the order request list',
  module: 'Order request list filtering',
  environment: 'Testing environment in a Chromium browser',
  buildVersion: 'QA build 2026.08.01',
  preconditions: 'A test user can access the order request list and the list contains requests with at least two different statuses.',
  steps: '1. Open the order request list.\n2. Select the Pending status filter.\n3. Confirm that only Pending requests are displayed.\n4. Refresh the browser page.\n5. Review the active filter and returned list.',
  expectedResult: 'The Pending filter remains selected after refresh and the list continues to display only Pending requests.',
  actualResult: 'The filter control appears to reset and the list displays requests from all statuses after refresh.',
  severity: 'High',
  priority: 'P1',
  attachments: 'order-filter-refresh.png, order-filter-refresh-console.txt'
};

function readText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

export function normalizeBugPromptInput(draft: Partial<BugPromptInput> | null | undefined): BugPromptInput {
  const source = (draft ?? {}) as Record<string, unknown>;

  return {
    rawNotes: readText(source['rawNotes']),
    title: readText(source['title']),
    module: readText(source['module']),
    environment: readText(source['environment']),
    buildVersion: readText(source['buildVersion']),
    preconditions: readText(source['preconditions']),
    steps: readText(source['steps']),
    expectedResult: readText(source['expectedResult']),
    actualResult: readText(source['actualResult']),
    severity: readText(source['severity']),
    priority: readText(source['priority']),
    attachments: readText(source['attachments'])
  };
}
