import { UiDropdownOption } from '../../shared/ui';

const UPC_MODULE_KEY = 'upc_ecommerce';

/** Mirrors FlatVariant.GhcPaymentMethods — the full legacy method list. */
export const GHC_PAYMENT_METHODS = [
  'COD', 'Visa', 'RajhiPoints', 'Tamara', 'Tabby', 'NeqatyPoints',
  'QitafPoints', 'MisPay', 'Emkan', 'YouGotaGift', 'OgMoney', 'PostToCredit'
];

/**
 * Mirrors FlatVariant.UpcPaymentMethods. UPC settles only through these three.
 * Cash on delivery is not one of them: a UPC cash order carries no payment row
 * at all (order_payment_method COD / order_payment_status not_payment), so
 * offering a "COD" row here would build a payload the API rejects.
 *
 * This list is a UI convenience, not the rule. FlatOrderValidator enforces the
 * same policy server-side and stays the boundary that actually matters.
 */
export const UPC_PAYMENT_METHODS = ['Visa', 'Tamara', 'Tabby'];

export function allowedPaymentMethods(moduleKey: string): string[] {
  return moduleKey === UPC_MODULE_KEY ? UPC_PAYMENT_METHODS : GHC_PAYMENT_METHODS;
}

export function paymentMethodOptions(moduleKey: string): UiDropdownOption[] {
  return allowedPaymentMethods(moduleKey).map(method => ({ value: method, label: method }));
}

/** The method a freshly opened Add Payment dialog starts on. */
export function defaultPaymentMethod(moduleKey: string): string {
  return allowedPaymentMethods(moduleKey)[0];
}

/**
 * Presentation only: the payload keeps the raw not_payment/done_payment/
 * failed_payment values, which are what the flat-order API reads.
 */
export const PAYMENT_STATUS_OPTIONS: UiDropdownOption[] = [
  { value: 'not_payment', label: 'Not paid', tone: 'neutral' },
  { value: 'done_payment', label: 'Paid', tone: 'success' },
  { value: 'failed_payment', label: 'Failed', tone: 'danger' }
];

export function paymentStatusLabel(status: string): string {
  return PAYMENT_STATUS_OPTIONS.find(option => option.value === status)?.label ?? status;
}

/** COD and PostToCredit are settled later; every other method is paid up front. */
export function defaultPaymentStatus(method: string): string {
  return method === 'COD' || method === 'PostToCredit' ? 'not_payment' : 'done_payment';
}
