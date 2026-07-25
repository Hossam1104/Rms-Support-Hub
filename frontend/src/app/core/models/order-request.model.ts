/**
 * Typed against OnlineOrderTool.Core.DTOs.OrderRequestDtos and the
 * OrderRequestsController responses documented in docs/api-spec.md §5.
 */

/** One row in the Order Requests list -- deliberately excludes the
 * RequestJson/ResponseJson blobs (see requestBytes/hasResponse). */
export interface OrderRequestListItem {
  id: number;
  orderNumber: string;
  orderDate: string;
  netTotal: number;
  itemCount: number;
  isSucceeded: boolean | null;
  requestBytes: number;
  hasResponse: boolean;
  orderHeaderId: number | null;
  branchCode: string | null;
  branchName: string | null;
  orderStatus: number | null;
  orderStatusLabel: string | null;
  parentOrderNumber: string | null;
  invoiceBarcode: string | null;
  invoiceDate: string | null;
}

export interface OrderRequestStats {
  total: number;
  succeeded: number;
  failed: number;
  cancelled: number;
}

/** GET /api/modules/{key}/order-requests response body. */
export interface OrderRequestListResponse {
  items: OrderRequestListItem[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  stats: OrderRequestStats;
}

export interface OrderRequestHeader {
  orderHeaderId: number;
  orderNumber: string;
  branchCode: string;
  branchName: string | null;
  orderStatus: number;
  orderStatusLabel: string;
  orderDate: string;
  consumerMobile: string | null;
  address: string | null;
  grossTotal: number;
  netTotal: number;
  totalVat: number;
  totalDiscount: number;
  orderPaymentMethod: string | null;
  orderNote: string | null;
  parentOrderNumber: string | null;
  canResend: boolean;
  canCancel: boolean;
}

export interface OrderRequestDetailLine {
  itemName: string | null;
  materialNumber: string | null;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  totalDiscount: number;
  itemVat: number;
  itemVatPercentage: number;
  offerCode: string | null;
  offerMessage: string | null;
}

export interface OrderRequestTransaction {
  paymentAmount: number;
  eCommercePaymentMethod: string | null;
  eCommercePaymentOption: string | null;
  optionCommission: number;
  paymentStatus: string | null;
  transactionCode: string | null;
  bankCode: string | null;
  cardName: string | null;
}

export interface OrderRequestInvoice {
  barcode: string | null;
  closeDateLocalTime: string | null;
  netAmount: number | null;
  paidAmount: number | null;
}

/** The only shape carrying requestJson/responseJson/exceptionMessage. */
export interface OrderRequestDetail {
  id: number;
  orderNumber: string;
  orderDate: string;
  netTotal: number;
  itemCount: number;
  isSucceeded: boolean | null;
  exceptionMessage: string | null;
  requestJson: string | null;
  responseJson: string | null;
  header: OrderRequestHeader | null;
  details: OrderRequestDetailLine[];
  transactions: OrderRequestTransaction[];
  invoice: OrderRequestInvoice | null;
}

export interface OrderRequestAttempt {
  id: number;
  orderDate: string;
  isSucceeded: boolean | null;
  hasException: boolean;
}

export interface OrderRequestLineageNode {
  orderNumber: string;
  orderDate: string;
  orderStatus: number | null;
  orderStatusLabel: string | null;
  netTotal: number | null;
}

export interface OrderRequestLineage {
  parent: OrderRequestLineageNode | null;
  children: OrderRequestLineageNode[];
}

/** GET .../order-requests/{id} and .../by-order/{orderNumber} response body. */
export interface OrderRequestDetailResponse {
  request: OrderRequestDetail;
  attempts: OrderRequestAttempt[];
  lineage: OrderRequestLineage;
}

export interface BranchSummary {
  branchCode: string;
  branchName: string | null;
  count: number;
}

export interface OrderRequestCancelRequest {
  reason: string;
  endpointKey?: string | null;
  customUrl?: string | null;
}

export interface OrderRequestResendRequest {
  branchCode: string;
  endpointKey?: string | null;
}

/** POST .../order-requests/{id}/cancel response body. */
export interface OrderRequestCancelResponse {
  success: boolean;
  statusCode: number;
  responseText: string;
  urlSent: string;
  request: OrderRequestDetail;
}
