"""Shared schema logic for the flat-order modules (GHC E-Commerce, UPC E-Commerce).

Both modules use the identical JSON schema — compare
request_examples/GHC E-Commerce/request_body.json against request_examples/UPC/*.json,
they're the same shape. Only the environment URLs and DB connection differ per module,
so the serializer/validator/DB-manager logic lives here once and both
modules/ghc_ecommerce.py and modules/upc_ecommerce.py reuse it via composition.

Ported from the pre-refactor managers.py (OrderManager / ProductCalculator /
DatabaseManager) and config.py (get_default_data), adapted to take an explicit
`state` dict instead of reading Flask's global session directly, so modules stay
decoupled from any particular session/persistence model.
"""

import logging
import re
from datetime import datetime
from typing import Any, Dict, List, Optional

import pyodbc

from config import get_default_data

from .base import ValidationError

logger = logging.getLogger(__name__)


# --------------------------------------------------------------------------
# UPC order-validation status codes (RequestOrderHeaders.OrderStatus)
# --------------------------------------------------------------------------

UPC_ORDER_STATUS_LABELS: Dict[int, str] = {
    1: "New",
    2: "Confirmed",
    3: "Ready",
    4: "With_Delegate",
    5: "Rejected",
    6: "CanceledByClient",
    7: "CanceledByAdmin",
    8: "Processing",
    9: "Done",
}

# An order may be resent to a different branch unless it's already been
# executed/invoiced (With_Delegate, Done) or is actively in the POS cart
# (Processing).
UPC_RESEND_BLOCKED_STATUSES = {4, 8, 9}


def upc_resend_allowed(status: Optional[int]) -> bool:
    return status is not None and status not in UPC_RESEND_BLOCKED_STATUSES


def normalize_upc_material_number(raw: str) -> str:
    """UPC's MaterialNumber is always 18 digits (typically 12 leading zeros +
    a 6-digit item number, e.g. "000000000000212401"). The UI accepts either
    the full 18-digit code or just the 6-digit item number, which is padded
    with 12 leading zeros here.
    """
    value = (raw or "").strip()
    if not value.isdigit():
        raise ValidationError("Item number must be numeric")
    if len(value) == 18:
        return value
    if len(value) == 6:
        return "0" * 12 + value
    raise ValidationError(
        "Item number must be either 6 digits or the full 18-digit material number"
    )


def normalize_phone_search(phone: str) -> str:
    """Canonicalize a phone number for search, regardless of how the user
    types it: with/without a leading '+', with/without the '966' country
    code, with/without a leading '0' trunk prefix. All of
    "+966556028080" / "966556028080" / "0556028080" / "556028080" collapse
    to the same 9-digit local number, which is compared against the stored
    column via SQL "RIGHT(column, 9) = ?" so it matches regardless of which
    of those forms the number happens to be stored in.
    """
    digits = re.sub(r"\D", "", phone or "")
    if len(digits) < 9:
        raise ValidationError("Phone number is too short")
    return digits[-9:]


# --------------------------------------------------------------------------
# Database access
# --------------------------------------------------------------------------


