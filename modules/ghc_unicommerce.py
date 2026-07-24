"""GHC External Uni-Commerce module.

This is a real, existing integration (per the user's module list), but its
production/testing API URLs and DB connection are not finalized in config.py
yet — config.py only ever had placeholder "Whites UniCommerce" entries with
api_url/cancel_url = None and available = False. This module replaces that
placeholder: the module itself is marked available (it exists), but its
environments stay unavailable/URL-less until the real routing is supplied.

Schema derived from the 20 example payloads in "request_examples/GHC Uni-Commerce/"
(carriers: Amazon, Aramex, Naqel, SMSA, Tabby, Tamara, Trendyol, Redbox, OwnFleet;
including return/partial-return/mixed-payment variants). This is a flat invoice
schema, completely different from the flat *order* schema used by
ghc_ecommerce/upc_ecommerce (see modules/flat_order.py): ReferenceNumber,
InvoiceConsumer, DeliveryDetails, RowItems, IsReturn/ParentReferenceNumber, and
PaidOnlineAmount/PaidWithPointsAmount/CustomerCreditAmount instead of a
payment-methods-with-options list.

Aggregation formula confirmed identically across all 20 examples (see the
per-row and per-invoice math in build_payload below) — every row's GrossAmount/
RowTotalDiscount/ItemVat/RowTotalVat/NetAmount and every invoice-level
GrossAmount/TotalDiscount/TotalVat/NetAmount reproduce exactly given
Quantity/ItemPrice/ItemDiscount/VatPercentage per row plus DeliveryFees.
"""

import logging
from datetime import datetime
from typing import Any, Dict, List, Optional

import pyodbc

from config import CLIENT_LOGOS

from . import flat_order
from .base import ModuleEnvironment, OrderModule, ValidationError
from .db_config import DB_CONFIGS

logger = logging.getLogger(__name__)


# --------------------------------------------------------------------------
# Database access
# --------------------------------------------------------------------------


