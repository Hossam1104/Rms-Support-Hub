/** Canonical RequestOrderHeaders.OrderStatus values shared by filters and
 * status presentation. Keep the numeric values aligned with the backend's
 * OrderRequestStatus map and docs/database-schema.md. */
export interface OrderRequestStatusOption {
  value: number;
  label: string;
}

export const ORDER_REQUEST_STATUSES: readonly OrderRequestStatusOption[] = [
  { value: 1, label: 'New' },
  { value: 2, label: 'Confirmed' },
  { value: 3, label: 'Ready' },
  { value: 4, label: 'With Delegate' },
  { value: 5, label: 'Rejected' },
  { value: 6, label: 'Canceled (Client)' },
  { value: 7, label: 'Canceled (Admin)' },
  { value: 8, label: 'Processing' },
  { value: 9, label: 'Done' }
] as const;

export const ORDER_REQUEST_STATUS_LABELS: Readonly<Record<number, string>> =
  Object.freeze(Object.fromEntries(ORDER_REQUEST_STATUSES.map(status => [status.value, status.label])));
