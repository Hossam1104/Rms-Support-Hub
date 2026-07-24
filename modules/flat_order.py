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
from datetime import datetime
from typing import Any, Dict, List, Optional

import pyodbc

from config import get_default_data

from .base import ValidationError

logger = logging.getLogger(__name__)


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
                    WHERE C.PhoneNumber = ?
                      AND C.IsActive = 1
                    ORDER BY C.Id DESC
                    """  # TODO(db-creds): confirm real table/column names
            cursor.execute(query, [phone])
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