class FlatOrderDatabaseManager:
    """DB manager for a flat-order module, parameterized by that module's own DB config."""

    def __init__(self, db_config: Dict[str, Any]):
        self.db_config = db_config
        self._cached_driver: Optional[str] = None

    def get_db_connection(self) -> Optional[pyodbc.Connection]:
        """Establish a DB connection with fallback drivers, caching the one that works."""
        drivers = []

        if self._cached_driver:
            drivers.append(self._cached_driver)

        if self.db_config["driver"] and self.db_config["driver"] != self._cached_driver:
            drivers.append(self.db_config["driver"])

        fallback_drivers = [
            "ODBC Driver 18 for SQL Server",
            "ODBC Driver 17 for SQL Server",
            "ODBC Driver 13 for SQL Server",
            "SQL Server Native Client 11.0",
            "SQL Server",
        ]
        for d in fallback_drivers:
            if d not in drivers:
                drivers.append(d)

        for driver in drivers:
            try:
                conn_str = (
                    f"DRIVER={{{driver}}};"
                    f"SERVER={self.db_config['server']};"
                    f"DATABASE={self.db_config['database']};"
                    f"UID={self.db_config['username']};"
                    f"PWD={self.db_config['password']};"
                    f"Trusted_Connection=no;"
                    f"Encrypt=no;"
                )
                conn = pyodbc.connect(conn_str)
                logger.info(f"Connected successfully using {driver}")

                if self._cached_driver != driver:
                    self._cached_driver = driver
                    logger.info(f"Cached database driver: {driver}")

                return conn
            except pyodbc.Error as e:
                logger.warning(f"Failed with {driver}: {str(e)}")
                continue

        logger.error("Could not connect with any available driver")
        return None

    def lookup_item(self, material_number: str, **filters) -> Dict[str, Any]:
        """Look up item details by 6-digit material number."""
        if (
            not material_number
            or not material_number.isdigit()
            or len(material_number) != 6
        ):
            raise ValidationError("Material number must be 6 digits")

        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            query = """
                    SELECT TOP 1
                I.MaterialNumber, IUOMB.UniversalBarCode,
                           I.Name                                                                    AS EnglishName,
                           I.NativeName                                                              AS ArabicName,
                           IP.Price                                                                  AS UnitPrice,
                           TT.Rate                                                                   AS VatRate,
                           CAST(ROUND(((IP.Price * TT.Rate) / 100) + IP.Price, 2) AS DECIMAL(10, 2)) AS NetPrice
                    FROM dbo.Items AS I
                             LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
                             INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
                             INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON IUM.Id = IUOMB.ItemUnitOfMeasureId
                             LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
                    WHERE RIGHT (I.MaterialNumber
                        , 6) = ?
                      AND IP.IsActive = 1
                      AND IP.Price IS NOT NULL
                      AND IP.ToDate
                        > GETDATE() \
                    """

            params = [material_number]

            if filters.get("customer_number"):
                query += " AND EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerNumber = ? AND IsActive = 1)"
                params.append(filters["customer_number"])

            if filters.get("sap_tax_code"):
                query += " AND I.SapTaxCode = ?"
                params.append(filters["sap_tax_code"])

            if filters.get("sap_mat_generic"):
                query += " AND I.SapMatGeneric = ?"
                params.append(filters["sap_mat_generic"])

            query += " ORDER BY I.Id DESC"

            cursor.execute(query, params)
            row = cursor.fetchone()

            if not row:
                raise ValueError("No item found with the specified criteria")

            return {
                "item_code": row[0] if row[0] else f"000000000000{material_number}",
                "item_Barcode": row[1] if row[1] else f"BC{material_number}",
                "item_EN_Name": row[2] if row[2] else f"Item {material_number}",
                "item_AR_Name": row[3] if row[3] else f"صنف {material_number}",
                "unit_price": float(row[4]) if row[4] else 0.0,
                "vat_percentage": float(row[5]) if row[5] else 0.0,
                "net_price": float(row[6]) if row[6] else 0.0,
            }
        finally:
            conn.close()

    def lookup_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        """Look up an existing consumer/customer by phone number, if one exists.

        # TODO(db-creds): confirm the real table/column names once real DB
        # credentials/schema are supplied. This guesses a dbo.Customers shape
        # consistent with the customer_number EXISTS-filter already used in
        # lookup_item above (managers.py's original customer_number filter).
        """
        phone = (phone or "").strip()
        if not phone:
            raise ValidationError("Phone number is required")
        canonical_phone = normalize_phone_search(phone)

        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            query = """
                    SELECT TOP 1
                        C.CustomerNumber, C.FirstName, C.MiddleName, C.LastName,
                        C.Email, C.PhoneNumber, C.Gender, C.BirthDate
                    FROM dbo.Customers AS C
                    WHERE RIGHT(C.PhoneNumber, 9) = ?
                      AND C.IsActive = 1
                    ORDER BY C.Id DESC
                    """  # TODO(db-creds): confirm real table/column names
            cursor.execute(query, [canonical_phone])
            row = cursor.fetchone()

            if not row:
                return None

            return {
                "customer_number": row[0] or "",
                "first_name": row[1] or "",
                "middle_name": row[2] or "",
                "last_name": row[3] or "",
                "email": row[4] or "",
                "phone": row[5] or phone,
                "gender": row[6] or "",
                "birthdate": row[7].isoformat() if row[7] else "",
            }
        finally:
            conn.close()

    def lookup_upc_item(
        self, material_number: str, branch_code: str
    ) -> Dict[str, Any]:
        """Look up branch-specific item pricing for UPC.

        Confirmed live against Items / BranchItemUnitOfMeasures /
        ItemUnitOfMeasurePrices / Branches on RmsMainTest2 (see the "Item
        Lookup" addendum in UPC_Enhancments_Plan.md). Unlike GHC's still-
        guessed lookup_item, price is branch-specific (BranchItemUnitOfMeasures
        joins Items to Branches), so branch_code is required, not optional.
        When an item has more than one unit-of-measure/price row for the same
        branch, the base unit of measure is preferred, then the highest price
        (mirrors the confirmed reference query's own ROW_NUMBER ordering).
        """
        padded = normalize_upc_material_number(material_number)

        branch_code = (branch_code or "").strip()
        if not branch_code:
            raise ValidationError("Branch code is required to look up UPC item pricing")

        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            query = """
                    WITH ItemPricesRanked AS (
                        SELECT
                            B.BranchCode AS Branch_Code,
                            I.MaterialNumber AS Full_Material_Number,
                            I.Name AS Item_Name_EN,
                            I.NativeName AS Item_Name_AR,
                            IUOMP.Price AS Item_Price,
                            BIUOM.IsBase AS IsBase,
                            TT.Rate AS Tax_Rate,
                            ROW_NUMBER() OVER (
                                PARTITION BY I.Id, B.Id
                                ORDER BY BIUOM.IsBase DESC, IUOMP.Price DESC
                            ) AS rn
                        FROM dbo.Items AS I
                        JOIN dbo.BranchItemUnitOfMeasures AS BIUOM ON BIUOM.ItemId = I.Id
                        JOIN dbo.ItemUnitOfMeasurePrices AS IUOMP
                            ON IUOMP.BranchItemUnitOfMeasureId = BIUOM.Id
                        JOIN dbo.Branches AS B ON B.Id = BIUOM.BranchId
                        LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
                        WHERE I.MaterialNumber = ? AND B.BranchCode = ?
                    )
                    SELECT TOP 1
                        Full_Material_Number, Item_Name_EN, Item_Name_AR,
                        Item_Price, Tax_Rate
                    FROM ItemPricesRanked
                    WHERE rn = 1
                    """
            cursor.execute(query, [padded, branch_code])
            row = cursor.fetchone()

            if not row:
                raise ValueError(
                    f"No item found for material number '{padded}' at branch '{branch_code}'"
                )

            price = float(row[3]) if row[3] is not None else 0.0
            vat_rate = float(row[4]) if row[4] is not None else 0.0

            return {
                "item_code": row[0] or padded,
                "item_Barcode": "",
                "item_EN_Name": row[1] or "",
                "item_AR_Name": row[2] or "",
                "unit_price": price,
                "vat_percentage": vat_rate,
                "net_price": round(price + (price * vat_rate / 100), 2),
            }
        finally:
            conn.close()

    def lookup_upc_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        """Look up an existing UPC consumer by phone, with their loyalty address.

        Confirmed live against Consumers / LoyaltyConsumerAddresses on
        RmsMainTest2 (see "Schema discovery" in UPC_Enhancments_Plan.md).
        LoyaltyConsumerAddresses has no single "the" address per consumer, so
        the OUTER APPLY prefers the row flagged IsMaster, falling back to the
        most recently added address if no master is set.
        """
        phone = (phone or "").strip()
        if not phone:
            raise ValidationError("Phone number is required")
        canonical_phone = normalize_phone_search(phone)

        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            query = """
                    SELECT TOP 1
                        C.Id, C.FirstName, C.MiddleName, C.LastName,
                        C.Email, C.PhoneNumber, C.Gender, C.BirthDate, C.ConsumerCode,
                        A.FullAddress, A.AddressCode, A.StreetName, A.Building,
                        A.Floor, A.Landmark, A.Area
                    FROM Consumers AS C
                    OUTER APPLY (
                        SELECT TOP 1
                            FullAddress, AddressCode, StreetName, Building,
                            Floor, Landmark, Area
                        FROM LoyaltyConsumerAddresses
                        WHERE ConsumerId = C.Id
                        ORDER BY IsMaster DESC, Id DESC
                    ) AS A
                    WHERE RIGHT(C.PhoneNumber, 9) = ?
                    ORDER BY C.Id DESC
                    """
            cursor.execute(query, [canonical_phone])
            row = cursor.fetchone()

            if not row:
                return None

            full_address = row[9] or ""
            if not full_address:
                # Compose a best-effort address from the parts when
                # FullAddress itself is blank.
                parts = [row[11], row[12], row[13], row[14], row[15]]
                full_address = ", ".join(p for p in parts if p)

            return {
                "customer_number": row[8] or "",
                "first_name": row[1] or "",
                "middle_name": row[2] or "",
                "last_name": row[3] or "",
                "email": row[4] or "",
                "phone": row[5] or phone,
                "gender": row[6] or "",
                "birthdate": row[7].isoformat() if row[7] else "",
                "address": full_address,
                "address_code": row[10] or "",
            }
        finally:
            conn.close()

    def search_upc_orders(self, filters: Dict[str, Any]) -> List[Dict[str, Any]]:
        """Search RequestOrderHeaders (the order-of-record) for the Order
        Validation grid, joined to OrderRequests (the raw API call log) and
        Invoices (for the barcode / invoice-date comparison).

        `filters` keys (all optional): order_number, phone, branch_code,
        status (int), date_from, date_to (date-only strings, inclusive).
        """
        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        where_clauses = []
        params: List[Any] = []

        if filters.get("order_number"):
            where_clauses.append("H.OrderNumber = ?")
            params.append(filters["order_number"])
        if filters.get("phone"):
            canonical_phone = normalize_phone_search(filters["phone"])
            where_clauses.append(
                "(RIGHT(H.ConsumerMobile, 9) = ? OR RIGHT(H.OrderMobileNumber, 9) = ?)"
            )
            params.append(canonical_phone)
            params.append(canonical_phone)
        if filters.get("branch_code"):
            where_clauses.append("H.BranchCode = ?")
            params.append(filters["branch_code"])
        if filters.get("status") is not None:
            where_clauses.append("H.OrderStatus = ?")
            params.append(filters["status"])
        if filters.get("date_from"):
            where_clauses.append("H.OrderDate >= ?")
            params.append(filters["date_from"])
        if filters.get("date_to"):
            where_clauses.append("H.OrderDate < DATEADD(day, 1, ?)")
            params.append(filters["date_to"])

        where_sql = f"WHERE {' AND '.join(where_clauses)}" if where_clauses else ""

        # OrderRequests and Invoices are not 1:1 with OrderNumber (retries and
        # re-invoicing both create extra rows), so both are pulled via
        # OUTER APPLY TOP 1 rather than a plain LEFT JOIN, which would
        # otherwise multiply a single header into duplicate result rows.
        query = f"""
                SELECT TOP 200
                    H.Id, H.OrderNumber, H.BranchCode, H.BranchName, H.OrderStatus,
                    H.OrderDate, H.ConsumerMobile, H.NetTotal, H.ParentOrderNumber,
                    OR2.IsSucceeded,
                    I.Barcode, I.CloseDateLocalTime
                FROM RequestOrderHeaders AS H
                OUTER APPLY (
                    SELECT TOP 1 IsSucceeded
                    FROM OrderRequests
                    WHERE OrderNumber = H.OrderNumber
                    ORDER BY Id DESC
                ) AS OR2
                OUTER APPLY (
                    SELECT TOP 1 Barcode, CloseDateLocalTime
                    FROM Invoices
                    WHERE OnlineOrderNumber = H.OrderNumber
                    ORDER BY Id DESC
                ) AS I
                {where_sql}
                ORDER BY H.OrderDate DESC
                """

        try:
            cursor = conn.cursor()
            cursor.execute(query, params)
            rows = cursor.fetchall()

            results = []
            for row in rows:
                status = row[4]
                results.append(
                    {
                        "order_header_id": row[0],
                        "order_number": row[1],
                        "branch_code": row[2] or "",
                        "branch_name": row[3] or "",
                        "status": status,
                        "status_label": UPC_ORDER_STATUS_LABELS.get(
                            status, "Unknown"
                        ),
                        "order_date": row[5].isoformat() if row[5] else "",
                        "consumer_mobile": row[6] or "",
                        "net_total": float(row[7]) if row[7] is not None else 0.0,
                        "parent_order_number": row[8] or "",
                        "request_succeeded": bool(row[9]) if row[9] is not None else None,
                        "invoice_barcode": row[10] or "",
                        "invoice_date": row[11].isoformat() if row[11] else "",
                        "resend_allowed": upc_resend_allowed(status),
                    }
                )
            return results
        finally:
            conn.close()

    def get_upc_order_details(
        self, order_number: str, order_header_id: Optional[int] = None
    ) -> Optional[Dict[str, Any]]:
        """Full detail view for one order: header, line items, transactions,
        and invoice info (joined the same way as search_upc_orders).

        The same order_number can have more than one RequestOrderHeaders row
        (the app can resend an order to a different branch), so pass
        order_header_id — as returned by search_upc_orders — to target an
        exact row. Without it, this returns the most recently created header
        for that order_number.
        """
        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            if order_header_id is not None:
                header_filter, header_param = "H.Id = ?", order_header_id
            else:
                header_filter, header_param = "H.OrderNumber = ?", order_number
            cursor.execute(
                f"""
                SELECT TOP 1
                    H.Id, H.OrderNumber, H.BranchCode, H.BranchName, H.OrderStatus,
                    H.OrderDate, H.ConsumerMobile, H.Address, H.GrossTotal, H.NetTotal,
                    H.TotalVat, H.TotalDiscount, H.OrderPaymentMethod, H.OrderNote,
                    H.RejectionMessage, H.ParentOrderNumber,
                    I.Barcode, I.CloseDateLocalTime
                FROM RequestOrderHeaders AS H
                OUTER APPLY (
                    SELECT TOP 1 Barcode, CloseDateLocalTime
                    FROM Invoices
                    WHERE OnlineOrderNumber = H.OrderNumber
                    ORDER BY Id DESC
                ) AS I
                WHERE {header_filter}
                ORDER BY H.Id DESC
                """,
                [header_param],
            )
            header_row = cursor.fetchone()
            if not header_row:
                return None

            header_id = header_row[0]
            status = header_row[4]
            header = {
                "order_header_id": header_id,
                "order_number": header_row[1],
                "branch_code": header_row[2] or "",
                "branch_name": header_row[3] or "",
                "status": status,
                "status_label": UPC_ORDER_STATUS_LABELS.get(status, "Unknown"),
                "order_date": header_row[5].isoformat() if header_row[5] else "",
                "consumer_mobile": header_row[6] or "",
                "address": header_row[7] or "",
                "gross_total": float(header_row[8]) if header_row[8] is not None else 0.0,
                "net_total": float(header_row[9]) if header_row[9] is not None else 0.0,
                "total_vat": float(header_row[10]) if header_row[10] is not None else 0.0,
                "total_discount": float(header_row[11])
                if header_row[11] is not None
                else 0.0,
                "payment_method": header_row[12] or "",
                "order_note": header_row[13] or "",
                "rejection_message": header_row[14] or "",
                "parent_order_number": header_row[15] or "",
                "invoice_barcode": header_row[16] or "",
                "invoice_date": header_row[17].isoformat() if header_row[17] else "",
                "resend_allowed": upc_resend_allowed(status),
            }

            cursor.execute(
                """
                SELECT ItemName, MaterialNumber, Quantity, UnitPrice, TotalPrice,
                       TotalDiscount, ItemVat, ItemVatPercentage, OfferCode, OfferMessage
                FROM RequestOrderDetails
                WHERE RequestOrderHeaderId = ?
                """,
                [header_id],
            )
            details = [
                {
                    "item_name": r[0] or "",
                    "material_number": r[1] or "",
                    "quantity": float(r[2]) if r[2] is not None else 0.0,
                    "unit_price": float(r[3]) if r[3] is not None else 0.0,
                    "total_price": float(r[4]) if r[4] is not None else 0.0,
                    "total_discount": float(r[5]) if r[5] is not None else 0.0,
                    "item_vat": float(r[6]) if r[6] is not None else 0.0,
                    "item_vat_percentage": float(r[7]) if r[7] is not None else 0.0,
                    "offer_code": r[8] or "",
                    "offer_message": r[9] or "",
                }
                for r in cursor.fetchall()
            ]

            cursor.execute(
                """
                SELECT PaymentAmount, ECommercePaymentMethod, ECommercePaymentOption,
                       OptionCommission, PaymentStatus, TransactionCode
                FROM RequestOrderTransactions
                WHERE RequestOrderHeaderId = ?
                """,
                [header_id],
            )
            transactions = [
                {
                    "payment_amount": float(r[0]) if r[0] is not None else 0.0,
                    "payment_method": r[1] or "",
                    "payment_option": r[2] or "",
                    "option_commission": float(r[3]) if r[3] is not None else 0.0,
                    "payment_status": r[4] or "",
                    "transaction_code": r[5] or "",
                }
                for r in cursor.fetchall()
            ]

            header["details"] = details
            header["transactions"] = transactions
            return header
        finally:
            conn.close()

    def get_latest_request_json(self, order_number: str) -> Optional[str]:
        """The raw JSON body of the most recent send for this order_number
        (OrderRequests.RequestJson), used to resend an order to a different
        branch without depending on whatever draft currently happens to be
        loaded in the session.
        """
        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            cursor.execute(
                """
                SELECT TOP 1 RequestJson
                FROM OrderRequests
                WHERE OrderNumber = ?
                ORDER BY Id DESC
                """,
                [order_number],
            )
            row = cursor.fetchone()
            return row[0] if row and row[0] else None
        finally:
            conn.close()


