import pyodbc
import logging
import json
import os
import requests
import socket
from datetime import datetime
from urllib.parse import urlparse
from typing import Dict, List, Any, Optional
from flask import session

# Import configuration
from config import (
    DEFAULT_API_ENDPOINT,
    get_default_data,
    DB_CONFIG,
)

logger = logging.getLogger(__name__)


class ValidationError(Exception):
    """Custom validation error"""

    pass


class DatabaseManager:
    """Database connection and query management"""

    _cached_driver = None

    @staticmethod
    def get_db_connection() -> Optional[pyodbc.Connection]:
        """
        Establish database connection with fallback drivers.
        Caches the successfully used driver to optimize future connections.
        """
        drivers = []

        # Priority to cached driver
        if DatabaseManager._cached_driver:
            drivers.append(DatabaseManager._cached_driver)

        # Use configured driver
        if (
            DB_CONFIG["driver"]
            and DB_CONFIG["driver"] != DatabaseManager._cached_driver
        ):
            drivers.append(DB_CONFIG["driver"])

        # Fallback drivers
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
                    f"SERVER={DB_CONFIG['server']};"
                    f"DATABASE={DB_CONFIG['database']};"
                    f"UID={DB_CONFIG['username']};"
                    f"PWD={DB_CONFIG['password']};"
                    f"Trusted_Connection=no;"
                    f"Encrypt=no;"
                )
                conn = pyodbc.connect(conn_str)
                logger.info(f"Connected successfully using {driver}")

                # Cache the successful driver
                if DatabaseManager._cached_driver != driver:
                    DatabaseManager._cached_driver = driver
                    logger.info(f"Cached database driver: {driver}")

                return conn
            except pyodbc.Error as e:
                logger.warning(f"Failed with {driver}: {str(e)}")
                continue

        logger.error("Could not connect with any available driver")
        return None

    @staticmethod
    def lookup_item(material_number: str, **filters) -> Dict[str, Any]:
        """
        Look up item details from database
        """
        if (
            not material_number
            or not material_number.isdigit()
            or len(material_number) != 6
        ):
            raise ValidationError("Material number must be 6 digits")

        conn = DatabaseManager.get_db_connection()
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

            # Add optional filters
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


class ProductCalculator:
    """Product calculation utilities"""

    @staticmethod
    def normalize_vat_percentage(vat_input: Any) -> float:
        """
        Normalize VAT percentage input to proper format
        """
        try:
            if vat_input is None or vat_input == "":
                return 15.0

            vat_percentage = float(vat_input)

            # Handle various VAT percentage formats
            if vat_percentage > 10000:
                # Extract first two digits for extremely large numbers
                vat_str = str(int(vat_percentage))
                vat_percentage = float(vat_str[:2]) if len(vat_str) > 2 else 15.0
            elif vat_percentage > 100:
                # Convert to percentage (e.g., 1500 -> 15.0)
                vat_percentage = vat_percentage / 100
            elif vat_percentage < 1:
                # Convert decimal to percentage (e.g., 0.15 -> 15.0)
                vat_percentage = vat_percentage * 100

            return round(vat_percentage, 2)
        except (ValueError, TypeError):
            return 15.0  # Default VAT

    @staticmethod
    def calculate_product_totals(
        quantity: float, unit_price: float, vat_percentage: float, discount: float
    ) -> Dict[str, float]:
        """
        Calculate product totals including VAT and discounts
        """
        try:
            quantity = float(quantity) if quantity else 0.0
            unit_price = float(unit_price) if unit_price else 0.0
            discount = float(discount) if discount else 0.0

            # Normalize VAT percentage
            vat_percentage = ProductCalculator.normalize_vat_percentage(vat_percentage)
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


