import { Theme } from '../services/theme.service';

export type AssetPath = `/assets/${string}`;

/**
 * A brand mark supplied as two colourways. The variant name matches the theme
 * it is drawn for: `dark` is the light-ink artwork used on dark surfaces and
 * `light` is the dark-ink artwork used on light surfaces.
 */
export interface ThemedAssetPair {
  readonly light: AssetPath;
  readonly dark: AssetPath;
}

export interface AppAssetCatalog {
  readonly brand: {
    readonly rms: ThemedAssetPair;
    readonly dbs: ThemedAssetPair;
    readonly favicon: AssetPath;
  };
  readonly modules: {
    readonly upc: AssetPath;
    readonly ghc: AssetPath;
    readonly byKey: Readonly<Record<string, AssetPath>>;
    readonly altByKey: Readonly<Record<string, string>>;
  };
  readonly currency: {
    readonly riyal: AssetPath;
  };
  readonly payments: {
    readonly visa: AssetPath;
    readonly mastercard: AssetPath;
    readonly mada: AssetPath;
    readonly tabby: AssetPath;
    readonly tamara: AssetPath;
    readonly stcPay: AssetPath;
    readonly emkan: AssetPath;
    readonly misPay: AssetPath;
    readonly ogMoney: AssetPath;
    readonly youGotaGift: AssetPath;
  };
  readonly commerce: {
    readonly offer: AssetPath;
  };
  readonly system: {
    readonly loader: AssetPath;
    readonly statusSuccess: AssetPath;
    readonly statusError: AssetPath;
    readonly customMessage: {
      readonly error: AssetPath;
      readonly information: AssetPath;
      readonly question: AssetPath;
      readonly success: AssetPath;
      /** The supplied file is intentionally kept as `warrning.svg`. */
      readonly warning: AssetPath;
    };
  };
}

const rms: ThemedAssetPair = {
  light: '/assets/CompanyLogos/Rms_Plus_Light.svg',
  dark: '/assets/CompanyLogos/Rms_Plus_Dark.svg'
};
const dbs: ThemedAssetPair = {
  light: '/assets/CompanyLogos/DBS_logo_light.svg',
  dark: '/assets/CompanyLogos/DBS_logo_dark.svg'
};
const upc = '/assets/ClientsLogo/UPC_Logo.svg' as AssetPath;
const ghc = '/assets/ClientsLogo/GHC_Logo.svg' as AssetPath;

/**
 * Canonical semantic references for supplied visual assets.
 *
 * Folder names mirror the supplied `assets/` drop exactly (`CompanyLogos`,
 * `ClientsLogo`, `Payments`, `CustomMessageBox`) so a re-supplied asset can be
 * copied across without a rename step. The root Riyal path is a deliberate
 * compatibility location: the existing currency component and repository
 * verifier both depend on it.
 */
export const APP_ASSETS: AppAssetCatalog = {
  brand: {
    rms,
    dbs,
    favicon: '/assets/CompanyLogos/Rms_Plus.ico'
  },
  modules: {
    upc,
    ghc,
    byKey: {
      upc_ecommerce: upc,
      ghc_ecommerce: ghc,
      ghc_unicommerce: ghc
    },
    altByKey: {
      upc_ecommerce: 'UPC',
      ghc_ecommerce: 'GHC / Whites',
      ghc_unicommerce: 'GHC / Whites'
    }
  },
  currency: {
    riyal: '/assets/Saudi_Riyal.svg'
  },
  payments: {
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
  },
  commerce: {
    offer: '/assets/offer_logo.png'
  },
  system: {
    loader: '/assets/loader.svg',
    statusSuccess: '/assets/CompanyLogos/StatusSuccess.png',
    statusError: '/assets/CompanyLogos/StatusError.png',
    customMessage: {
      error: '/assets/CustomMessageBox/error.svg',
      information: '/assets/CustomMessageBox/information.svg',
      question: '/assets/CustomMessageBox/question.svg',
      success: '/assets/CustomMessageBox/success.svg',
      warning: '/assets/CustomMessageBox/warrning.svg'
    }
  }
};

/** Pick the supplied colourway that matches the active theme. */
export function themedAsset(pair: ThemedAssetPair, theme: Theme): AssetPath {
  return theme === 'dark' ? pair.dark : pair.light;
}

/**
 * Resolve only payment methods with an unambiguous supplied brand asset.
 *
 * The comparison is deliberately exact after trimming/case-normalization:
 * values such as `Card`, `ApplePay`, or `STCPay` must keep the neutral icon
 * fallback rather than being guessed from incidental payment metadata.
 */
export function paymentAssetForMethod(method: string | null | undefined): AssetPath | null {
  switch ((method ?? '').trim().toLowerCase()) {
    case 'visa':
      return APP_ASSETS.payments.visa;
    case 'mastercard':
      return APP_ASSETS.payments.mastercard;
    case 'mada':
      return APP_ASSETS.payments.mada;
    case 'tabby':
      return APP_ASSETS.payments.tabby;
    case 'tamara':
      return APP_ASSETS.payments.tamara;
    case 'stcpay':
    case 'stc_pay':
      return APP_ASSETS.payments.stcPay;
    case 'emkan':
      return APP_ASSETS.payments.emkan;
    case 'mispay':
      return APP_ASSETS.payments.misPay;
    case 'ogmoney':
      return APP_ASSETS.payments.ogMoney;
    case 'yougotagift':
      return APP_ASSETS.payments.youGotaGift;
    default:
      return null;
  }
}