# --------------------------------------------------------------------------
# Product calculations
# --------------------------------------------------------------------------


def normalize_vat_percentage(vat_input: Any) -> float:
    """Normalize VAT percentage input to a proper 0-100 percentage."""
    try:
        if vat_input is None or vat_input == "":
            return 15.0

        vat_percentage = float(vat_input)

        if vat_percentage > 10000:
            vat_str = str(int(vat_percentage))
            vat_percentage = float(vat_str[:2]) if len(vat_str) > 2 else 15.0
        elif vat_percentage > 100:
            vat_percentage = vat_percentage / 100
        elif vat_percentage < 1:
            vat_percentage = vat_percentage * 100

        return round(vat_percentage, 2)
    except (ValueError, TypeError):
        return 15.0


def calculate_product_totals(
    quantity: float, unit_price: float, vat_percentage: float, discount: float
) -> Dict[str, float]:
    """Calculate a product row's subtotal/VAT/net-total, given raw inputs."""
    try:
        quantity = float(quantity) if quantity else 0.0
        unit_price = float(unit_price) if unit_price else 0.0
        discount = float(discount) if discount else 0.0

        vat_percentage = normalize_vat_percentage(vat_percentage)
        vat_decimal = vat_percentage / 100

        subtotal = quantity * unit_price
        vat_amount = round((subtotal - discount) * vat_decimal, 2)
        net_total = round(subtotal - discount + vat_amount, 2)
        unit_vat = round(vat_amount / quantity, 2) if quantity > 0 else 0.0

        return {
            "subtotal": subtotal,
            "vat_amount": vat_amount,
            "net_total": net_total,
            "unit_vat": unit_vat,
            "vat_percentage": vat_percentage,
            "vat_percentage_decimal": vat_decimal,
        }
    except Exception as e:
        logger.error(f"Error calculating product totals: {str(e)}")
        return {
            "subtotal": 0.0,
            "vat_amount": 0.0,
            "net_total": 0.0,
            "unit_vat": 0.0,
            "vat_percentage": 15.0,
            "vat_percentage_decimal": 0.15,
        }


