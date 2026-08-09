import { APP_ASSETS, paymentAssetForMethod } from './app-assets';

describe('APP_ASSETS', () => {
  it('exposes semantic paths for the supplied brand and module assets', () => {
    expect(APP_ASSETS.brand.rms).toBe('/assets/brand/RMS_Logo.svg');
    expect(APP_ASSETS.brand.dbs).toBe('/assets/brand/DBS_Logo.svg');
    expect(APP_ASSETS.modules.upc).toBe('/assets/modules/UPC_Logo.svg');
    expect(APP_ASSETS.modules.ghc).toBe('/assets/modules/GHC_Logo.svg');
    expect(APP_ASSETS.modules.byKey).toEqual({
      upc_ecommerce: APP_ASSETS.modules.upc,
      ghc_ecommerce: APP_ASSETS.modules.ghc,
      ghc_unicommerce: APP_ASSETS.modules.ghc
    });
  });

  it('keeps payment, commerce, loader, message, and Riyal references centralized', () => {
    expect(APP_ASSETS.currency.riyal).toBe('/assets/Saudi_Riyal.svg');
    expect(APP_ASSETS.payments).toEqual({
      visa: '/assets/payments/Visa.png',
      mastercard: '/assets/payments/MasterCard.png',
      mada: '/assets/payments/MADA.png',
      tabby: '/assets/payments/tabby.png',
      tamara: '/assets/payments/tamara.png',
      stcPay: '/assets/payments/STC_PAY.png',
      emkan: '/assets/payments/emkan.png',
      misPay: '/assets/payments/mispay.png',
      ogMoney: '/assets/payments/ogmoney.png',
      youGotaGift: '/assets/payments/yougotagift.png'
    });
    expect(APP_ASSETS.commerce.offer).toBe('/assets/commerce/offer_logo.png');
    expect(APP_ASSETS.system.loader).toBe('/assets/system/loader.svg');
    expect(APP_ASSETS.system.customMessage.warning).toBe('/assets/system/custom-message/warrning.svg');
  });

  it('maps only exact known payment methods and keeps ambiguous methods neutral', () => {
    expect(paymentAssetForMethod('Visa')).toBe(APP_ASSETS.payments.visa);
    expect(paymentAssetForMethod('MasterCard')).toBe(APP_ASSETS.payments.mastercard);
    expect(paymentAssetForMethod('MADA')).toBe(APP_ASSETS.payments.mada);
    expect(paymentAssetForMethod(' tabby ')).toBe(APP_ASSETS.payments.tabby);
    expect(paymentAssetForMethod('Tamara')).toBe(APP_ASSETS.payments.tamara);
    expect(paymentAssetForMethod('STCPay')).toBe(APP_ASSETS.payments.stcPay);
    expect(paymentAssetForMethod('MisPay')).toBe(APP_ASSETS.payments.misPay);
    expect(paymentAssetForMethod('Emkan')).toBe(APP_ASSETS.payments.emkan);
    expect(paymentAssetForMethod('OgMoney')).toBe(APP_ASSETS.payments.ogMoney);
    expect(paymentAssetForMethod('YouGotaGift')).toBe(APP_ASSETS.payments.youGotaGift);
    expect(paymentAssetForMethod('Card')).toBeNull();
    expect(paymentAssetForMethod('ApplePay')).toBeNull();
    expect(paymentAssetForMethod(undefined)).toBeNull();
  });
});
