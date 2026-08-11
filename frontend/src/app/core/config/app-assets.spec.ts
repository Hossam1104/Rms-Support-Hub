import { APP_ASSETS, paymentAssetForMethod, themedAsset } from './app-assets';

describe('APP_ASSETS', () => {
  it('exposes semantic paths for the supplied brand and client assets', () => {
    expect(APP_ASSETS.brand.rms).toEqual({
      light: '/assets/CompanyLogos/Rms_Plus_Light.svg',
      dark: '/assets/CompanyLogos/Rms_Plus_Dark.svg'
    });
    expect(APP_ASSETS.brand.dbs).toEqual({
      light: '/assets/CompanyLogos/DBS_logo_light.svg',
      dark: '/assets/CompanyLogos/DBS_logo_dark.svg'
    });
    expect(APP_ASSETS.brand.favicon).toBe('/assets/CompanyLogos/Rms_Plus.ico');
    expect(APP_ASSETS.modules.upc).toBe('/assets/ClientsLogo/UPC_Logo.svg');
    expect(APP_ASSETS.modules.ghc).toBe('/assets/ClientsLogo/GHC_Logo.svg');
    expect(APP_ASSETS.modules.byKey).toEqual({
      upc_ecommerce: APP_ASSETS.modules.upc,
      ghc_ecommerce: APP_ASSETS.modules.ghc,
      ghc_unicommerce: APP_ASSETS.modules.ghc
    });
  });

  it('resolves each brand lockup to the colourway drawn for the active theme', () => {
    expect(themedAsset(APP_ASSETS.brand.rms, 'dark')).toBe('/assets/CompanyLogos/Rms_Plus_Dark.svg');
    expect(themedAsset(APP_ASSETS.brand.rms, 'light')).toBe('/assets/CompanyLogos/Rms_Plus_Light.svg');
    expect(themedAsset(APP_ASSETS.brand.dbs, 'dark')).toBe('/assets/CompanyLogos/DBS_logo_dark.svg');
    expect(themedAsset(APP_ASSETS.brand.dbs, 'light')).toBe('/assets/CompanyLogos/DBS_logo_light.svg');
  });

  it('keeps payment, commerce, loader, message, and Riyal references centralized', () => {
    expect(APP_ASSETS.currency.riyal).toBe('/assets/Saudi_Riyal.svg');
    expect(APP_ASSETS.payments).toEqual({
      visa: '/assets/Payments/Visa.png',
      mastercard: '/assets/Payments/MasterCard.png',
      mada: '/assets/Payments/MADA.png',
      tabby: '/assets/Payments/tabby.png',
      tamara: '/assets/Payments/tamara.png',
      stcPay: '/assets/Payments/STC_PAY.png',
      emkan: '/assets/Payments/Emkan.png',
      misPay: '/assets/Payments/mispay.png',
      ogMoney: '/assets/Payments/ogmoney.png',
      youGotaGift: '/assets/Payments/yougotagift.png'
    });
    expect(APP_ASSETS.commerce.offer).toBe('/assets/offer_logo.png');
    expect(APP_ASSETS.system.loader).toBe('/assets/loader.svg');
    expect(APP_ASSETS.system.customMessage.warning).toBe('/assets/CustomMessageBox/warrning.svg');
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
