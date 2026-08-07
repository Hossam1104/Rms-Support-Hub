export interface TestCasePromptInput {
    requirementReference: string;
    title: string;
    module: string;
    scenario: string;
    preconditions: string;
    testData: string;
    steps: string;
    expectedResult: string;
    priority: string;
    attachments: string;
}

export const EMPTY_TEST_CASE_PROMPT_INPUT: TestCasePromptInput = {
    requirementReference: '',
    title: '',
    module: '',
    scenario: '',
    preconditions: '',
    testData: '',
    steps: '',
    expectedResult: '',
    priority: '',
    attachments: ''
};

export const SAMPLE_TEST_CASE_PROMPT_INPUT: TestCasePromptInput = {
    requirementReference: 'POS-PAYMENTS-042',
    title: 'Verify successful credit card payment processing and receipt printing on POS Dashboard',
    module: 'POS_Transactions',
    scenario: 'A cashier completes a credit card payment and the approved transaction appears in the dashboard feed.',
    preconditions: 'Cashier is logged in, register session is active, credit card reader terminal is connected and online.',
    testData: 'Non-production items totaling $10.00 or more and an approved test payment card',
    steps: '1. Scan items totaling $10.00 or more.\n2. Tap the Pay button on the checkout pane.\n3. Choose "Credit Card" as the active payment method.\n4. Tap the test card on the payment reader terminal.\n5. Wait for the approval transaction status update.',
    expectedResult: 'The checkout status displays transaction approval, the approved transaction appears in the POS Dashboard transaction feed with status Success, and the printer outputs the tax receipt.',
    priority: 'P1',
    attachments: 'pos_payment_terminal_approved.png, pos_dashboard_transactions_live.png'
};

function readText(value: unknown): string {
    return typeof value === 'string' ? value : '';
}

export function normalizeTestCasePromptInput(draft: Partial<TestCasePromptInput> | null | undefined): TestCasePromptInput {
    const source = (draft ?? {}) as Record<string, unknown>;

    return {
        requirementReference: readText(source['requirementReference'] ?? source['requirementStoryReference'] ?? source['storyReference']),
        title: readText(source['title']) || readText(source['name']),
        module: readText(source['module']) || readText(source['targetSection']),
        scenario: readText(source['scenario']) || readText(source['scenarioType']) || readText(source['scenarioCategory']),
        preconditions: readText(source['preconditions']),
        testData: readText(source['testData']),
        steps: readText(source['steps']),
        expectedResult: readText(source['expectedResult']),
        priority: readText(source['priority']),
        attachments: readText(source['attachments'])
    };
}