# --------------------------------------------------------------------------
# Session-draft <-> API payload
# --------------------------------------------------------------------------


def default_state() -> Dict[str, Any]:
    """Blank/default draft shape: {order_data, products, payments}."""
    template = get_default_data()
    return {
        "order_data": template,
        "products": [p.copy() for p in template["order_products"]],
        "payments": [p.copy() for p in template["payment_methods_with_options"]],
    }


def _format_birthdate(birthdate: str) -> str:
    if not birthdate:
        return "1989-04-11T12:00:00.000Z"
    if "T" in birthdate:
        return birthdate
    if len(birthdate) == 10:  # YYYY-MM-DD
        return f"{birthdate}T12:00:00.000Z"
    return birthdate


def _format_time(time_str: str) -> str:
    if not time_str:
        return ""
    if len(time_str) == 5:  # HH:MM
        return time_str + ":00"
    elif "." in time_str:  # HH:MM:SS.mmm
        return time_str.split(".")[0]
    return time_str


def _get_payment_method_string(payments: List[Dict]) -> str:
    methods = [p.get("payment_method", "") for p in payments if p.get("payment_method")]
    return ",".join(methods) if methods else "COD"


def _determine_payment_status(payments: List[Dict], order_final_total: float) -> str:
    """Determine order payment status based on payment methods and API rules."""
    if not payments:
        return "not_payment"

    total_paid = round(sum(p.get("payment_amount", 0) for p in payments), 2)
    methods = [p.get("payment_method", "") for p in payments]
    statuses = [p.get("payment_status", "") for p in payments]

    credit_methods = ["PostToCredit"]
    if any(method in credit_methods for method in methods):
        return "not_payment"

    digital_wallets = ["Tamara", "Tabby", "MisPay", "Emkan", "Visa"]
    if any(method in digital_wallets for method in methods):
        if "done_payment" in statuses:
            if abs(total_paid - order_final_total) <= 0.01:
                return "done_payment"
            else:
                return "partially_paid"

    if "COD" in methods:
        return "not_payment"

    points_methods = ["RajhiPoints", "NeqatyPoints", "QitafPoints", "Points"]
    if any(method in points_methods for method in methods):
        if "done_payment" in statuses and abs(total_paid - order_final_total) <= 0.01:
            return "done_payment"
        else:
            return "partially_paid"

    if len(methods) > 1:
        return "partially_paid"

    return statuses[0] if statuses else "not_payment"


