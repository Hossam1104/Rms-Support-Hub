export type AssetPath = `/assets/${string}`;

export interface AppAssetCatalog {
  readonly brand: {
    readonly rms: AssetPath;
    readonly dbs: AssetPath;
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
  };
  readonly commerce: {
    readonly offer: AssetPath;
  };
  readonly system: {
    readonly loader: AssetPath;
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

const rms = '/assets/brand/RMS_Logo.svg' as AssetPath;
const dbs = '/assets/brand/DBS_Logo.svg' as AssetPath;
const upc = '/assets/modules/UPC_Logo.svg' as AssetPath;
const ghc = '/assets/modules/GHC_Logo.svg' as AssetPath;

/**
 * Canonical semantic references for supplied visual assets.
 *
 * The root Riyal path is a deliberate compatibility location: the existing
 * currency component and repository verifier both depend on it. Other assets
 * use their semantic public folders and are not duplicated at the root.
 */
export const APP_ASSETS: AppAssetCatalog = {
  brand: {
    rms,
    dbs
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
    visa: '/assets/payments/Visa.png',
    mastercard: '/assets/payments/MasterCard.png',
    mada: '/assets/payments/MADA.png',
    tabby: '/assets/payments/tabby.png',
    tamara: '/assets/payments/tamara.png'
  },
  commerce: {
    offer: '/assets/commerce/offer_logo.png'
  },
  system: {
    loader: '/assets/system/loader.svg',
    customMessage: {
      error: '/assets/system/custom-message/error.svg',
      information: '/assets/system/custom-message/information.svg',
      question: '/assets/system/custom-message/question.svg',
      success: '/assets/system/custom-message/success.svg',
      warning: '/assets/system/custom-message/warrning.svg'
    }
  }
};