class GhcUnicommerceDatabaseManager:
    """DB manager for the Uni-Commerce module, parameterized by its own DB config.

    Connection-fallback logic mirrors modules/flat_order.py's
    FlatOrderDatabaseManager (same driver-probing pattern); kept as a separate
    implementation rather than a shared base class since this module's real
    schema/DB is still unconfirmed and may diverge once real credentials land.
    """

    def __init__(self, db_config: Dict[str, Any]):
        self.db_config = db_config
        self._cached_driver: Optional[str] = None

    def get_db_connection(self) -> Optional[pyodbc.Connection]:
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
        """Look up a material by 6-digit code, returning fields to prefill a RowItem draft.

        # TODO(db-creds): confirm real schema. Guessing the same dbo.Items table
        # shape used by modules/flat_order.py's lookup_item, since both modules
        # look up SKUs by material number/barcode — may need to change once the
        # real Uni-Commerce DB connection is supplied.
        """
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
                        I.Name AS EnglishName,
                        IP.Price AS UnitPrice,
                        TT.Rate AS VatRate
                    FROM dbo.Items AS I
                             LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
                             INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
                             INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON IUM.Id = IUOMB.ItemUnitOfMeasureId
                             LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
                    WHERE RIGHT(I.MaterialNumber, 6) = ?
                      AND IP.IsActive = 1
                      AND IP.Price IS NOT NULL
                      AND IP.ToDate > GETDATE()
                    ORDER BY I.Id DESC
                    """  # TODO(db-creds): confirm real table/column names
            cursor.execute(query, [material_number])
            row = cursor.fetchone()

            if not row:
                raise ValueError("No item found with the specified criteria")

            return {
                "material_number": row[0] if row[0] else f"000000000000{material_number}",
                "barcode": row[1] if row[1] else f"BC{material_number}",
                "item_name": row[2] if row[2] else f"Item {material_number}",
                "item_price": float(row[3]) if row[3] else 0.0,
                "vat_percentage": float(row[4]) if row[4] else 0.0,
                "quantity": 1.0,
            }
        finally:
            conn.close()

    def lookup_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        """Look up an existing consumer by phone, returning InvoiceConsumer-shaped fields.

        # TODO(db-creds): confirm real table/column names once real DB
        # credentials/schema are supplied — guessing a dbo.Customers shape
        # consistent with modules/flat_order.py's equivalent lookup.
        """
        phone = (phone or "").strip()
        if not phone:
            raise ValidationError("Phone number is required")

        conn = self.get_db_connection()
        if not conn:
            raise ConnectionError("Database connection failed")

        try:
            cursor = conn.cursor()
            query = """
                    SELECT TOP 1
                        C.CustomerNumber, C.FirstName, C.MiddleName, C.LastName,
                        C.Email, C.PhoneNumber, C.Gender, C.BirthDate,
                        C.NationalId, C.Nationality
                    FROM dbo.Customers AS C
                    WHERE C.PhoneNumber = ?
                      AND C.IsActive = 1
                    ORDER BY C.Id DESC
                    """  # TODO(db-creds): confirm real table/column names
            cursor.execute(query, [phone])
            row = cursor.fetchone()

            if not row:
                return None

            return {
                "consumer_code": row[0] or "",
                "first_name": row[1] or "",
                "middle_name": row[2] or "",
                "last_name": row[3] or "",
                "email": row[4] or "",
                "primary_phone_number": row[5] or phone,
                "gender": row[6] or "",
                "birth_date": row[7].isoformat() if row[7] else "",
                "national_id": row[8] or "",
                "nationality": row[9] or "",
            }
        finally:
            conn.close()


# --------------------------------------------------------------------------
# Session-draft <-> API payload
# --------------------------------------------------------------------------


def default_state() -> Dict[str, Any]:
    """Blank/default draft shape for a Uni-Commerce invoice."""
    return {
        "reference_number": "",
        "online_order_number": "",
        "is_return": False,
        "parent_reference_number": "",
        "order_creation_date": "",
        "customer_name": "AMAZON",
        "paid_online_amount": 0.0,
        "paid_with_points_amount": 0.0,
        "consumer": {
            "first_name": "",
            "middle_name": "",
            "last_name": "",
            "consumer_code": "",
            "gender": "",
            "birth_date": "",
            "primary_phone_number": "",
            "email": "",
            "national_id": "",
            "nationality": "",
        },
        "delivery": {
            "delivery_phone_number": "",
            "delivery_address": "",
            "delivery_location_url": "",
            "delivery_notes": "",
            "delivery_fees": 0.0,
        },
        "row_items": [],
    }


def _format_birth_date(value: Optional[str]) -> Optional[str]:
    """Match the InvoiceConsumer.BirthDate convention seen in the examples
    (date-only inputs become midnight, e.g. "1984-04-04T00:00:00")."""
    if not value:
        return None
    if "T" in value:
        return value
    if len(value) == 10:  # YYYY-MM-DD
        return f"{value}T00:00:00"
    return value


def _row_payload(item: Dict[str, Any]) -> Dict[str, Any]:
    """Build one RowItems entry, computing GrossAmount/RowTotalDiscount/ItemVat/
    RowTotalVat/NetAmount from Quantity/ItemPrice/ItemDiscount/VatPercentage —
    formula confirmed against all 20 example payloads:
      GrossAmount = Quantity * ItemPrice
      RowTotalDiscount = ItemDiscount * Quantity
      ItemVat (per unit, post-discount) = (ItemPrice - ItemDiscount) * VatRate
      RowTotalVat = ItemVat * Quantity
      NetAmount = GrossAmount - RowTotalDiscount + RowTotalVat
    """
    quantity = float(item.get("quantity") or 0)
    item_price = float(item.get("item_price") or 0)
    item_discount = float(item.get("item_discount") or 0)
    vat_percentage = flat_order.normalize_vat_percentage(item.get("vat_percentage"))
    vat_decimal = round(vat_percentage / 100, 4)

    gross_amount = round(quantity * item_price, 2)
    row_total_discount = round(item_discount * quantity, 2)
    item_vat = round((item_price - item_discount) * vat_decimal, 2)
    row_total_vat = round(item_vat * quantity, 2)
    net_amount = round(gross_amount - row_total_discount + row_total_vat, 2)

    return {
        "Quantity": quantity,
        "MaterialNumber": item.get("material_number", ""),
        "ItemPrice": item_price,
        "ItemDiscount": item_discount,
        "RowTotalDiscount": row_total_discount,
        "ItemVat": item_vat,
        "RowTotalVat": row_total_vat,
        "BatchNumber": item.get("batch_number") or None,
        "ExpireDate": item.get("expire_date") or None,
        "SerialNumber": item.get("serial_number") or None,
        "Barcode": item.get("barcode", ""),
        "ScannedCode": item.get("scanned_code") or None,
        "GrossAmount": gross_amount,
        "NetAmount": net_amount,
        "VatPercentage": vat_decimal,
        "OfferIdentifier": item.get("offer_identifier") or None,
    }


def build_payload(state: Dict[str, Any]) -> Dict[str, Any]:
    """Turn a Uni-Commerce draft into the exact invoice JSON the API expects."""
    row_items = [_row_payload(item) for item in state.get("row_items") or []]

    gross_amount = round(sum(r["GrossAmount"] for r in row_items), 2)
    total_discount = round(sum(r["RowTotalDiscount"] for r in row_items), 2)
    total_vat = round(sum(r["RowTotalVat"] for r in row_items), 2)

    delivery = state.get("delivery") or {}
    delivery_fees = round(float(delivery.get("delivery_fees") or 0), 2)
    net_amount = round(sum(r["NetAmount"] for r in row_items) + delivery_fees, 2)

    paid_online_amount = round(float(state.get("paid_online_amount") or 0), 2)
    paid_with_points_amount = round(float(state.get("paid_with_points_amount") or 0), 2)
    customer_credit_amount = round(
        net_amount - paid_online_amount - paid_with_points_amount, 2
    )

    consumer = state.get("consumer") or {}
    order_creation_date = state.get("order_creation_date") or datetime.now().isoformat(
        timespec="milliseconds"
    )

    return {
        "ReferenceNumber": state.get("reference_number", ""),
        "OnlineOrderNumber": state.get("online_order_number", ""),
        "IsReturn": bool(state.get("is_return", False)),
        "ParentReferenceNumber": state.get("parent_reference_number") or None,
        "OrderCreationDate": order_creation_date,
        "TotalDiscount": total_discount,
        "TotalVat": total_vat,
        "GrossAmount": gross_amount,
        "NetAmount": net_amount,
        "CustomerName": state.get("customer_name", ""),
        "CustomerCreditAmount": customer_credit_amount,
        "PaidOnlineAmount": paid_online_amount,
        "PaidWithPointsAmount": paid_with_points_amount,
        "InvoiceConsumer": {
            "FirstName": consumer.get("first_name") or None,
            "MiddleName": consumer.get("middle_name") or None,
            "LastName": consumer.get("last_name") or None,
            "ConsumerCode": consumer.get("consumer_code") or None,
            "Gender": consumer.get("gender") or None,
            "BirthDate": _format_birth_date(consumer.get("birth_date")),
            "PrimaryPhoneNumber": consumer.get("primary_phone_number") or None,
            "Email": consumer.get("email") or None,
            "NationalId": consumer.get("national_id") or None,
            "Nationality": consumer.get("nationality") or None,
        },
        "DeliveryDetails": {
            "DeliveryPhoneNumber": delivery.get("delivery_phone_number") or None,
            "DeliveryAddress": delivery.get("delivery_address") or None,
            "DeliveryLocationUrl": delivery.get("delivery_location_url") or None,
            "DeliveryNotes": delivery.get("delivery_notes") or None,
            "DeliveryFees": delivery_fees,
        },
        "RowItems": row_items,
    }


def validate(payload: Dict[str, Any]) -> List[str]:
    """Validate a built Uni-Commerce payload.

    Rules are limited to what's directly evidenced by the example payloads:
    - ReferenceNumber/CustomerName required, at least one RowItem required.
    - Returns (IsReturn=true) must carry a ParentReferenceNumber (every return/
      partial-return example does).
    - PaidOnlineAmount + PaidWithPointsAmount + CustomerCreditAmount must equal
      NetAmount within rounding tolerance — holds exactly across all 20 examples,
      including the mixed-payment ones (SMSA-MIX-001, TAB-MIX-003, ARMX-RET-MIX-004).
    """
    errors = []

    if not payload.get("ReferenceNumber"):
        errors.append("Missing required field: ReferenceNumber")

    if not payload.get("CustomerName"):
        errors.append("Missing required field: CustomerName")

    if not payload.get("RowItems"):
        errors.append("No row items in the invoice")

    if payload.get("IsReturn") and not payload.get("ParentReferenceNumber"):
        errors.append("Returns must reference a ParentReferenceNumber")

    net_amount = payload.get("NetAmount", 0)
    paid_online_amount = payload.get("PaidOnlineAmount", 0)
    paid_with_points_amount = payload.get("PaidWithPointsAmount", 0)
    customer_credit_amount = payload.get("CustomerCreditAmount", 0)
    total_settled = round(
        paid_online_amount + paid_with_points_amount + customer_credit_amount, 2
    )
    if abs(total_settled - net_amount) > 0.01:
        errors.append(
            "PaidOnlineAmount + PaidWithPointsAmount + CustomerCreditAmount must equal "
            f"NetAmount. Current: {total_settled}, Required: {net_amount}"
        )

    return errors


class GhcUnicommerceModule(OrderModule):
    key = "ghc_unicommerce"
    label = "GHC Uni-Commerce"
    client = "GHC"
    available = True  # the integration exists; environments below are pending config

    def __init__(self):
        db_config = DB_CONFIGS["ghc_unicommerce"]
        self.environments: Dict[str, ModuleEnvironment] = {
            "GHC Uni-Commerce Production": ModuleEnvironment(
                key="GHC Uni-Commerce Production",
                environment="Production",
                description="GHC Uni-Commerce live routing (pending API URL).",
                accent="aurora",
                cue="Automation",
                icon="bi-cpu",
                route_label="Pending lane",
                visual_url=CLIENT_LOGOS["Whites UniCommerce"],
                visual_alt="GHC Uni-Commerce logo",
                available=False,  # TODO(db-creds): flip once real API URL is supplied
                api_url=None,
                cancel_url=None,
                db_config=db_config,
            ),
            "GHC Uni-Commerce Testing": ModuleEnvironment(
                key="GHC Uni-Commerce Testing",
                environment="Testing",
                description="GHC Uni-Commerce QA routing (pending API URL).",
                accent="violet",
                cue="Staging",
                icon="bi-hourglass-split",
                route_label="Pending lane",
                visual_url=CLIENT_LOGOS["Whites UniCommerce"],
                visual_alt="GHC Uni-Commerce logo",
                available=False,  # TODO(db-creds): flip once real API URL is supplied
                api_url=None,
                cancel_url=None,
                db_config=db_config,
            ),
        }
        self._db = GhcUnicommerceDatabaseManager(db_config)

    def default_state(self) -> Dict[str, Any]:
        return default_state()

    def build_payload(self, state: Dict[str, Any]) -> Dict[str, Any]:
        return build_payload(state)

    def validate(self, payload: Dict[str, Any]) -> List[str]:
        return validate(payload)

    def lookup_item(self, code: str, **filters) -> Dict[str, Any]:
        return self._db.lookup_item(code, **filters)

    def lookup_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        return self._db.lookup_consumer_by_phone(phone)


MODULE = GhcUnicommerceModule()