def calculate_payment_summary(state: Dict[str, Any]) -> Dict[str, float]:
    """Totals used by the calculate-totals/remaining-amount routes and templates."""
    order_data = state.get("order_data") or {}
    products = state.get("products") or []
    payments = state.get("payments") or []

    order_product_total = round(sum(p.get("row_net_total", 0) for p in products), 2)
    order_discount = round(sum(p.get("row_total_discount", 0) for p in products), 2)
    delivery_cost = round(order_data.get("order_delivery_cost", 0), 2)
    order_final_total = round(order_product_total + delivery_cost, 2)

    total_paid = round(sum(p.get("payment_amount", 0) for p in payments), 2)
    remaining_amount = round(max(0, order_final_total - total_paid), 2)

    return {
        "total_paid": total_paid,
        "remaining_amount": remaining_amount,
        "order_final_total": order_final_total,
        "delivery_cost": delivery_cost,
        "products_total": order_product_total,
        "order_discount": order_discount,
    }


def _prepare_products(products: List[Dict]) -> List[Dict]:
    prepared_products = []
    for product in products:
        prepared_product = product.copy()

        vat = prepared_product.get("vat_percentage", 15.0)
        if vat > 1:  # convert percentage to decimal for the API
            prepared_product["vat_percentage"] = round(vat / 100, 4)

        for key, value in prepared_product.items():
            if isinstance(value, float):
                prepared_product[key] = round(value, 2)

        prepared_products.append(prepared_product)

    return prepared_products


