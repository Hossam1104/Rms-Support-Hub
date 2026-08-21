import { ClientInfoComponent } from './client-info.component';

describe('ClientInfoComponent', () => {
  function emissionsFrom(fieldName: string, value: unknown) {
    const component = new ClientInfoComponent();
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

  it('leaves the separate country-code field and every other field untouched', () => {
    expect(emissionsFrom('client_country_code', '966')).toEqual([
      { fieldName: 'client_country_code', value: '966' }
    ]);
    expect(emissionsFrom('client_first_name', '  Jane  ')).toEqual([
      { fieldName: 'client_first_name', value: '  Jane  ' }
    ]);
  });

  it('accepts the GHC-only order contact fields through the normal field contract', () => {
    const component = new ClientInfoComponent();
    component.moduleKey = 'ghc_ecommerce';
    const emitted: { fieldName: string; value: unknown }[] = [];
    component.fieldChange.subscribe(change => emitted.push(change));

    component.onFieldChange('order_country_code', '966');
    component.onFieldChange('order_phone', '556028080');

    expect(emitted).toEqual([
      { fieldName: 'order_country_code', value: '966' },
      { fieldName: 'order_phone', value: '556028080' }
    ]);
  });
});
