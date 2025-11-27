import socket
import logging
from datetime import datetime
from urllib.parse import urlparse
from typing import Dict, List, Any, Optional
import json
import os

import pyodbc
import requests
from flask import (
    Flask,
    render_template,
    request,
    jsonify,
    flash,
    redirect,
    url_for,
    session,
)
from dotenv import load_dotenv

# Import configuration
from config import (
    API_URLS,
    DEFAULT_API_ENDPOINT,
    PAYMENT_METHODS,
    PAYMENT_STATUSES,
    PAYMENT_OPTIONS,
    get_default_data,
    CANCEL_API_URLS,
    DB_CONFIG,
    AppConfig,
)

load_dotenv()

app = Flask(__name__)
app.config.from_object(AppConfig)

# Setup logging
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


class ValidationError(Exception):
    """Custom validation error"""
    pass


class DatabaseManager:
    """Database connection and query management"""

    @staticmethod
    def get_db_connection() -> Optional[pyodbc.Connection]:
        """
        Establish database connection with fallback drivers
        """
        drivers = [
            "ODBC Driver 18 for SQL Server",
            "ODBC Driver 17 for SQL Server",
            "ODBC Driver 13 for SQL Server",
            "SQL Server Native Client 11.0",
            "SQL Server",
        ]

        # Use configured driver first
        if DB_CONFIG["driver"]:
            drivers.insert(0, DB_CONFIG["driver"])

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
                I.MaterialNumber,
                IUOMB.UniversalBarCode,
                I.Name AS EnglishName,
                I.NativeName AS ArabicName,
                IP.Price AS UnitPrice,
                TT.Rate AS VatRate,
                CAST(ROUND(((IP.Price * TT.Rate)/100) + IP.Price, 2) AS DECIMAL(10,2)) AS NetPrice
            FROM dbo.Items AS I
            LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
            INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
            INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON IUM.Id = IUOMB.ItemUnitOfMeasureId
            LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
            WHERE RIGHT(I.MaterialNumber, 6) = ?
            AND IP.IsActive = 1
            AND IP.Price IS NOT NULL
            AND IP.ToDate > GETDATE()
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
                with open(last_order_file, 'r') as f:
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
                if last_order_data and "payment_methods_with_options" in last_order_data:
                    session["payments"] = last_order_data["payment_methods_with_options"].copy()
                else:
                    session["payments"] = session["order_data"]["payment_methods_with_options"].copy()

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
            with open("last_order.json", 'w') as f:
                json.dump(order_data, f, indent=2)
        except Exception as e:
            logger.error(f"Error saving last order: {str(e)}")

    @staticmethod
    def prepare_order_data() -> Dict[str, Any]:
        """Prepare complete order data for API submission"""
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
                "order_payment_status": OrderManager._determine_payment_status(
                    payments
                ),
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
            order_data = OrderManager.prepare_order_data()
            products = session.get("products", [])
            payments = session.get("payments", [])

            order_product_total = round(
                sum(p.get("row_net_total", 0) for p in products), 2
            )
            delivery_cost = round(order_data.get("order_delivery_cost", 0), 2)
            order_final_total = round(order_product_total + delivery_cost, 2)

            total_paid = round(sum(p.get("payment_amount", 0) for p in payments), 2)
            remaining_amount = round(max(0, order_final_total - total_paid), 2)

            return {
                "total_paid": total_paid,
                "remaining_amount": remaining_amount,
                "order_final_total": order_final_total
            }
        except Exception as e:
            logger.error(f"Error calculating payment summary: {str(e)}")
            return {"total_paid": 0.0, "remaining_amount": 0.0, "order_final_total": 0.0}

    @staticmethod
    def _get_payment_method_string(payments: List[Dict]) -> str:
        """Convert payment methods to comma-separated string"""
        methods = [
            p.get("payment_method", "") for p in payments if p.get("payment_method")
        ]
        return ",".join(methods) if methods else "cash"

    @staticmethod
    def _determine_payment_status(payments: List[Dict]) -> str:
        """Determine order payment status based on payment methods"""
        if not payments:
            return "not_payment"

        methods = [p.get("payment_method", "") for p in payments]
        statuses = [p.get("payment_status", "") for p in payments]

        if len(methods) > 1:
            return "partially_paid"

        if (
                methods
                and methods[0] in ["Visa", "Points"]
                and statuses[0] == "done_payment"
        ):
            return "done_payment"
        elif (
                methods
                and methods[0] in ["PostToCredit", "cash"]
                and statuses[0] == "not_payment"
        ):
            return "not_payment"

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
        """Validate order data before sending to API"""
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
        else:
            for i, product in enumerate(order_data["order_products"]):
                if not product.get("item_code"):
                    errors.append(f"Product {i + 1}: Missing item code")
                if not product.get("item_name"):
                    errors.append(f"Product {i + 1}: Missing item name")

        if order_data.get("order_final_total_value", 0) <= 0:
            errors.append("Order total must be greater than 0")

        return errors


