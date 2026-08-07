export type TestCaseScenarioType =
  | 'Happy'
  | 'Negative'
  | 'Edge/Boundary'
  | 'UI'
  | 'Navigation/State'
  | 'Security'
  | 'Data Integrity'
  | 'Performance'
  | 'Accessibility'
  | 'Localization'
  | 'Recovery'
  | 'Happy Path';
export type TestCaseExpectedResultMode = 'Per Step' | 'Final Result';
export type TestCaseOutputType = 'Single Test Case' | 'Scenario Matrix' | 'Jira-friendly' | 'Spreadsheet-friendly';

export const TEST_CASE_SCENARIO_TYPES = [
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
  'Recovery',
  'Happy Path'
] as const;

export const TEST_CASE_PRIORITIES = [
  'P0 (Blocker)',
  'P1 (Critical)',
  'P2 (Major)',
  'P3 (Minor)'
] as const;

export const TEST_CASE_EXPECTED_RESULT_MODES = ['Per Step', 'Final Result'] as const;
export const TEST_CASE_OUTPUT_TYPES = ['Single Test Case', 'Scenario Matrix', 'Jira-friendly', 'Spreadsheet-friendly'] as const;

export const DEFAULT_TEST_CASE_SCENARIO_TYPE: TestCasePromptInput['scenarioType'] = '';
export const DEFAULT_TEST_CASE_EXPECTED_RESULT_MODE: TestCaseExpectedResultMode = 'Final Result';
export const DEFAULT_TEST_CASE_OUTPUT_TYPE: TestCaseOutputType = 'Single Test Case';

export interface TestCasePromptInput {
  testCaseId: string;
  requirementReference: string;
  scenarioType: TestCaseScenarioType | '';
  name: string;
  targetSection: string;
  environment: string;
  priority: string;
  preconditions: string;
  testData: string;
  steps: string;
  expectedResult: string;
  expectedResultMode: TestCaseExpectedResultMode;
  postconditions: string;
  cleanup: string;
  attachments: string;
  automationCandidacy: string;
  regressionTag: string;
  outputType: TestCaseOutputType;
}

export const EMPTY_TEST_CASE_PROMPT_INPUT: TestCasePromptInput = {
  testCaseId: '',
  requirementReference: '',
  scenarioType: DEFAULT_TEST_CASE_SCENARIO_TYPE,
  name: '',
  targetSection: '',
  environment: '',
  priority: '',
  preconditions: '',
  testData: '',
  steps: '',
  expectedResult: '',
  expectedResultMode: DEFAULT_TEST_CASE_EXPECTED_RESULT_MODE,
  postconditions: '',
  cleanup: '',
  attachments: '',
  automationCandidacy: '',
  regressionTag: '',
  outputType: DEFAULT_TEST_CASE_OUTPUT_TYPE
};

export const SAMPLE_TEST_CASE_PROMPT_INPUT: TestCasePromptInput = {
  testCaseId: 'TC-POS-042',
  requirementReference: 'POS-PAYMENTS-042',
  scenarioType: 'Happy Path',
  name: 'Verify successful credit card payment processing and receipt printing on POS Dashboard',
  targetSection: 'POS_Transactions',
  environment: 'Testing environment with a registered POS terminal and connected payment reader',
  priority: 'P1 (Critical)',
  preconditions: 'Cashier is logged in, register session is active, credit card reader terminal is connected and online.',
  testData: 'Non-production items totaling $10.00 or more and an approved test payment card',
  steps: '1. Scan items totaling $10.00 or more.\n2. Tap the Pay button on the checkout pane.\n3. Choose "Credit Card" as the active payment method.\n4. Tap the test card on the payment reader terminal.\n5. Wait for approval transaction status update.',
  expectedResult: 'The checkout status displays transaction approval, transaction entry records dynamically synchronize into the POS Dashboard database feed (marked status Success), and the printer automatically outputs the tax receipt.',
  expectedResultMode: 'Final Result',
  postconditions: 'The approved transaction remains available in the POS Dashboard transaction feed with status Success.',
  cleanup: 'Void or reset the test transaction using the approved Testing workflow and return the register to its prior test state.',
  attachments: 'pos_payment_terminal_approved.png, pos_dashboard_transactions_live.png',
  automationCandidacy: 'Candidate after the payment reader and receipt-printer integration are available to the test harness.',
  regressionTag: 'POS Payments; Receipt Printing; Dashboard Synchronization',
  outputType: 'Single Test Case'
};

function readText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function readScenarioType(value: unknown): TestCasePromptInput['scenarioType'] {
  return typeof value === 'string' && (TEST_CASE_SCENARIO_TYPES as readonly string[]).includes(value)
    ? value as TestCaseScenarioType
    : DEFAULT_TEST_CASE_SCENARIO_TYPE;
}

function readExpectedResultMode(value: unknown): TestCaseExpectedResultMode {
  if (value === 'Per Step' || value === 'Expected Result per Step') return 'Per Step';
  return value === 'Final Result' || value === 'Final expected result'
    ? 'Final Result'
    : DEFAULT_TEST_CASE_EXPECTED_RESULT_MODE;
}

function readOutputType(value: unknown): TestCaseOutputType {
  return typeof value === 'string' && (TEST_CASE_OUTPUT_TYPES as readonly string[]).includes(value)
    ? value as TestCaseOutputType
    : DEFAULT_TEST_CASE_OUTPUT_TYPE;
}

export function normalizeTestCasePromptInput(draft: Partial<TestCasePromptInput> | null | undefined): TestCasePromptInput {
  const source = (draft ?? {}) as Record<string, unknown>;

  return {
    testCaseId: readText(source['testCaseId']),
    requirementReference: readText(source['requirementReference'] ?? source['requirementStoryReference'] ?? source['storyReference']),
    scenarioType: readScenarioType(source['scenarioType'] ?? source['scenarioCategory']),
    name: readText(source['name']),
    targetSection: readText(source['targetSection'] ?? source['module']),
    environment: readText(source['environment']),
    priority: readText(source['priority']),
    preconditions: readText(source['preconditions']),
    testData: readText(source['testData']),
    steps: readText(source['steps']),
    expectedResult: readText(source['expectedResult']),
    expectedResultMode: readExpectedResultMode(source['expectedResultMode']),
    postconditions: readText(source['postconditions']),
    cleanup: readText(source['cleanup']),
    attachments: readText(source['attachments']),
    automationCandidacy: readText(source['automationCandidacy']),
    regressionTag: readText(source['regressionTag'] ?? source['regressionSuiteTag']),
    outputType: readOutputType(source['outputType'])
  };
}