def _prepare_payments(
    payments: List[Dict], include_credit_info: bool = True
) -> List[Dict]:
    prepared_payments = []
    for payment in payments:
        prepared = {
            "payment_method": payment.get("payment_method"),
            "payment_status": payment.get("payment_status"),
            "payment_amount": round(float(payment.get("payment_amount", 0)), 2),
            "transaction_id": payment.get("transaction_id", ""),
            "payment_option": payment.get("payment_option", ""),
            "option_commission": round(float(payment.get("option_commission", 0)), 2),
        }
        if include_credit_info:
            prepared["credit_customer_info"] = payment.get("credit_customer_info")
        prepared_payments.append(prepared)
    return prepared_payments


def build_payload(state: Dict[str, Any]) -> Dict[str, Any]:
    """Turn a {order_data, products, payments} draft into the flat-order API JSON."""
    order_data = state.get("order_data") or {}
    products = state.get("products") or []
    payments = state.get("payments") or []

    order_product_total = round(sum(p.get("row_net_total", 0) for p in products), 2)
    order_discount = round(sum(p.get("row_total_discount", 0) for p in products), 2)
    delivery_cost = round(order_data.get("order_delivery_cost", 0), 2)
    order_final_total = round(order_product_total + delivery_cost, 2)

    payment_status = _determine_payment_status(payments, order_final_total)

    final_data = {
        "branch_code": order_data.get("branch_code", ""),
        "order_code": order_data.get("order_code", ""),
        "parent_order_code": order_data.get("parent_order_code", ""),
        "order_creation_date": datetime.now().isoformat() + "Z",
        "order_notes": order_data.get("order_notes", "Don't Ring the bell"),
        "order_product_total_value": order_product_total,
        "is_delivery": order_data.get("is_delivery", 1),
        "order_delivery_cost": delivery_cost,
        "order_total_discount": order_discount,
        "order_final_total_value": order_final_total,
        "order_payment_method": _get_payment_method_string(payments),
        "order_status": order_data.get("order_status", "new"),
        "client_country_code": order_data.get("client_country_code", "966"),
        "client_phone": order_data.get("client_phone", ""),
        "client_first_name": order_data.get("client_first_name", ""),
        "client_middle_name": order_data.get("client_middle_name", ""),
        "client_last_name": order_data.get("client_last_name", ""),
        "client_email": order_data.get("client_email", ""),
        "client_birthdate": _format_birthdate(order_data.get("client_birthdate", "")),
        "client_gender": order_data.get("client_gender", "Male"),
        "order_address": order_data.get("order_address", ""),
        "address_code": order_data.get("address_code", ""),
        "order_payment_status": payment_status,
        "order_gps": order_data.get(
            "order_gps", [21.779006345949554, 39.08578576461103]
        ),
        "order_products": _prepare_products(products),
        "payment_methods_with_options": _prepare_payments(payments),
        "delivery_date": order_data.get("delivery_date", ""),
        "delivery_from_time": _format_time(order_data.get("delivery_from_time", "")),
        "delivery_to_time": _format_time(order_data.get("delivery_to_time", "")),
        "shipping_address_2": order_data.get("shipping_address_2", ""),
        "fullfilment_plant": order_data.get("fullfilment_plant", ""),
    }

    return {k: v for k, v in final_data.items() if v is not None}


