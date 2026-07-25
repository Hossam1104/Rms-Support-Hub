/** Mirrors OnlineOrderTool.Core.Models.Product. */
export interface Product {
  itemCode: string;
  itemName: string;
  itemNameAr?: string | null;
  itemBarcode?: string | null;
  quantity: number;
  unitPrice: number;
  vatPercentage: number;
  /** Flat row-level discount (row_total_discount) -- applied once, not per unit. */
  discount: number;
  offerCode?: string | null;
  offerMessage?: string | null;
  // Server-computed read-only fields, present when a Product comes back
  // from the API (e.g. a lookup result) but not required when building one.
  rowSubtotal?: number;
  totalVat?: number;
  unitVat?: number;
  estimatedTotal?: number;
  netUnitPrice?: number;
}

/** Mirrors OnlineOrderTool.Core.Models.Payment. */
export interface Payment {
  paymentMethod: string;
  paymentStatus: string;
  paymentAmount: number;
  transactionId?: string | null;
  paymentOption?: string | null;
  optionCommission?: number;
  /** Only meaningful for paymentMethod === "PostToCredit" (GHC only). */
  customerName?: string | null;
  customerNumber?: string | null;
  /** GHC-only card payment metadata. */
  cardName?: string | null;
  bankCode?: string | null;
}

/** Mirrors OnlineOrderTool.Core.Models.Consumer. */
export interface Consumer {
  firstName?: string | null;
  middleName?: string | null;
  lastName?: string | null;
  consumerCode?: string | null;
  gender?: string | null;
  birthDate?: string | null;
  primaryPhoneNumber?: string | null;
  email?: string | null;
  nationalId?: string | null;
  nationality?: string | null;
  /** UPC-only: resolved from LoyaltyConsumerAddresses. */
  address?: string | null;
  addressCode?: string | null;
}

/** Mirrors OnlineOrderTool.Core.Models.DeliveryDetails (Uni-Commerce only). */
export interface DeliveryDetails {
  deliveryPhoneNumber?: string | null;
  deliveryAddress?: string | null;
  deliveryLocationUrl?: string | null;
  deliveryNotes?: string | null;
  deliveryFees: number;
}

/** Mirrors OnlineOrderTool.Core.Models.RowItem (Uni-Commerce invoice line). */
export interface RowItem {
  quantity: number;
  materialNumber: string;
  itemPrice: number;
  itemDiscount: number;
  vatPercentage: number;
  batchNumber?: string | null;
  expireDate?: string | null;
  serialNumber?: string | null;
  barcode: string;
  scannedCode?: string | null;
  offerIdentifier?: string | null;
}

/**
 * Mirrors OnlineOrderTool.Core.Models.OrderDraft. orderData is a genuine
 * loosely-typed bag (Dictionary<string, object?> server-side, holding a
 * different field set per module/variant) -- `unknown` is the honest type,
 * not `any`: every read site must narrow/cast before use.
 */
export interface OrderDraft {
  orderData: Record<string, unknown>;
  products: Product[];
  payments: Payment[];
  consumer: Consumer;
  delivery: DeliveryDetails;
  rowItems: RowItem[];
}

/** Mirrors OnlineOrderTool.Core.Services.TotalsSummary. */
export interface TotalsSummary {
  totalProductAmount: number;
  totalProductVat: number;
  orderDiscount: number;
  deliveryCost: number;
  totalOrderAmount: number;
  totalPaidAmount: number;
  remainingBalance: number;
}