class APIManager:
    """API communication management"""

    @staticmethod
    def send_order(url: str, order_data: Dict[str, Any]) -> Dict[str, Any]:
        """Send order data to API endpoint"""
        try:
            headers = {"Content-Type": "application/json"}
            response = requests.post(url, json=order_data, headers=headers, timeout=30)

            return {
                "status_code": response.status_code,
                "response_text": response.text,
                "url_sent": url,
                "success": response.status_code == 200,
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


# Initialize session data before each request
@app.before_request
def before_request():
    OrderManager.initialize_session_data()


@app.route("/")
def index():
    """Main application page"""
    payment_summary = OrderManager.calculate_payment_summary()

    return render_template(
        "base.html",
        api_urls=API_URLS,
        cancel_api_urls=CANCEL_API_URLS,
        payment_methods=PAYMENT_METHODS,
        payment_statuses=PAYMENT_STATUSES,
        payment_options=PAYMENT_OPTIONS,
        data=session.get("order_data", get_default_data()),
        products=session.get("products", []),
        payments=session.get("payments", []),
        selected_endpoint=session.get("api_endpoint", DEFAULT_API_ENDPOINT),
        payment_summary=payment_summary,
    )


@app.route("/calculate-totals")
def calculate_totals():
    """Calculate order totals"""
    try:
        order_data = OrderManager.prepare_order_data()
        payment_summary = OrderManager.calculate_payment_summary()

        return jsonify(
            {
                "products_total": order_data["order_product_total_value"],
                "order_discount": order_data["order_total_discount"],
                "delivery_cost": order_data["order_delivery_cost"],
                "final_total": order_data["order_final_total_value"],
                "total_paid": payment_summary["total_paid"],
                "remaining_amount": payment_summary["remaining_amount"],
            }
        )
    except Exception as e:
        logger.error(f"Error calculating totals: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/calculate-payment-summary")
def calculate_payment_summary():
    """Calculate payment summary"""
    try:
        summary = OrderManager.calculate_payment_summary()
        return jsonify(summary)
    except Exception as e:
        logger.error(f"Error calculating payment summary: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/export-json")
def export_json():
    """Export order as JSON"""
    try:
        order_data = OrderManager.prepare_order_data()
        return jsonify(order_data)
    except Exception as e:
        logger.error(f"Error exporting JSON: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/load-default")
def load_default():
    """Load default order data"""
    try:
        default_data = get_default_data()
        session.update(
            {
                "order_data": default_data,
                "products": default_data["order_products"].copy(),
                "payments": default_data["payment_methods_with_options"].copy(),
            }
        )
        session.modified = True

        flash("Default data loaded successfully!", "success")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error loading default data: {str(e)}")
        flash(f"Error loading default data: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/clear-all")
def clear_all():
    """Clear all order data"""
    try:
        default_data = get_default_data()
        session.update(
            {
                "order_data": {
                    **default_data,
                    **{
                        "branch_code": "",
                        "order_code": "",
                        "parent_order_code": "",
                        "client_first_name": "",
                        "client_middle_name": "",
                        "client_last_name": "",
                        "client_phone": "",
                        "client_email": "",
                        "order_address": "",
                    },
                },
                "products": [],
                "payments": [],
            }
        )
        session.modified = True

        flash("All data cleared successfully!", "success")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error clearing data: {str(e)}")
        flash(f"Error clearing data: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/get-item-details", methods=["GET"])
def get_item_details():
    """Get item details from database"""
    try:
        material_number = request.args.get("material_number", "").strip()
        customer_number = request.args.get("customer_number", "").strip()
        sap_tax_code = request.args.get("sap_tax_code", "").strip()
        sap_mat_generic = request.args.get("sap_mat_generic", "").strip()

        filters = {}
        if customer_number:
            filters["customer_number"] = customer_number
        if sap_tax_code:
            filters["sap_tax_code"] = sap_tax_code
        if sap_mat_generic:
            filters["sap_mat_generic"] = sap_mat_generic

        item_details = DatabaseManager.lookup_item(material_number, **filters)
        return jsonify(item_details)

    except ValidationError as e:
        return jsonify({"error": str(e)}), 400
    except ConnectionError as e:
        return jsonify({"error": str(e)}), 500
    except ValueError as e:
        return jsonify({"error": str(e)}), 404
    except Exception as e:
        logger.error(f"Database error: {str(e)}")
        return jsonify({"error": f"Database error: {str(e)}"}), 500


@app.route("/add-product", methods=["POST"])
def add_product():
    """Add product to order"""
    try:
        quantity = float(request.form.get("quantity", 0))
        unit_price = float(request.form.get("unit_price", 0))
        vat_input = request.form.get("vat_percentage", "0")
        discount = float(request.form.get("discount", 0))

        calculations = ProductCalculator.calculate_product_totals(
            quantity, unit_price, vat_input, discount
        )

        product = {
            "item_code": request.form.get("item_code", "").strip(),
            "item_name": request.form.get("item_name", "").strip(),
            "quantity": quantity,
            "unit_price": unit_price,
            "vat_percentage": calculations["vat_percentage"],
            "row_total_discount": discount,
            "total_vat_amount": calculations["vat_amount"],
            "row_net_total": calculations["net_total"],
            "unit_vat_amount": calculations["unit_vat"],
            "offer_code": request.form.get("offer_code", "").strip(),
            "offer_message": request.form.get("offer_message", "").strip(),
        }

        products = session.get("products", [])
        products.append(product)
        session["products"] = products
        session.modified = True

        flash("Product added successfully!", "success")
        return redirect(url_for("index"))

    except Exception as e:
        logger.error(f"Error adding product: {str(e)}")
        flash(f"Error adding product: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/add-product-from-db", methods=["POST"])
def add_product_from_db():
    """Add product from database lookup"""
    try:
        data = request.get_json()
        if not data:
            return jsonify({"success": False, "error": "No data provided"}), 400

        # Use the product calculator for consistent calculations
        calculations = ProductCalculator.calculate_product_totals(
            1, data["unit_price"], data["vat_percentage"], 0
        )

        product = {
            "item_code": data["item_code"],
            "item_name": data["item_EN_Name"],
            "quantity": 1.0,
            "unit_price": float(data["unit_price"]),
            "vat_percentage": calculations["vat_percentage"],
            "row_total_discount": 0.0,
            "total_vat_amount": calculations["vat_amount"],
            "row_net_total": calculations["net_total"],
            "unit_vat_amount": calculations["unit_vat"],
            "offer_code": "",
            "offer_message": "",
        }

        products = session.get("products", [])
        products.append(product)
        session["products"] = products
        session.modified = True

        return jsonify({"success": True, "message": "Product added successfully!"})

    except Exception as e:
        logger.error(f"Error adding product from DB: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/remove-product/<int:index>")
def remove_product(index):
    """Remove product from order"""
    try:
        products = session.get("products", [])
        if 0 <= index < len(products):
            removed_product = products.pop(index)
            session["products"] = products
            session.modified = True
            flash(
                f"Product '{removed_product.get('item_name', 'Unknown')}' removed successfully!",
                "success",
            )
        else:
            flash("Invalid product index", "danger")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error removing product: {str(e)}")
        flash(f"Error removing product: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/get-product/<int:index>")
def get_product(index):
    """Get product details for editing"""
    try:
        products = session.get("products", [])
        if 0 <= index < len(products):
            product = products[index].copy()
            product["index"] = index
            return jsonify(product)
        else:
            return jsonify({"error": "Invalid product index"}), 404
    except Exception as e:
        logger.error(f"Error getting product: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/update-product/<int:index>", methods=["POST"])
def update_product(index):
    """Update existing product"""
    try:
        products = session.get("products", [])
        if 0 <= index < len(products):
            quantity = float(request.form.get("quantity", 0))
            unit_price = float(request.form.get("unit_price", 0))
            vat_input = request.form.get("vat_percentage", "0")
            discount = float(request.form.get("discount", 0))

            calculations = ProductCalculator.calculate_product_totals(
                quantity, unit_price, vat_input, discount
            )

            products[index] = {
                "item_code": request.form.get("item_code", "").strip(),
                "item_name": request.form.get("item_name", "").strip(),
                "quantity": quantity,
                "unit_price": unit_price,
                "vat_percentage": calculations["vat_percentage"],
                "row_total_discount": discount,
                "total_vat_amount": calculations["vat_amount"],
                "row_net_total": calculations["net_total"],
                "unit_vat_amount": calculations["unit_vat"],
                "offer_code": request.form.get("offer_code", "").strip(),
                "offer_message": request.form.get("offer_message", "").strip(),
            }

            session["products"] = products
            session.modified = True
            flash("Product updated successfully!", "success")
        else:
            flash("Invalid product index", "danger")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error updating product: {str(e)}")
        flash(f"Error updating product: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/add-payment", methods=["POST"])
def add_payment():
    """Add payment method to order"""
    try:
        payment_amount = float(request.form.get("payment_amount", 0))

        payment = {
            "payment_method": request.form.get("payment_method", "").strip(),
            "payment_status": request.form.get("payment_status", "").strip(),
            "payment_amount": payment_amount,
            "transaction_id": request.form.get("transaction_id", "").strip(),
            "payment_option": request.form.get("payment_option", "").strip(),
            "option_commission": float(request.form.get("option_commission", 0)),
            "card_name": request.form.get("card_name", "").strip(),
            "bank_code": request.form.get("bank_code", "").strip(),
            "credit_customer_info": None,
        }

        # Handle credit customer info for PostToCredit
        if payment["payment_method"] == "PostToCredit":
            customer_number = request.form.get("customer_number", "").strip()
            customer_name = request.form.get("customer_name", "").strip()
            if customer_number or customer_name:
                payment["credit_customer_info"] = {
                    "customer_number": customer_number,
                    "customer_name": customer_name,
                }

        payments = session.get("payments", [])
        payments.append(payment)
        session["payments"] = payments
        session.modified = True

        flash("Payment method added successfully!", "success")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error adding payment: {str(e)}")
        flash(f"Error adding payment: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/remove-payment/<int:index>")
def remove_payment(index):
    """Remove payment method from order"""
    try:
        payments = session.get("payments", [])
        if 0 <= index < len(payments):
            removed_payment = payments.pop(index)
            session["payments"] = payments
            session.modified = True
            flash(
                f"Payment method '{removed_payment.get('payment_method', 'Unknown')}' removed successfully!",
                "success",
            )
        else:
            flash("Invalid payment index", "danger")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error removing payment: {str(e)}")
        flash(f"Error removing payment: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/get-payment/<int:index>")
def get_payment(index):
    """Get payment details for editing"""
    try:
        payments = session.get("payments", [])
        if 0 <= index < len(payments):
            payment = payments[index].copy()
            payment["index"] = index
            return jsonify(payment)
        else:
            return jsonify({"error": "Invalid payment index"}), 404
    except Exception as e:
        logger.error(f"Error getting payment: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/update-payment/<int:index>", methods=["POST"])
def update_payment(index):
    """Update existing payment method"""
    try:
        payments = session.get("payments", [])
        if 0 <= index < len(payments):
            payment = {
                "payment_method": request.form.get("payment_method", "").strip(),
                "payment_status": request.form.get("payment_status", "").strip(),
                "payment_amount": float(request.form.get("payment_amount", 0)),
                "transaction_id": request.form.get("transaction_id", "").strip(),
                "payment_option": request.form.get("payment_option", "").strip(),
                "option_commission": float(request.form.get("option_commission", 0)),
                "card_name": request.form.get("card_name", "").strip(),
                "bank_code": request.form.get("bank_code", "").strip(),
                "credit_customer_info": None,
            }

            # Handle credit customer info for PostToCredit
            if payment["payment_method"] == "PostToCredit":
                customer_number = request.form.get("customer_number", "").strip()
                customer_name = request.form.get("customer_name", "").strip()
                if customer_number or customer_name:
                    payment["credit_customer_info"] = {
                        "customer_number": customer_number,
                        "customer_name": customer_name,
                    }

            payments[index] = payment
            session["payments"] = payments
            session.modified = True
            flash("Payment method updated successfully!", "success")
        else:
            flash("Invalid payment index", "danger")
        return redirect(url_for("index"))
    except Exception as e:
        logger.error(f"Error updating payment: {str(e)}")
        flash(f"Error updating payment: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/update-order-field", methods=["POST"])
def update_order_field():
    """Update individual order field via AJAX"""
    try:
        field = request.json.get('field')
        value = request.json.get('value')

        if not field:
            return jsonify({"success": False, "error": "No field specified"}), 400

        order_data = session.get("order_data", get_default_data())

        # Map field names to session keys
        field_mapping = {
            'branch_code': 'branch_code',
            'order_code': 'order_code',
            'parent_order_code': 'parent_order_code',
            'delivery_cost': 'order_delivery_cost',
            'is_delivery': 'is_delivery',
            'order_status': 'order_status',
            'delivery_date': 'delivery_date',
            'fulfillment_plant': 'fullfilment_plant',
            'shipping_address_2': 'shipping_address_2',
            'order_notes': 'order_notes',
            'first_name': 'client_first_name',
            'middle_name': 'client_middle_name',
            'last_name': 'client_last_name',
            'phone': 'client_phone',
            'email': 'client_email',
            'birthdate': 'client_birthdate',
            'gender': 'client_gender',
            'country_code': 'client_country_code',
            'address': 'order_address',
            'delivery_from_time': 'delivery_from_time',
            'delivery_to_time': 'delivery_to_time',
            'order_payment_status': 'order_payment_status'
        }

        if field in field_mapping:
            session_key = field_mapping[field]

            # Handle data type conversions
            if field in ['delivery_cost', 'is_delivery']:
                try:
                    value = float(value) if field == 'delivery_cost' else int(value)
                except (ValueError, TypeError):
                    value = 0 if field == 'is_delivery' else 0.0

            order_data[session_key] = value
            session["order_data"] = order_data
            session.modified = True

            return jsonify({"success": True, "message": f"Field {field} updated"})
        else:
            return jsonify({"success": False, "error": f"Unknown field: {field}"}), 400

    except Exception as e:
        logger.error(f"Error updating order field: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/get-remaining-amount")
def get_remaining_amount():
    """Get remaining amount for payment"""
    try:
        summary = OrderManager.calculate_payment_summary()
        return jsonify({"remaining_amount": summary["remaining_amount"]})
    except Exception as e:
        logger.error(f"Error getting remaining amount: {str(e)}")
        return jsonify({"remaining_amount": 0.0})


@app.route("/send-request", methods=["POST"])
def send_request():
    """Send order to API endpoint"""
    try:
        # Get selected endpoint or custom URL
        api_endpoint = request.form.get("api_endpoint", "").strip()
        custom_url = request.form.get("custom_url", "").strip()

        # Determine which URL to use
        url = custom_url if custom_url else API_URLS.get(api_endpoint)

        if not url:
            flash("Please select an API endpoint or enter a custom URL", "danger")
            return redirect(url_for("index"))

        # Prepare order data
        order_data = OrderManager.prepare_order_data()

        # Validate if checkbox is checked
        if request.form.get("validateBeforeSend"):
            errors = OrderManager.validate_order_data(order_data)
            if errors:
                for error in errors:
                    flash(error, "danger")
                return redirect(url_for("index"))

        # Send to API
        response = APIManager.send_order(url, order_data)

        # Store endpoint selection
        if api_endpoint:
            session["api_endpoint"] = api_endpoint
            session.modified = True

        if response["success"]:
            flash(
                f"Order sent successfully! Status: {response['status_code']}", "success"
            )
        else:
            flash(
                f"Order sent but received status code: {response['status_code']}",
                "warning",
            )

        return render_template(
            "base.html",
            api_urls=API_URLS,
            cancel_api_urls=CANCEL_API_URLS,
            payment_methods=PAYMENT_METHODS,
            payment_statuses=PAYMENT_STATUSES,
            payment_options=PAYMENT_OPTIONS,
            data=session.get("order_data", get_default_data()),
            products=session.get("products", []),
            payments=session.get("payments", []),
            selected_endpoint=session.get("api_endpoint", DEFAULT_API_ENDPOINT),
            response=response,
        )

    except Exception as e:
        logger.error(f"Error sending request: {str(e)}")
        flash(f"Error sending request: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/cancel-order", methods=["POST"])
def cancel_order():
    """Cancel an existing order"""
    try:
        # Get selected endpoint or custom URL
        cancel_api_endpoint = request.form.get("cancel_api_endpoint", "").strip()
        cancel_custom_url = request.form.get("cancel_custom_url", "").strip()

        # Determine which URL to use
        url = (
            cancel_custom_url
            if cancel_custom_url
            else CANCEL_API_URLS.get(cancel_api_endpoint)
        )

        if not url:
            flash("Please select a cancel API endpoint or enter a custom URL", "danger")
            return redirect(url_for("index"))

        # Get order details
        order_number = request.form.get("order_number", "").strip()
        reason = request.form.get("reason", "").strip()

        if not order_number or not reason:
            flash("Order number and reason are required", "danger")
            return redirect(url_for("index"))

        # Prepare cancel data
        cancel_data = {
            "order_number": order_number,
            "reason": reason,
        }

        # Send to API
        response = APIManager.send_order(url, cancel_data)

        if response["success"]:
            flash(
                f"Order cancellation request sent successfully! Status: {response['status_code']}",
                "success",
            )
        else:
            flash(
                f"Cancellation request sent but received status code: {response['status_code']}",
                "warning",
            )

        return render_template(
            "base.html",
            api_urls=API_URLS,
            cancel_api_urls=CANCEL_API_URLS,
            payment_methods=PAYMENT_METHODS,
            payment_statuses=PAYMENT_STATUSES,
            payment_options=PAYMENT_OPTIONS,
            data=session.get("order_data", get_default_data()),
            products=session.get("products", []),
            payments=session.get("payments", []),
            selected_endpoint=session.get("api_endpoint", DEFAULT_API_ENDPOINT),
            cancel_response=response,
        )

    except Exception as e:
        logger.error(f"Error canceling order: {str(e)}")
        flash(f"Error canceling order: {str(e)}", "danger")
        return redirect(url_for("index"))


@app.route("/test-database-connection", methods=["POST"])
def test_database_connection():
    """Test database connection with provided credentials"""
    try:
        data = request.get_json()
        if not data:
            return jsonify({"success": False, "message": "No data provided"}), 400

        # Try to connect with provided credentials
        server = data.get("server", ".")
        database = data.get("database", "RMSCashierSrv")
        username = data.get("username", "sa")
        password = data.get("password", "")

        drivers = [
            "ODBC Driver 18 for SQL Server",
            "ODBC Driver 17 for SQL Server",
            "ODBC Driver 13 for SQL Server",
            "SQL Server Native Client 11.0",
            "SQL Server",
        ]

        for driver in drivers:
            try:
                conn_str = (
                    f"DRIVER={{{driver}}};"
                    f"SERVER={server};"
                    f"DATABASE={database};"
                    f"UID={username};"
                    f"PWD={password};"
                    f"Trusted_Connection=no;"
                    f"Encrypt=no;"
                )
                conn = pyodbc.connect(conn_str)
                conn.close()
                return jsonify(
                    {
                        "success": True,
                        "message": f"Connected successfully using {driver}",
                    }
                )
            except pyodbc.Error:
                continue

        return (
            jsonify(
                {
                    "success": False,
                    "message": "Could not connect with any available driver. Please check your credentials.",
                }
            ),
            500,
        )

    except Exception as e:
        logger.error(f"Error testing database connection: {str(e)}")
        return jsonify({"success": False, "message": str(e)}), 500


@app.route("/test-endpoint", methods=["POST"])
def test_endpoint():
    """Test API endpoint connectivity"""
    try:
        data = request.get_json()
        if not data or "url" not in data:
            return jsonify({"status": "Error", "error": "No URL provided"}), 400

        url = data["url"]
        result = APIManager.test_endpoint(url)
        return jsonify(result)

    except Exception as e:
        logger.error(f"Error testing endpoint: {str(e)}")
        return jsonify({"status": "Error", "error": str(e)}), 500


@app.context_processor
def inject_global_variables():
    return dict(
        api_urls=API_URLS,
        cancel_api_urls=CANCEL_API_URLS,
        payment_methods=PAYMENT_METHODS,
        payment_statuses=PAYMENT_STATUSES,
        payment_options=PAYMENT_OPTIONS,
    )


@app.context_processor
def inject_session_data():
    payment_summary = OrderManager.calculate_payment_summary()
    return dict(
        data=session.get("order_data", get_default_data()),
        products=session.get("products", []),
        payments=session.get("payments", []),
        payment_summary=payment_summary,
    )


if __name__ == "__main__":
    app.run(debug=True, port=5002)