def build_upc_payload(state: Dict[str, Any]) -> Dict[str, Any]:
    """Turn a {order_data, products, payments} draft into the UPC API JSON.

    UPC uses the same flat-order schema as GHC but WITHOUT the delivery/fulfillment
    fields: delivery_date, delivery_from_time, delivery_to_time, shipping_address_2,
    fullfilment_plant.
    """
    order_data = state.get("order_data") or {}
    products = state.get("products") or []
    payments = state.get("payments") or []

    order_product_total = round(sum(p.get("row_net_total", 0) for p in products), 2)
    order_discount = round(sum(p.get("row_total_discount", 0) for p in products), 2)
    delivery_cost = round(order_data.get("order_delivery_cost", 0), 2)
    order_final_total = round(order_product_total + delivery_cost, 2)

    payment_status = _determine_payment_status(payments, order_final_total)

    final_data = {
        "branch_code": order_data.get("branch_code", ""),
        "order_code": order_data.get("order_code", ""),
        "parent_order_code": order_data.get("parent_order_code", ""),
        "order_creation_date": datetime.now().isoformat() + "Z",
        "order_notes": order_data.get("order_notes", "Don't Ring the bell"),
        "order_product_total_value": order_product_total,
        "is_delivery": order_data.get("is_delivery", 1),
        "order_delivery_cost": delivery_cost,
        "order_total_discount": order_discount,
        "order_final_total_value": order_final_total,
        "order_payment_method": _get_payment_method_string(payments),
        "order_status": order_data.get("order_status", "new"),
        "client_country_code": order_data.get("client_country_code", "966"),
        "client_phone": order_data.get("client_phone", ""),
        "client_first_name": order_data.get("client_first_name", ""),
        "client_middle_name": order_data.get("client_middle_name", ""),
        "client_last_name": order_data.get("client_last_name", ""),
        "client_email": order_data.get("client_email", ""),
        "client_birthdate": _format_birthdate(order_data.get("client_birthdate", "")),
        "client_gender": order_data.get("client_gender", "Male"),
        "order_address": order_data.get("order_address", ""),
        "address_code": order_data.get("address_code", ""),
        "order_payment_status": payment_status,
        "order_gps": order_data.get(
            "order_gps", [21.779006345949554, 39.08578576461103]
        ),
        "order_products": _prepare_products(products),
        "payment_methods_with_options": _prepare_payments(
            payments, include_credit_info=False
        ),
    }

    return {k: v for k, v in final_data.items() if v is not None}


