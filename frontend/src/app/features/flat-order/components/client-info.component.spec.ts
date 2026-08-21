import { ClientInfoComponent } from './client-info.component';

describe('ClientInfoComponent', () => {
  function emissionsFrom(fieldName: string, value: unknown, moduleKey = '') {
    const component = new ClientInfoComponent();
    component.moduleKey = moduleKey;
    const emitted: { fieldName: string; value: unknown }[] = [];
    component.fieldChange.subscribe(change => emitted.push(change));
    component.onFieldChange(fieldName, value);
    return emitted;
  }

  it('strips the country code out of the phone field as it is typed', () => {
    expect(emissionsFrom('client_phone', '+966556028080')).toEqual([
      { fieldName: 'client_phone', value: '556028080' }
    ]);
    expect(emissionsFrom('client_phone', '0556028080')).toEqual([
      { fieldName: 'client_phone', value: '556028080' }
    ]);
  });

  it('normalizes the GHC order phone with the same local-number rule', () => {
    expect(emissionsFrom('order_phone', '+966556028080', 'ghc_ecommerce')).toEqual([
      { fieldName: 'order_phone', value: '556028080' }
    ]);
    expect(emissionsFrom('order_phone', '966556028080', 'ghc_ecommerce')).toEqual([
      { fieldName: 'order_phone', value: '556028080' }
    ]);
    expect(emissionsFrom('order_phone', '0556028080', 'ghc_ecommerce')).toEqual([
      { fieldName: 'order_phone', value: '556028080' }
    ]);
  });

  it('leaves the separate country-code field and every other field untouched', () => {
    expect(emissionsFrom('client_country_code', '966')).toEqual([
      { fieldName: 'client_country_code', value: '966' }
    ]);
    expect(emissionsFrom('client_first_name', '  Jane  ')).toEqual([
      { fieldName: 'client_first_name', value: '  Jane  ' }
    ]);
  });

  it('accepts the GHC-only order contact fields through the normal field contract', () => {
    expect(emissionsFrom('order_country_code', '+966')).toEqual([
      { fieldName: 'order_country_code', value: '+966' }
    ]);
    expect(emissionsFrom('order_phone', '556028080', 'ghc_ecommerce')).toEqual([
      { fieldName: 'order_phone', value: '556028080' }
    ]);
  });

  it('does not apply the GHC-only order phone rule to UPC', () => {
    expect(emissionsFrom('order_phone', '+966556028080', 'upc')).toEqual([
      { fieldName: 'order_phone', value: '+966556028080' }
    ]);
  });
});