class OrderManager:
    """Order data management and validation"""

    @staticmethod
    def initialize_session_data():
        """Initialize or update session data with defaults"""
        try:
            # Try to load last saved order data
            last_order_file = "last_order.json"
            if os.path.exists(last_order_file):
                with open(last_order_file, "r") as f:
                    last_order_data = json.load(f)
            else:
                last_order_data = None

            if "order_data" not in session:
                if last_order_data:
                    session["order_data"] = last_order_data
                else:
                    session["order_data"] = get_default_data()

            if "products" not in session:
                if last_order_data and "order_products" in last_order_data:
                    session["products"] = last_order_data["order_products"].copy()
                else:
                    session["products"] = session["order_data"]["order_products"].copy()

            if "payments" not in session:
                if (
                    last_order_data
                    and "payment_methods_with_options" in last_order_data
                ):
                    session["payments"] = last_order_data[
                        "payment_methods_with_options"
                    ].copy()
                else:
                    session["payments"] = session["order_data"][
                        "payment_methods_with_options"
                    ].copy()

            if "api_endpoint" not in session:
                session["api_endpoint"] = DEFAULT_API_ENDPOINT

            # Ensure session is marked as modified
            session.modified = True

        except Exception as e:
            logger.error(f"Error initializing session data: {str(e)}")
            # Reset to defaults on error
            session.clear()
            default_data = get_default_data()
            session.update(
                {
                    "order_data": default_data,
                    "products": default_data["order_products"].copy(),
                    "payments": default_data["payment_methods_with_options"].copy(),
                    "api_endpoint": DEFAULT_API_ENDPOINT,
                }
            )

    @staticmethod
    def save_last_order(order_data: Dict[str, Any]):
        """Save order data to file for persistence"""
        try:
            with open("last_order.json", "w") as f:
                json.dump(order_data, f, indent=2)
        except Exception as e:
            logger.error(f"Error saving last order: {str(e)}")

    @staticmethod
    def prepare_order_data() -> Dict[str, Any]:
        """Prepare complete order data for API submission with API rules"""
        try:
            order_data = session.get("order_data", get_default_data())
            products = session.get("products", [])
            payments = session.get("payments", [])

            # Calculate totals
            order_product_total = round(
                sum(p.get("row_net_total", 0) for p in products), 2
            )
            order_discount = round(
                sum(p.get("row_total_discount", 0) for p in products), 2
            )
            delivery_cost = round(order_data.get("order_delivery_cost", 0), 2)
            order_final_total = round(order_product_total + delivery_cost, 2)

            # Calculate payment totals
            total_paid = round(sum(p.get("payment_amount", 0) for p in payments), 2)
            remaining_amount = round(order_final_total - total_paid, 2)

            # Apply API-specific rules for payment methods
            payment_status = OrderManager._determine_payment_status(payments)

            # SPECIAL RULE: For credit methods, payment amount must equal order total
            credit_methods = ["PostToCredit"]
            payment_methods = [p.get("payment_method", "") for p in payments]

            if any(method in credit_methods for method in payment_methods):
                # Force payment amount to equal order total for credit methods
                if payments and abs(total_paid - order_final_total) > 0.01:
                    adjustment_needed = order_final_total - total_paid
                    payments[0]["payment_amount"] = round(
                        payments[0].get("payment_amount", 0) + adjustment_needed, 2
                    )
                    total_paid = order_final_total
                    remaining_amount = 0.0

                    # Update session
                    session["payments"] = payments
                    session.modified = True

            # Prepare final order data
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
                "order_payment_method": OrderManager._get_payment_method_string(
                    payments
                ),
                "order_status": order_data.get("order_status", "new"),
                "client_country_code": order_data.get("client_country_code", "966"),
                "client_phone": order_data.get("client_phone", ""),
                "client_first_name": order_data.get("client_first_name", ""),
                "client_middle_name": order_data.get("client_middle_name", ""),
                "client_last_name": order_data.get("client_last_name", ""),
                "client_email": order_data.get("client_email", ""),
                "client_birthdate": OrderManager._format_birthdate(
                    order_data.get("client_birthdate", "")
                ),
                "client_gender": order_data.get("client_gender", "Male"),
                "order_address": order_data.get("order_address", ""),
                "order_payment_status": payment_status,
                "order_gps": order_data.get(
                    "order_gps", [21.779006345949554, 39.08578576461103]
                ),
                "order_products": OrderManager._prepare_products(products),
                "payment_methods_with_options": OrderManager._prepare_payments(
                    payments
                ),
                "delivery_date": order_data.get("delivery_date", ""),
                "delivery_from_time": OrderManager._format_time(
                    order_data.get("delivery_from_time", "")
                ),
                "delivery_to_time": OrderManager._format_time(
                    order_data.get("delivery_to_time", "")
                ),
                "shipping_address_2": order_data.get("shipping_address_2", ""),
                "fullfilment_plant": order_data.get("fullfilment_plant", ""),
                "total_paid": total_paid,
                "remaining_amount": remaining_amount,
            }

            # Save this order data for persistence
            OrderManager.save_last_order(final_data)

            return {k: v for k, v in final_data.items() if v is not None}

        except Exception as e:
            logger.error(f"Error preparing order data: {str(e)}")
            raise

    @staticmethod
    def calculate_payment_summary() -> Dict[str, float]:
        """Calculate payment summary including total paid and remaining amount"""
        try:
            order_data = session.get("order_data", get_default_data())
            products = session.get("products", [])
            payments = session.get("payments", [])

            order_product_total = round(
                sum(p.get("row_net_total", 0) for p in products), 2
            )
            order_discount = round(
                sum(p.get("row_total_discount", 0) for p in products), 2
            )
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
        except Exception as e:
            logger.error(f"Error calculating payment summary: {str(e)}")
            return {
                "total_paid": 0.0,
                "remaining_amount": 0.0,
                "order_final_total": 0.0,
                "delivery_cost": 0.0,
                "products_total": 0.0,
                "order_discount": 0.0,
            }

    @staticmethod
    def _get_payment_method_string(payments: List[Dict]) -> str:
        """Convert payment methods to comma-separated string with API-compatible names"""
        methods = []
        for payment in payments:
            method = payment.get("payment_method", "")
            # Map to API-compatible names
            if method == "cash":
                methods.append("COD")
            elif method == "credit_card":
                methods.append("Visa")
            else:
                methods.append(method)
        return ",".join(methods) if methods else "COD"

    @staticmethod
    def _determine_payment_status(payments: List[Dict]) -> str:
        """Determine order payment status based on payment methods and API rules"""
        if not payments:
            return "not_payment"

        # Calculate totals for validation
        order_data = session.get("order_data", get_default_data())
        products = session.get("products", [])
        order_product_total = round(sum(p.get("row_net_total", 0) for p in products), 2)
        delivery_cost = round(order_data.get("order_delivery_cost", 0), 2)
        order_final_total = round(order_product_total + delivery_cost, 2)

        total_paid = round(sum(p.get("payment_amount", 0) for p in payments), 2)

        methods = [p.get("payment_method", "") for p in payments]
        statuses = [p.get("payment_status", "") for p in payments]

        # SPECIAL RULES FOR SPECIFIC PAYMENT METHODS

        # Rule 1: Credit methods (PostToCredit) must have not_payment status
        credit_methods = ["PostToCredit"]
        if any(method in credit_methods for method in methods):
            # For credit methods, payment status must be not_payment
            # and payment amount must equal order total
            if abs(total_paid - order_final_total) > 0.01:
                # Auto-adjust payment amount to match total
                if payments:
                    adjustment_needed = order_final_total - total_paid
                    payments[0]["payment_amount"] = round(
                        payments[0].get("payment_amount", 0) + adjustment_needed, 2
                    )
                    # Update session
                    session["payments"] = payments
                    session.modified = True
            return "not_payment"

        # Rule 2: Digital wallets (Tamara, Tabby, etc.) should be done_payment when amount matches total
        digital_wallets = ["Tamara", "Tabby", "MisPay", "Emkan", "Visa"]
        if any(method in digital_wallets for method in methods):
            if "done_payment" in statuses:
                if abs(total_paid - order_final_total) <= 0.01:
                    return "done_payment"
                else:
                    return "partially_paid"

        # Rule 3: COD/cash should be not_payment
        cod_methods = ["cash", "COD"]
        if any(method in cod_methods for method in methods):
            return "not_payment"

        # Rule 4: Points systems
        points_methods = ["RajhiPoints", "NeqatyPoints", "QitafPoints", "Points"]
        if any(method in points_methods for method in methods):
            if (
                "done_payment" in statuses
                and abs(total_paid - order_final_total) <= 0.01
            ):
                return "done_payment"
            else:
                return "partially_paid"

        # Default logic
        if len(methods) > 1:
            return "partially_paid"

        return statuses[0] if statuses else "not_payment"

    @staticmethod
    def _format_birthdate(birthdate: str) -> str:
        """Format birthdate to ISO format"""
        if not birthdate:
            return "1989-04-11T12:00:00.000Z"

        if "T" in birthdate:
            return birthdate

        if len(birthdate) == 10:  # YYYY-MM-DD
            return f"{birthdate}T12:00:00.000Z"

        return birthdate

    @staticmethod
    def _format_time(time_str: str) -> str:
        """Format time string to HH:MM:SS"""
        if not time_str:
            return ""

        if len(time_str) == 5:  # HH:MM
            return time_str + ":00"
        elif "." in time_str:  # HH:MM:SS.mmm
            return time_str.split(".")[0]

        return time_str

    @staticmethod
    def _prepare_products(products: List[Dict]) -> List[Dict]:
        """Prepare products data with proper formatting"""
        prepared_products = []
        for product in products:
            prepared_product = product.copy()

            # Ensure VAT is in decimal format for API
            vat = prepared_product.get("vat_percentage", 15.0)
            if vat > 1:  # Convert percentage to decimal
                prepared_product["vat_percentage"] = round(vat / 100, 4)

            # Round all float values to 2 decimal places
            for key, value in prepared_product.items():
                if isinstance(value, float):
                    prepared_product[key] = round(value, 2)

            prepared_products.append(prepared_product)

        return prepared_products

    @staticmethod
    def _prepare_payments(payments: List[Dict]) -> List[Dict]:
        """Prepare payments data with proper formatting"""
        prepared_payments = []
        for payment in payments:
            prepared_payment = payment.copy()

            # Round all float values to 2 decimal places
            for key, value in prepared_payment.items():
                if isinstance(value, float):
                    prepared_payment[key] = round(value, 2)

            prepared_payments.append(prepared_payment)

        return prepared_payments

    @staticmethod
    def validate_order_data(order_data: Dict[str, Any]) -> List[str]:
        """Validate order data before sending to API with specific payment rules"""
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
            if not order_data.get(field):
                errors.append(f"Missing required field: {field}")

        if not order_data.get("order_products"):
            errors.append("No products in the order")

        # Payment method validation
        payment_method = order_data.get("order_payment_method", "")
        payment_status = order_data.get("order_payment_status", "")
        total_paid = order_data.get("total_paid", 0)
        order_final_total = order_data.get("order_final_total_value", 0)

        # Rule 1: Credit methods require specific conditions
        credit_methods = ["PostToCredit"]
        if any(method in payment_method for method in credit_methods):
            if payment_status != "not_payment":
                errors.append("Credit payment methods must have 'not_payment' status")
            if abs(total_paid - order_final_total) > 0.01:
                errors.append(
                    f"Credit payments must cover full order amount. Current: ${total_paid}, Required: ${order_final_total}"
                )

        # Rule 2: Digital wallets validation
        digital_wallets = ["Tamara", "Tabby", "MisPay", "Emkan", "Visa"]
        if any(method in payment_method for method in digital_wallets):
            if (
                payment_status == "done_payment"
                and abs(total_paid - order_final_total) > 0.01
            ):
                errors.append(
                    f"Digital wallet 'done_payment' must equal order total. Current: ${total_paid}, Required: ${order_final_total}"
                )

        # Rule 3: COD validation
        if "COD" in payment_method and payment_status != "not_payment":
            errors.append("COD payments must have 'not_payment' status")

        return errors


class APIManager:
    """API communication management"""

    @staticmethod
    def send_order(url: str, order_data: Dict[str, Any]) -> Dict[str, Any]:
        """Send order data to API endpoint"""
        try:
            # Disable warnings for unverified HTTPS requests
            import urllib3

            urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

            headers = {"Content-Type": "application/json", "Accept": "application/json"}
            response = requests.post(
                url, json=order_data, headers=headers, timeout=30, verify=False
            )

            return {
                "status_code": response.status_code,
                "response_text": response.text,
                "url_sent": url,
                "success": response.status_code in [200, 201],
            }
        except requests.exceptions.RequestException as e:
            logger.error(f"API request failed: {str(e)}")
            raise

    @staticmethod
    def test_endpoint(url: str) -> Dict[str, Any]:
        """Test API endpoint connectivity"""
        try:
            parsed = urlparse(url)
            host = parsed.hostname
            port = parsed.port or (80 if parsed.scheme == "http" else 443)

            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(3)
            result = sock.connect_ex((host, port))
            sock.close()

            return {"status": "Online" if result == 0 else "Offline", "url": url}
        except Exception as e:
            return {"status": "Error", "error": str(e)}