def validate(payload: Dict[str, Any]) -> List[str]:
    """Validate a built payload against the flat-order payment-method business rules."""
    errors = []

    required_fields = [
        "branch_code",
        "order_code",
        "client_phone",
        "client_first_name",
        "client_last_name",
        "order_address",
    ]
    for field in required_fields:
        if not payload.get(field):
            errors.append(f"Missing required field: {field}")

    if not payload.get("order_products"):
        errors.append("No products in the order")

    payment_method = payload.get("order_payment_method", "")
    payment_status = payload.get("order_payment_status", "")
    total_paid = payload.get("total_paid", 0)
    order_final_total = payload.get("order_final_total_value", 0)

    credit_methods = ["PostToCredit"]
    if any(method in payment_method for method in credit_methods):
        if payment_status != "not_payment":
            errors.append("Credit payment methods must have 'not_payment' status")
        if abs(total_paid - order_final_total) > 0.01:
            errors.append(
                f"Credit payments must cover full order amount. Current: ${total_paid}, Required: ${order_final_total}"
            )

    digital_wallets = ["Tamara", "Tabby", "MisPay", "Emkan", "Visa"]
    if any(method in payment_method for method in digital_wallets):
        if (
            payment_status == "done_payment"
            and abs(total_paid - order_final_total) > 0.01
        ):
            errors.append(
                f"Digital wallet 'done_payment' must equal order total. Current: ${total_paid}, Required: ${order_final_total}"
            )

    if "COD" in payment_method and payment_status != "not_payment":
        errors.append("COD payments must have 'not_payment' status")

    return errors
