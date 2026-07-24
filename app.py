import logging
import json
import os
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
    abort,
)
from dotenv import load_dotenv

# Import configuration
from config import (
    LEGACY_ENDPOINT_ALIASES,
    PAYMENT_METHODS,
    PAYMENT_STATUSES,
    PAYMENT_OPTIONS,
    AppConfig,
)

# Import managers
from managers import OrderManager, APIManager

# Import the per-client module registry
from modules import DEFAULT_MODULE_KEY, ENV_TO_MODULE, MODULE_REGISTRY
from modules import flat_order
from modules.base import ValidationError as ModuleValidationError
from modules.db_config import DB_CONFIGS

load_dotenv()

app = Flask(__name__, template_folder=".", static_folder=".", static_url_path="/static")
app.config.from_object(AppConfig)

# Setup logging
logging.basicConfig(
    level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Modules whose draft shape is {order_data, products, payments} (ghc_ecommerce/
# upc_ecommerce share modules/flat_order.py). Product/payment CRUD routes only
# make sense for these; ghc_unicommerce's row-item/consumer/delivery shape gets
# its own dedicated UI + routes in a later session.
FLAT_ORDER_MODULE_KEYS = {"ghc_ecommerce", "upc_ecommerce"}


def normalize_endpoint_name(endpoint_name):
    """Map legacy environment labels (e.g. old CLIENT_ENDPOINTS keys) to current ones."""
    if not endpoint_name:
        return None
    return LEGACY_ENDPOINT_ALIASES.get(endpoint_name, endpoint_name)


def get_active_module_key() -> str:
    key = session.get("active_module") or DEFAULT_MODULE_KEY
    if key not in MODULE_REGISTRY:
        key = DEFAULT_MODULE_KEY
    return key


def get_module_or_404(module_key: str):
    module = MODULE_REGISTRY.get(module_key)
    if module is None:
        abort(404, description=f"Unknown module '{module_key}'")
    return module


def get_active_environment(module):
    """Resolve the active ModuleEnvironment for a module from the session,
    falling back to the module's default environment."""
    env_key = session.get("active_environment")
    return module.get_environment(env_key)


def get_module_state(module_key: str) -> dict:
    """Return (lazily initializing) the session draft for a module."""
    module = MODULE_REGISTRY[module_key]
    modules_state = session.get("modules", {})
    if module_key not in modules_state:
        draft = OrderManager.load_module_draft(module_key)
        modules_state[module_key] = draft if draft else module.default_state()
        session["modules"] = modules_state
        session.modified = True
    return session["modules"][module_key]


def set_module_state(module_key: str, state: dict):
    """Persist a module's draft back into the session and its autosave file."""
    modules_state = session.get("modules", {})
    modules_state[module_key] = state
    session["modules"] = modules_state
    session.modified = True
    OrderManager.save_module_draft(module_key, state)


def require_flat_order_module(module_key: str):
    if module_key not in FLAT_ORDER_MODULE_KEYS:
        abort(
            400, description=f"This action is not supported by module '{module_key}'."
        )


# Initialize session data before each request
@app.before_request
def before_request():
    OrderManager.initialize_session_data()
    normalized = normalize_endpoint_name(session.get("active_environment"))
    if normalized and normalized != session.get("active_environment"):
        session["active_environment"] = normalized
        session.modified = True


@app.route("/")
def index():
    """Landing page: module/environment picker."""
    return render_template("landing.html", modules=MODULE_REGISTRY.values())


@app.route("/docs")
def docs():
    """Render the project README as HTML for the in-app Docs viewer."""
    import re

    readme_path = os.path.join(os.path.dirname(__file__), "README.md")
    try:
        with open(readme_path, "r", encoding="utf-8") as f:
            md = f.read()
    except OSError:
        return jsonify({"error": "README not found"}), 404

    def escape(s):
        return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

    lines = md.splitlines()
    html_parts = []
    in_code = False
    code_lang = ""
    code_buffer = []
    in_table = False
    table_rows = []

    def flush_table():
        nonlocal in_table, table_rows
        if not table_rows:
            in_table = False
            return
        header = table_rows[0]
        body = table_rows[1:] if len(table_rows) > 2 else table_rows[1:]
        out = [
            '<div class="table-responsive"><table class="table table-sm table-bordered">'
        ]
        out.append("<thead><tr>")
        for cell in header:
            out.append(f"<th>{escape(cell.strip())}</th>")
        out.append("</tr></thead><tbody>")
        for row in body:
            out.append("<tr>")
            for cell in row:
                out.append(f"<td>{escape(cell.strip())}</td>")
            out.append("</tr>")
        out.append("</tbody></table></div>")
        html_parts.append("".join(out))
        table_rows = []
        in_table = False

    for line in lines:
        # Code fence
        if line.strip().startswith("```"):
            if in_code:
                html_parts.append(
                    f'<pre class="docs-code"><code>{escape(chr(10).join(code_buffer))}</code></pre>'
                )
                in_code = False
                code_buffer = []
            else:
                if in_table:
                    flush_table()
                in_code = True
                code_lang = line.strip()[3:]
                code_buffer = []
            continue
        if in_code:
            code_buffer.append(line)
            continue

        # Table row
        if line.strip().startswith("|") and line.strip().endswith("|"):
            cells = [c for c in line.strip().strip("|").split("|")]
            # Skip separator rows like |---|---|
            if all(re.match(r"^[-:\s]+$", c) for c in cells):
                in_table = True
                continue
            if not in_table and table_rows:
                table_rows = []
            in_table = True
            table_rows.append(cells)
            continue
        else:
            if in_table:
                flush_table()

        # Headings
        m = re.match(r"^(#{1,6})\s+(.*)$", line)
        if m:
            level = len(m.group(1))
            html_parts.append(f"<h{level}>{escape(m.group(2).strip())}</h{level}>")
            continue

        # Horizontal rule
        if re.match(r"^(-{3,}|\*{3,}|_{3,})$", line.strip()):
            html_parts.append("<hr>")
            continue

        # Blockquote
        if line.strip().startswith(">"):
            html_parts.append(
                f'<blockquote class="blockquote">{escape(line.strip()[1:].strip())}</blockquote>'
            )
            continue

        # Unordered list
        if re.match(r"^\s*[-*]\s+", line):
            item = re.sub(r"^\s*[-*]\s+", "", line)
            html_parts.append(f"<ul><li>{inline(escape(item))}</li></ul>")
            continue

        # Ordered list
        if re.match(r"^\s*\d+\.\s+", line):
            item = re.sub(r"^\s*\d+\.\s+", "", line)
            html_parts.append(f"<ol><li>{inline(escape(item))}</li></ol>")
            continue

        # Blank line
        if not line.strip():
            html_parts.append("")
            continue

        # Paragraph
        html_parts.append(f"<p>{inline(escape(line))}</p>")

    if in_code:
        html_parts.append(
            f'<pre class="docs-code"><code>{escape(chr(10).join(code_buffer))}</code></pre>'
        )
    if in_table:
        flush_table()

    def inline(s):
        # Inline code
        s = re.sub(r"`([^`]+)`", r'<code class="docs-inline-code">\1</code>', s)
        # Bold
        s = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", s)
        # Italic
        s = re.sub(r"\*([^*]+)\*", r"<em>\1</em>", s)
        return s

    rendered = "\n".join(html_parts)
    return jsonify({"html": rendered})


@app.route("/select-client", methods=["POST"])
def select_client():
    """Persist the selected module/environment from the landing page."""
    payload = request.get_json(silent=True) or request.form
    env_key = normalize_endpoint_name(
        (payload.get("client") or payload.get("api_endpoint") or "").strip()
    )
    module_key = ENV_TO_MODULE.get(env_key) if env_key else None
    module = MODULE_REGISTRY.get(module_key) if module_key else None
    environment = module.environments.get(env_key) if module else None

    if not module or not environment:
        return jsonify({"success": False, "error": "Unknown client selection"}), 400

    if not environment.available or not environment.api_url:
        return (
            jsonify(
                {
                    "success": False,
                    "error": "This client environment is not available yet.",
                }
            ),
            409,
        )

    session["active_module"] = module_key
    session["active_environment"] = env_key
    session["client_selected"] = True
    session.modified = True
    get_module_state(module_key)  # lazily seed the draft if this is the first visit

    return jsonify(
        {
            "success": True,
            "selected_client": env_key,
            "selected_module": module_key,
            "api_url": environment.api_url,
            "cancel_url": environment.cancel_url,
        }
    )


@app.route("/modules/<module_key>/")
def module_home(module_key):
    """Module order-builder view. Flat-order modules (ghc/upc) get the shared
    flat_order.html view; ghc_unicommerce keeps its placeholder until session 7."""
    module = get_module_or_404(module_key)

    if not module.available:
        flash(f"{module.label} is coming soon.", "warning")
        return redirect(url_for("index"))

    session["active_module"] = module_key
    current_env = session.get("active_environment")
    if not current_env or ENV_TO_MODULE.get(current_env) != module_key:
        session["active_environment"] = module.get_environment().key
    session.modified = True

    state = get_module_state(module_key)

    if module_key in FLAT_ORDER_MODULE_KEYS:
        environment = get_active_environment(module)
        # env key -> api_url / cancel_url for this module's environments only
        api_urls = {
            env.key: env.api_url for env in module.environments.values() if env.api_url
        }
        cancel_api_urls = {
            env.key: env.cancel_url
            for env in module.environments.values()
            if env.cancel_url
        }
        db_config = DB_CONFIGS.get(module_key, {})
        return render_template(
            "flat_order.html",
            module=module,
            state=state,
            data=state["order_data"],
            products=state["products"],
            payments=state["payments"],
            api_urls=api_urls,
            cancel_api_urls=cancel_api_urls,
            selected_endpoint=environment.key,
            db_config=db_config,
            response=None,
            cancel_response=None,
        )

    if module_key == "ghc_unicommerce":
        environment = get_active_environment(module)
        api_urls = {
            env.key: env.api_url for env in module.environments.values() if env.api_url
        }
        cancel_api_urls = {
            env.key: env.cancel_url
            for env in module.environments.values()
            if env.cancel_url
        }
        db_config = DB_CONFIGS.get(module_key, {})
        # Computed totals for the read-only summary fields
        payload = module.build_payload(state)
        totals = {
            "gross_amount": payload["GrossAmount"],
            "total_discount": payload["TotalDiscount"],
            "total_vat": payload["TotalVat"],
            "net_amount": payload["NetAmount"],
            "customer_credit_amount": payload["CustomerCreditAmount"],
        }
        return render_template(
            "unicommerce.html",
            module=module,
            state=state,
            consumer=state.get("consumer", {}),
            delivery=state.get("delivery", {}),
            row_items=state.get("row_items", []),
            totals=totals,
            api_urls=api_urls,
            cancel_api_urls=cancel_api_urls,
            selected_endpoint=environment.key,
            db_config=db_config,
            response=None,
            cancel_response=None,
        )

    return render_template("module_placeholder.html", module=module, state=state)


@app.route("/modules/<module_key>/get-item-details", methods=["GET"])
def module_get_item_details(module_key):
    """Get item details from this module's own database."""
    module = get_module_or_404(module_key)
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

        item_details = module.lookup_item(material_number, **filters)
        return jsonify(item_details)

    except ModuleValidationError as e:
        return jsonify({"error": str(e)}), 400
    except ConnectionError as e:
        return jsonify({"error": str(e)}), 500
    except ValueError as e:
        return jsonify({"error": str(e)}), 404
    except Exception as e:
        logger.error(f"Database error: {str(e)}")
        return jsonify({"error": f"Database error: {str(e)}"}), 500


@app.route("/modules/<module_key>/get-consumer-details", methods=["GET"])
def module_get_consumer_details(module_key):
    """Get an existing consumer/customer by phone number from this module's own database."""
    module = get_module_or_404(module_key)
    try:
        phone = request.args.get("phone", "").strip()
        consumer = module.lookup_consumer_by_phone(phone)
        if consumer is None:
            return jsonify({"found": False}), 404
        return jsonify({"found": True, "consumer": consumer})

    except ModuleValidationError as e:
        return jsonify({"error": str(e)}), 400
    except ConnectionError as e:
        return jsonify({"error": str(e)}), 500
    except Exception as e:
        logger.error(f"Database error: {str(e)}")
        return jsonify({"error": f"Database error: {str(e)}"}), 500


# --------------------------------------------------------------------------
# Flat-order (ghc_ecommerce / upc_ecommerce) product & payment CRUD
# --------------------------------------------------------------------------


@app.route("/modules/<module_key>/add-product", methods=["POST"])
def module_add_product(module_key):
    """Add product to the module's draft order."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        quantity = float(request.form.get("quantity", 0))
        unit_price = float(request.form.get("unit_price", 0))
        vat_input = float(request.form.get("vat_percentage", "0"))
        discount = float(request.form.get("discount", 0))

        calculations = flat_order.calculate_product_totals(
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

        state = get_module_state(module_key)
        state["products"].append(product)
        set_module_state(module_key, state)

        flash("Product added successfully!", "success")
        return redirect(url_for("module_home", module_key=module_key))

    except Exception as e:
        logger.error(f"Error adding product: {str(e)}")
        flash(f"Error adding product: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/add-product-from-db", methods=["POST"])
def module_add_product_from_db(module_key):
    """Add product from database lookup."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        data = request.get_json()
        if not data:
            return jsonify({"success": False, "error": "No data provided"}), 400

        calculations = flat_order.calculate_product_totals(
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

        state = get_module_state(module_key)
        state["products"].append(product)
        set_module_state(module_key, state)

        return jsonify({"success": True, "message": "Product added successfully!"})

    except Exception as e:
        logger.error(f"Error adding product from DB: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/modules/<module_key>/remove-product/<int:index>")
def module_remove_product(module_key, index):
    """Remove product from the module's draft order."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        products = state["products"]
        if 0 <= index < len(products):
            removed_product = products.pop(index)
            set_module_state(module_key, state)
            flash(
                f"Product '{removed_product.get('item_name', 'Unknown')}' removed successfully!",
                "success",
            )
        else:
            flash("Invalid product index", "danger")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error removing product: {str(e)}")
        flash(f"Error removing product: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/get-product/<int:index>")
def module_get_product(module_key, index):
    """Get product details for editing."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        products = get_module_state(module_key)["products"]
        if 0 <= index < len(products):
            product = products[index].copy()
            product["index"] = index
            return jsonify(product)
        return jsonify({"error": "Invalid product index"}), 404
    except Exception as e:
        logger.error(f"Error getting product: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/modules/<module_key>/update-product/<int:index>", methods=["POST"])
def module_update_product(module_key, index):
    """Update existing product."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        products = state["products"]
        if 0 <= index < len(products):
            quantity = float(request.form.get("quantity", 0))
            unit_price = float(request.form.get("unit_price", 0))
            vat_input = float(request.form.get("vat_percentage", "0"))
            discount = float(request.form.get("discount", 0))

            calculations = flat_order.calculate_product_totals(
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

            set_module_state(module_key, state)
            flash("Product updated successfully!", "success")
        else:
            flash("Invalid product index", "danger")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error updating product: {str(e)}")
        flash(f"Error updating product: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/add-payment", methods=["POST"])
def module_add_payment(module_key):
    """Add payment method to the module's draft order."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        payment_amount = float(request.form.get("payment_amount", 0))

        payment = {
            "payment_method": request.form.get("payment_method", "").strip(),
            "payment_status": request.form.get("payment_status", "").strip(),
            "payment_amount": payment_amount,
            "transaction_id": request.form.get("transaction_id", "").strip(),
            "payment_option": request.form.get("payment_option", "").strip(),
            "option_commission": float(request.form.get("option_commission", 0)),
            "credit_customer_info": None,
        }

        if payment["payment_method"] in ["COD", "Visa", "PostToCredit"]:
            customer_number = request.form.get("customer_number", "").strip()
            customer_name = request.form.get("customer_name", "").strip()
            if customer_number or customer_name:
                payment["credit_customer_info"] = {
                    "customer_number": customer_number,
                    "customer_name": customer_name,
                }

        state = get_module_state(module_key)
        state["payments"].append(payment)
        set_module_state(module_key, state)

        flash("Payment method added successfully!", "success")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error adding payment: {str(e)}")
        flash(f"Error adding payment: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/remove-payment/<int:index>")
def module_remove_payment(module_key, index):
    """Remove payment method from the module's draft order."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        payments = state["payments"]
        if 0 <= index < len(payments):
            removed_payment = payments.pop(index)
            set_module_state(module_key, state)
            flash(
                f"Payment method '{removed_payment.get('payment_method', 'Unknown')}' removed successfully!",
                "success",
            )
        else:
            flash("Invalid payment index", "danger")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error removing payment: {str(e)}")
        flash(f"Error removing payment: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/get-payment/<int:index>")
def module_get_payment(module_key, index):
    """Get payment details for editing."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        payments = get_module_state(module_key)["payments"]
        if 0 <= index < len(payments):
            payment = payments[index].copy()
            payment["index"] = index
            return jsonify(payment)
        return jsonify({"error": "Invalid payment index"}), 404
    except Exception as e:
        logger.error(f"Error getting payment: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/modules/<module_key>/update-payment/<int:index>", methods=["POST"])
def module_update_payment(module_key, index):
    """Update existing payment method."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        payments = state["payments"]
        if 0 <= index < len(payments):
            payment = {
                "payment_method": request.form.get("payment_method", "").strip(),
                "payment_status": request.form.get("payment_status", "").strip(),
                "payment_amount": float(request.form.get("payment_amount", 0)),
                "transaction_id": request.form.get("transaction_id", "").strip(),
                "payment_option": request.form.get("payment_option", "").strip(),
                "option_commission": float(request.form.get("option_commission", 0)),
                "credit_customer_info": None,
            }

            if payment["payment_method"] in ["COD", "Visa", "PostToCredit"]:
                customer_number = request.form.get("customer_number", "").strip()
                customer_name = request.form.get("customer_name", "").strip()
                if customer_number or customer_name:
                    payment["credit_customer_info"] = {
                        "customer_number": customer_number,
                        "customer_name": customer_name,
                    }

            payments[index] = payment
            set_module_state(module_key, state)
            flash("Payment method updated successfully!", "success")
        else:
            flash("Invalid payment index", "danger")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error updating payment: {str(e)}")
        flash(f"Error updating payment: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/update-order-field", methods=["POST"])
def module_update_order_field(module_key):
    """Update individual order field via AJAX."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        field = request.json.get("field")
        value = request.json.get("value")

        if not field:
            return jsonify({"success": False, "error": "No field specified"}), 400

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

        if field not in field_mapping:
            return jsonify({"success": False, "error": f"Unknown field: {field}"}), 400

        session_key = field_mapping[field]

        if field in ["delivery_cost", "is_delivery"]:
            try:
                value = float(value) if field == "delivery_cost" else int(value)
            except (ValueError, TypeError):
                value = 0 if field == "is_delivery" else 0.0

        state = get_module_state(module_key)
        state["order_data"][session_key] = value
        set_module_state(module_key, state)

        return jsonify({"success": True, "message": f"Field {field} updated"})

    except Exception as e:
        logger.error(f"Error updating order field: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/modules/<module_key>/calculate-totals")
def module_calculate_totals(module_key):
    """Calculate order totals."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        summary = flat_order.calculate_payment_summary(state)
        return jsonify(
            {
                "products_total": summary["products_total"],
                "order_discount": summary["order_discount"],
                "delivery_cost": summary["delivery_cost"],
                "final_total": summary["order_final_total"],
                "total_paid": summary["total_paid"],
                "remaining_amount": summary["remaining_amount"],
            }
        )
    except Exception as e:
        logger.error(f"Error calculating totals: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/modules/<module_key>/calculate-payment-summary")
def module_calculate_payment_summary(module_key):
    """Calculate payment summary."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        return jsonify(flat_order.calculate_payment_summary(state))
    except Exception as e:
        logger.error(f"Error calculating payment summary: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/modules/<module_key>/get-remaining-amount")
def module_get_remaining_amount(module_key):
    """Get remaining amount for payment."""
    get_module_or_404(module_key)
    require_flat_order_module(module_key)
    try:
        state = get_module_state(module_key)
        summary = flat_order.calculate_payment_summary(state)
        return jsonify({"remaining_amount": summary["remaining_amount"]})
    except Exception as e:
        logger.error(f"Error getting remaining amount: {str(e)}")
        return jsonify({"remaining_amount": 0.0})


# --------------------------------------------------------------------------
# GHC Uni-Commerce actions: order fields, consumer, delivery, row-items, totals
# --------------------------------------------------------------------------

UNICOMMERCE_MODULE_KEY = "ghc_unicommerce"


def require_unicommerce_module(module_key: str):
    if module_key != UNICOMMERCE_MODULE_KEY:
        abort(
            400, description=f"This action is not supported by module '{module_key}'."
        )


@app.route("/modules/<module_key>/update-invoice-field", methods=["POST"])
def module_update_invoice_field(module_key):
    """Update a top-level invoice field (reference_number, is_return, paid amounts, ...)."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        field = request.json.get("field")
        value = request.json.get("value")
        allowed = {
            "reference_number",
            "online_order_number",
            "is_return",
            "parent_reference_number",
            "order_creation_date",
            "customer_name",
            "paid_online_amount",
            "paid_with_points_amount",
        }
        if field not in allowed:
            return jsonify({"success": False, "error": f"Unknown field: {field}"}), 400

        if field in ("paid_online_amount", "paid_with_points_amount"):
            try:
                value = float(value or 0)
            except (ValueError, TypeError):
                value = 0.0
        elif field == "is_return":
            value = bool(value)

        state = get_module_state(module_key)
        state[field] = value
        set_module_state(module_key, state)
        return jsonify({"success": True})
    except Exception as e:
        logger.error(f"Error updating invoice field: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/modules/<module_key>/update-consumer-field", methods=["POST"])
def module_update_consumer_field(module_key):
    """Update one InvoiceConsumer field."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        field = request.json.get("field")
        value = request.json.get("value")
        allowed = {
            "first_name",
            "middle_name",
            "last_name",
            "consumer_code",
            "gender",
            "birth_date",
            "primary_phone_number",
            "email",
            "national_id",
            "nationality",
        }
        if field not in allowed:
            return jsonify({"success": False, "error": f"Unknown field: {field}"}), 400

        state = get_module_state(module_key)
        state.setdefault("consumer", {})[field] = value
        set_module_state(module_key, state)
        return jsonify({"success": True})
    except Exception as e:
        logger.error(f"Error updating consumer field: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/modules/<module_key>/update-delivery-field", methods=["POST"])
def module_update_delivery_field(module_key):
    """Update one DeliveryDetails field."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        field = request.json.get("field")
        value = request.json.get("value")
        allowed = {
            "delivery_phone_number",
            "delivery_address",
            "delivery_location_url",
            "delivery_notes",
            "delivery_fees",
        }
        if field not in allowed:
            return jsonify({"success": False, "error": f"Unknown field: {field}"}), 400

        if field == "delivery_fees":
            try:
                value = float(value or 0)
            except (ValueError, TypeError):
                value = 0.0

        state = get_module_state(module_key)
        state.setdefault("delivery", {})[field] = value
        set_module_state(module_key, state)
        return jsonify({"success": True})
    except Exception as e:
        logger.error(f"Error updating delivery field: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


def _clean_row_item(form):
    """Normalize a row-item from form data into the draft shape."""

    def fnum(name, default=0.0):
        try:
            return float(form.get(name, default) or default)
        except (ValueError, TypeError):
            return default

    return {
        "quantity": fnum("quantity", 1.0),
        "material_number": form.get("material_number", "").strip(),
        "item_price": fnum("item_price"),
        "item_discount": fnum("item_discount"),
        "vat_percentage": fnum("vat_percentage"),
        "barcode": form.get("barcode", "").strip(),
        "batch_number": form.get("batch_number", "").strip(),
        "expire_date": form.get("expire_date", "").strip(),
        "serial_number": form.get("serial_number", "").strip(),
        "scanned_code": form.get("scanned_code", "").strip(),
        "offer_identifier": form.get("offer_identifier", "").strip(),
    }


@app.route("/modules/<module_key>/add-row-item", methods=["POST"])
def module_add_row_item(module_key):
    """Add a RowItem to the Uni-Commerce draft."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        item = _clean_row_item(request.form)
        if not item["material_number"]:
            return (
                jsonify({"success": False, "error": "Material number is required"}),
                400,
            )

        state = get_module_state(module_key)
        state.setdefault("row_items", []).append(item)
        set_module_state(module_key, state)

        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": True, "count": len(state["row_items"])})
        flash("Row item added successfully!", "success")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error adding row item: {str(e)}")
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
        flash(f"Error adding row item: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/add-row-item-from-db", methods=["POST"])
def module_add_row_item_from_db(module_key):
    """Add a RowItem from a database item lookup."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        data = request.get_json()
        if not data:
            return jsonify({"success": False, "error": "No data provided"}), 400

        item = {
            "quantity": 1.0,
            "material_number": data.get("material_number", ""),
            "item_price": float(data.get("item_price", 0) or 0),
            "item_discount": 0.0,
            "vat_percentage": float(data.get("vat_percentage", 0) or 0),
            "barcode": data.get("barcode", ""),
            "batch_number": "",
            "expire_date": "",
            "serial_number": "",
            "scanned_code": "",
            "offer_identifier": "",
        }

        state = get_module_state(module_key)
        state.setdefault("row_items", []).append(item)
        set_module_state(module_key, state)
        return jsonify({"success": True, "count": len(state["row_items"])})
    except Exception as e:
        logger.error(f"Error adding row item from DB: {str(e)}")
        return jsonify({"success": False, "error": str(e)}), 500


@app.route("/modules/<module_key>/remove-row-item/<int:index>", methods=["POST", "GET"])
def module_remove_row_item(module_key, index):
    """Remove a RowItem by index."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        state = get_module_state(module_key)
        items = state.setdefault("row_items", [])
        if 0 <= index < len(items):
            items.pop(index)
            set_module_state(module_key, state)
            if request.headers.get("X-Requested-With") == "XMLHttpRequest":
                return jsonify({"success": True})
            flash("Row item removed.", "success")
        else:
            if request.headers.get("X-Requested-With") == "XMLHttpRequest":
                return jsonify({"success": False, "error": "Invalid index"}), 404
            flash("Invalid row item index", "danger")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error removing row item: {str(e)}")
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
        flash(f"Error removing row item: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/get-row-item/<int:index>")
def module_get_row_item(module_key, index):
    """Get one RowItem for editing."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        items = get_module_state(module_key).get("row_items", [])
        if 0 <= index < len(items):
            item = items[index].copy()
            item["index"] = index
            return jsonify(item)
        return jsonify({"error": "Invalid index"}), 404
    except Exception as e:
        logger.error(f"Error getting row item: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/modules/<module_key>/update-row-item/<int:index>", methods=["POST"])
def module_update_row_item(module_key, index):
    """Update a RowItem by index."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        state = get_module_state(module_key)
        items = state.setdefault("row_items", [])
        if not (0 <= index < len(items)):
            return jsonify({"success": False, "error": "Invalid index"}), 404

        items[index] = _clean_row_item(request.form)
        set_module_state(module_key, state)

        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": True})
        flash("Row item updated.", "success")
        return redirect(url_for("module_home", module_key=module_key))
    except Exception as e:
        logger.error(f"Error updating row item: {str(e)}")
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
        flash(f"Error updating row item: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/calculate-invoice-totals")
def module_calculate_invoice_totals(module_key):
    """Computed invoice totals (from the serializer's aggregation)."""
    get_module_or_404(module_key)
    require_unicommerce_module(module_key)
    try:
        module = MODULE_REGISTRY[module_key]
        state = get_module_state(module_key)
        payload = module.build_payload(state)
        return jsonify(
            {
                "gross_amount": payload["GrossAmount"],
                "total_discount": payload["TotalDiscount"],
                "total_vat": payload["TotalVat"],
                "delivery_fees": payload["DeliveryDetails"]["DeliveryFees"],
                "net_amount": payload["NetAmount"],
                "paid_online_amount": payload["PaidOnlineAmount"],
                "paid_with_points_amount": payload["PaidWithPointsAmount"],
                "customer_credit_amount": payload["CustomerCreditAmount"],
            }
        )
    except Exception as e:
        logger.error(f"Error calculating invoice totals: {str(e)}")
        return jsonify({"error": str(e)}), 500


# --------------------------------------------------------------------------
# Generic (module-agnostic) actions: export/send/cancel/test/reset
# --------------------------------------------------------------------------


@app.route("/modules/<module_key>/export-json")
def module_export_json(module_key):
    """Export the module's draft as its API JSON."""
    module = get_module_or_404(module_key)
    try:
        state = get_module_state(module_key)
        payload = module.build_payload(state)
        return jsonify(payload)
    except Exception as e:
        logger.error(f"Error exporting JSON: {str(e)}")
        return jsonify({"error": str(e)}), 500


@app.route("/modules/<module_key>/load-default")
def module_load_default(module_key):
    """Reset the module's draft to its default state."""
    module = get_module_or_404(module_key)
    try:
        set_module_state(module_key, module.default_state())
        flash("Default data loaded successfully!", "success")
    except Exception as e:
        logger.error(f"Error loading default data: {str(e)}")
        flash(f"Error loading default data: {str(e)}", "danger")
    return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/clear-all")
def module_clear_all(module_key):
    """Clear the module's draft (keeping structural defaults, blanking identifying fields)."""
    module = get_module_or_404(module_key)
    try:
        state = module.default_state()
        if module_key in FLAT_ORDER_MODULE_KEYS:
            for field in (
                "branch_code",
                "order_code",
                "parent_order_code",
                "client_first_name",
                "client_middle_name",
                "client_last_name",
                "client_phone",
                "client_email",
                "order_address",
            ):
                state["order_data"][field] = ""
            state["products"] = []
            state["payments"] = []
        set_module_state(module_key, state)
        flash("All data cleared successfully!", "success")
    except Exception as e:
        logger.error(f"Error clearing data: {str(e)}")
        flash(f"Error clearing data: {str(e)}", "danger")
    return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/send-request", methods=["POST"])
def module_send_request(module_key):
    """Send the module's draft to its API endpoint."""
    module = get_module_or_404(module_key)
    try:
        environment = get_active_environment(module)
        custom_url = request.form.get("custom_url", "").strip()
        url = custom_url if custom_url else environment.api_url

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
            return redirect(url_for("module_home", module_key=module_key))

        state = get_module_state(module_key)
        payload = module.build_payload(state)

        validate_flag = request.form.get("validateBeforeSend")
        should_validate = validate_flag in ("on", "true", "1", True)

        if should_validate:
            errors = module.validate(payload)
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
                return redirect(url_for("module_home", module_key=module_key))

        response = APIManager.send_order(url, payload)
        set_module_state(module_key, state)  # autosave the draft that was sent

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
        return redirect(url_for("module_home", module_key=module_key))

    except Exception as e:
        logger.error(f"Error sending request: {str(e)}")
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
        flash(f"Error sending request: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/cancel-order", methods=["POST"])
def module_cancel_order(module_key):
    """Cancel an existing order via this module's cancel endpoint."""
    module = get_module_or_404(module_key)
    try:
        environment = get_active_environment(module)
        custom_url = request.form.get("custom_url", "").strip()
        url = custom_url if custom_url else environment.cancel_url

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
            return redirect(url_for("module_home", module_key=module_key))

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
            return redirect(url_for("module_home", module_key=module_key))

        cancel_data = {"order_number": order_number, "reason": reason}
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
        return redirect(url_for("module_home", module_key=module_key))

    except Exception as e:
        logger.error(f"Error canceling order: {str(e)}")
        if request.headers.get("X-Requested-With") == "XMLHttpRequest":
            return jsonify({"success": False, "error": str(e)}), 500
        flash(f"Error canceling order: {str(e)}", "danger")
        return redirect(url_for("module_home", module_key=module_key))


@app.route("/modules/<module_key>/test-database-connection", methods=["POST"])
def module_test_database_connection(module_key):
    """Test database connection, using provided credentials or this module's own config."""
    get_module_or_404(module_key)
    try:
        data = request.get_json(silent=True) or {}
        module_db_config = DB_CONFIGS.get(module_key, {})

        server = data.get("server") or module_db_config.get("server", ".")
        database = data.get("database") or module_db_config.get(
            "database", "RMSCashierSrv"
        )
        username = data.get("username") or module_db_config.get("username", "sa")
        password = data.get("password") or module_db_config.get("password", "")

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


@app.route("/modules/<module_key>/test-endpoint", methods=["POST"])
def module_test_endpoint(module_key):
    """Test API endpoint connectivity."""
    get_module_or_404(module_key)
    try:
        data = request.get_json()
        if not data or "url" not in data:
            return jsonify({"status": "Error", "error": "No URL provided"}), 400

        result = APIManager.test_endpoint(data["url"])
        return jsonify(result)

    except Exception as e:
        logger.error(f"Error testing endpoint: {str(e)}")
        return jsonify({"status": "Error", "error": str(e)}), 500


@app.context_processor
def inject_global_variables():
    modules_meta = []
    for module in MODULE_REGISTRY.values():
        modules_meta.append(
            {
                "key": module.key,
                "label": module.label,
                "client": module.client,
                "available": module.available,
                "environments": [
                    {
                        "key": env.key,
                        "environment": env.environment,
                        "description": env.description,
                        "accent": env.accent,
                        "cue": env.cue,
                        "icon": env.icon,
                        "route_label": env.route_label,
                        "visual_url": env.visual_url,
                        "visual_alt": env.visual_alt,
                        "available": env.available,
                        "status_label": env.status_label,
                    }
                    for env in module.environments.values()
                ],
            }
        )
    return dict(
        modules=modules_meta,
        payment_methods=PAYMENT_METHODS,
        payment_statuses=PAYMENT_STATUSES,
        payment_options=PAYMENT_OPTIONS,
    )


@app.context_processor
def inject_session_data():
    module_key = get_active_module_key()
    module = MODULE_REGISTRY[module_key]
    state = session.get("modules", {}).get(module_key, module.default_state())
    payment_summary = (
        flat_order.calculate_payment_summary(state)
        if module_key in FLAT_ORDER_MODULE_KEYS
        else None
    )
    return dict(
        active_module_key=module_key,
        active_module_label=module.label,
        active_environment_key=session.get("active_environment"),
        module_state=state,
        payment_summary=payment_summary,
        client_selected=session.get("client_selected", False),
    )


if __name__ == "__main__":
    app.run(debug=True, port=5002)
