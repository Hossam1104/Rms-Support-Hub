import { mapSendValidationErrors } from './send-validation';

describe('mapSendValidationErrors', () => {
  it('maps server-named required fields to their inline targets', () => {
    const mapped = mapSendValidationErrors([
      'Missing required field: branch_code',
      'Missing required field: client_phone'
    ]);

    expect(mapped.fieldErrors['branch_code']).toEqual(['Missing required field: branch_code']);
    expect(mapped.fieldErrors['client_phone']).toEqual(['Missing required field: client_phone']);
    expect(mapped.globalErrors).toEqual([]);
    expect(mapped.totalCount).toBe(2);
  });

  it('focuses the first invalid field in page order, not message order', () => {
    const mapped = mapSendValidationErrors([
      'Missing required field: client_phone',
      'Missing required field: branch_code'
    ]);

    expect(mapped.firstTargetId).toBe('field-branch-code');
  });

  it('routes the confirmed product and payment section messages', () => {
    const mapped = mapSendValidationErrors([
      'Order must contain at least one product.',
      'Order must contain at least one payment method.',
      "COD payments must have 'not_payment' status.",
      'PostToCredit requires customer_name and customer_number in credit_customer_info.',
      "Digital wallet 'done_payment' must equal order total. Current: 0, Required: 100"
    ]);

    expect(mapped.fieldErrors['products']).toEqual(['Order must contain at least one product.']);
    expect(mapped.fieldErrors['payments']?.length).toBe(4);
    expect(mapped.globalErrors).toEqual([]);
    expect(mapped.firstTargetId).toBe('products-card');
  });

  it('keeps unknown messages global and reports no focus target', () => {
    const mapped = mapSendValidationErrors(['Something the mapper does not know.']);

    expect(mapped.fieldErrors).toEqual({});
    expect(mapped.globalErrors).toEqual(['Something the mapper does not know.']);
    expect(mapped.firstTargetId).toBeNull();
    expect(mapped.totalCount).toBe(1);
  });

  it('handles an empty error list', () => {
    const mapped = mapSendValidationErrors([]);

    expect(mapped.fieldErrors).toEqual({});
    expect(mapped.globalErrors).toEqual([]);
    expect(mapped.totalCount).toBe(0);
    expect(mapped.firstTargetId).toBeNull();
  });
});
