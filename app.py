import logging
import json
import pyodbc
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
    AppConfig,
)

# Import managers
from managers import (
    DatabaseManager,
    ProductCalculator,
    OrderManager,
    APIManager,
    ValidationError,
)

load_dotenv()

app = Flask(__name__)
app.config.from_object(AppConfig)

# Setup logging
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


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
        payment_summary = OrderManager.calculate_payment_summary()

        return jsonify(
            {
                "products_total": payment_summary["products_total"],
                "order_discount": payment_summary["order_discount"],
                "delivery_cost": payment_summary["delivery_cost"],
                "final_total": payment_summary["order_final_total"],
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
        field = request.json.get("field")
        value = request.json.get("value")

        if not field:
            return jsonify({"success": False, "error": "No field specified"}), 400

        order_data = session.get("order_data", get_default_data())

        # Field mapping for session data
        field_mapping = {
            "branch_code": "branch_code",
            "order_code": "order_code",
            "parent_order_code": "parent_order_code",
            "delivery_cost": "order_delivery_cost",
            "is_delivery": "is_delivery",
            "order_status": "order_status",
            "delivery_date": "delivery_date",
            "fulfillment_plant": "fullfilment_plant",
            "shipping_address_2": "shipping_address_2",
            "order_notes": "order_notes",
            "first_name": "client_first_name",
            "middle_name": "client_middle_name",
            "last_name": "client_last_name",
            "phone": "client_phone",
            "email": "client_email",
            "birthdate": "client_birthdate",
            "gender": "client_gender",
            "country_code": "client_country_code",
            "address": "order_address",
            "delivery_from_time": "delivery_from_time",
            "delivery_to_time": "delivery_to_time",
            "order_payment_status": "order_payment_status",
        }

        if field in field_mapping:
            session_key = field_mapping[field]

            # Handle data type conversions
            if field in ["delivery_cost", "is_delivery"]:
                try:
                    value = float(value) if field == "delivery_cost" else int(value)
                except (ValueError, TypeError):
                    value = 0 if field == "is_delivery" else 0.0

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
            if request.headers.get("X-Requested-With") == "XMLHttpRequest":
                return (
                    jsonify(
                        {
                            "success": False,
                            "error": "Please select an API endpoint or enter a custom URL",
                        }
                    ),
                    400,
                )
            flash("Please select an API endpoint or enter a custom URL", "danger")
            return redirect(url_for("index"))

        # Prepare order data
        order_data = OrderManager.prepare_order_data()

        # Validate if checkbox is checked
        validate_flag = request.form.get("validateBeforeSend")
        should_validate = (
            validate_flag == "on"
            or validate_flag == "true"
            or validate_flag == "1"
            or validate_flag is True
        )

        if should_validate:
            errors = OrderManager.validate_order_data(order_data)
            if errors:
                if request.headers.get("X-Requested-With") == "XMLHttpRequest":
                    return (
                        jsonify(
                            {
                                "status_code": 400,
                                "response_text": json.dumps(
                                    {"validation_errors": errors}, indent=2
                                ),
                                "url_sent": "Validation Check",
                                "success": False,
                            }
                        ),
                        400,
                    )

                for error in errors:
                    flash(error, "danger")
                return redirect(url_for("index"))

        # Send to API
        response = APIManager.send_order(url, order_data)

        # Store endpoint selection
        if api_endpoint:
            session["api_endpoint"] = api_endpoint
            session.modified = True

        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify(response)

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
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
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
            if request.headers.get("X-Requested-With") == "XMLHttpRequest":
                return (
                    jsonify(
                        {
                            "success": False,
                            "error": "Please select a cancel API endpoint or enter a custom URL",
                        }
                    ),
                    400,
                )
            flash("Please select a cancel API endpoint or enter a custom URL", "danger")
            return redirect(url_for("index"))

        # Get order details
        order_number = request.form.get("order_number", "").strip()
        reason = request.form.get("reason", "").strip()

        if not order_number or not reason:
            if request.headers.get("X-Requested-With") == "XMLHttpRequest":
                return (
                    jsonify(
                        {
                            "success": False,
                            "error": "Order number and reason are required",
                        }
                    ),
                    400,
                )
            flash("Order number and reason are required", "danger")
            return redirect(url_for("index"))

        # Prepare cancel data
        cancel_data = {
            "order_number": order_number,
            "reason": reason,
        }

        # Send to API
        response = APIManager.send_order(url, cancel_data)

        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify(response)

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
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
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

        # Optimization: Try using the cached driver if it exists and wasn't explicitly overridden by request
        # Note: Since the request provides credentials but not driver, we iterate through drivers.
        # However, we can prioritize the driver that worked previously if we had access to it easily.
        # But DatabaseManager._cached_driver is internal.
        # Let's just stick to the original logic for this specific route as it is a "Test" route for custom credentials.

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
