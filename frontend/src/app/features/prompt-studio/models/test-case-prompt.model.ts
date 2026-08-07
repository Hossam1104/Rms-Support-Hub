export interface TestCasePromptInput {
  testCaseId: string;
  scenarioType: string;
  name: string;
  targetSection: string;
  priority: string;
  preconditions: string;
  steps: string;
  expectedResult: string;
  attachments: string;
}

export const EMPTY_TEST_CASE_PROMPT_INPUT: TestCasePromptInput = {
  testCaseId: '',
  scenarioType: '',
  name: '',
  targetSection: '',
  priority: '',
  preconditions: '',
  steps: '',
  expectedResult: '',
  attachments: ''
};

export const SAMPLE_TEST_CASE_PROMPT_INPUT: TestCasePromptInput = {
  testCaseId: 'TC-POS-042',
  scenarioType: 'Happy Path',
  name: 'Verify successful credit card payment processing and receipt printing on POS Dashboard',
  targetSection: 'POS_Transactions',
  priority: 'P1 (Critical)',
  preconditions: 'Cashier is logged in, register session is active, credit card reader terminal is connected and online.',
  steps: '1. Scan items totaling $10.00 or more.\n2. Tap the Pay button on the checkout pane.\n3. Choose "Credit Card" as the active payment method.\n4. Tap the test card on the payment reader terminal.\n5. Wait for approval transaction status update.',
  expectedResult: 'The checkout status displays transaction approval, transaction entry records dynamically synchronize into the POS Dashboard database feed (marked status Success), and the printer automatically outputs the tax receipt.',
  attachments: 'pos_payment_terminal_approved.png, pos_dashboard_transactions_live.png'
};

export const TEST_CASE_SCENARIO_TYPES = [
  'Happy Path',
  'Negative / Error Flow',
  'Edge / Boundary',
  'Security / Authorization',
  'Performance / Latency',
  'Localization / I18n / RTL',
  'Data Integrity / Database',
  'Accessibility / A11y'
] as const;

export const TEST_CASE_PRIORITIES = [
  'P0 (Blocker)',
  'P1 (Critical)',
  'P2 (Major)',
  'P3 (Minor)'
] as const;
