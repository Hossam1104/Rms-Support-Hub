export interface BugPromptInput {
  title: string;
  preconditions: string;
  steps: string;
  expectedResult: string;
  actualResult: string;
  attachments: string;
}

export const EMPTY_BUG_PROMPT_INPUT: BugPromptInput = {
  title: '',
  preconditions: '',
  steps: '',
  expectedResult: '',
  actualResult: '',
  attachments: ''
};

export const SAMPLE_BUG_PROMPT_INPUT: BugPromptInput = {
  title: 'Discount calculation mismatch when applying multiple promotions sequentially',
  preconditions: 'Cashier is logged in, register active, cart holds at least 3 distinct menu items.',
  steps: '1. Add "Espresso" (Qty 2) and "Croissant" (Qty 1) to the cart.\n2. Select Coupon Code field and apply code "WELCOME10" (10% storewide).\n3. From the Quick Promos list, click and apply "Coffee & Pastry Bundle" (50% off pastry).\n4. Review the transaction summary details before payment.',
  expectedResult: 'The cart subtotal, discount breakdowns, tax, and total are computed correctly (applying coupon to original base and bundle discount to coffee/pastry pairing without double-deductions on the croissant).',
  actualResult: 'The Croissant item registers double deductions (both 10% storewide coupon and 50% bundle deduction calculated on base price sequentially), causing its final line price to register as negative ($ -1.25).',
  attachments: 'pos_checkout_calculation_error.png, checkout_calculation_debug.log'
};
