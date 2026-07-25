"""
Configuration settings for Online Order Tool
"""

import os
from datetime import datetime, timedelta

ASSET_BASE_URL = "/static/assets"
CLIENT_LOGOS = {
    "UPC": f"{ASSET_BASE_URL}/upc_logo.svg",
    "GHC": f"{ASSET_BASE_URL}/whites_logo.svg",
    "Whites UniCommerce": f"{ASSET_BASE_URL}/whites_logo.svg",
}

# NOTE: The old CLIENT_ENDPOINTS / API_URLS / CANCEL_API_URLS / CLIENT_OPTIONS /
# DEFAULT_API_ENDPOINT structures were removed in the module refactor (session 8).
# Each module's Production/Testing environments (URLs, branding, DB config) now
# live in modules/*.py and are exposed via modules.MODULE_REGISTRY.

LEGACY_ENDPOINT_ALIASES = {
    "UPC Pharmacy - Production": "UPC Production",
    "UPC Pharmacy - Testing": "UPC Testing",
    "Whites Pharmacy - Production": "GHC Production",
    "Whites Pharmacy - Testing": "GHC Testing",
}

# Payment Configuration - Update with allowed methods from API
PAYMENT_METHODS = [
    "COD",
    "Visa",
    "RajhiPoints",
    "Tamara",
    "Tabby",
    "NeqatyPoints",
    "QitafPoints",
    "MisPay",
    "Emkan",
    "YouGotaGift",
    "OgMoney",
]

PAYMENT_STATUSES = ["not_payment", "done_payment", "partially_paid"]

PAYMENT_OPTIONS = {
    "COD": ["COD"],
    "Visa": ["visa", "mastercard", "mada", "amex"],
    "RajhiPoints": ["RajhiPoints"],
    "Tamara": ["tamara"],
    "Tabby": ["tabby"],
    "NeqatyPoints": ["NeqatyPoints"],
    "QitafPoints": ["QitafPoints"],
    "MisPay": ["mispay"],
    "Emkan": ["emkan"],
    "YouGotaGift": ["yougotagift"],
    "OgMoney": ["ogmoney"],
}

# NOTE: the single global DB_CONFIG was removed in the module refactor. Each
# module now has its own DB config in modules/db_config.py (env-var prefixed:
# GHC_ECOM_DB_*, GHC_UNICOM_DB_*), exposed as DB_CONFIGS. UPC is the
# exception: its Production/Testing environments use two separate databases
# (UPC_ECOM_PROD_DB_* / UPC_ECOM_TEST_DB_*, see UPC_DB_CONFIGS).


# Application Configuration
class AppConfig:
    SECRET_KEY = os.environ.get("FLASK_SECRET_KEY", "your-secret-key-here")
    SESSION_TYPE = "filesystem"
    JSON_AS_ASCII = False
    MAX_CONTENT_LENGTH = 16 * 1024 * 1024  # 16MB max file size


def get_default_data():
    """Get default order data with current timestamps"""
    current_date = datetime.now().strftime("%Y-%m-%d")
    current_time = datetime.now().strftime("%H:%M:%S")
    three_hours_later = (datetime.now() + timedelta(hours=3)).strftime("%H:%M:%S")

    return {
        "branch_code": "2000",
        "order_code": f"ORD{datetime.now().strftime('%Y%m%d%H%M%S')}",
        "parent_order_code": "",
        "order_delivery_cost": 10.0,
        "is_delivery": 1,
        "order_status": "new",
        "order_payment_status": "not_payment",
        "delivery_date": current_date,
        "delivery_from_time": current_time,
        "delivery_to_time": three_hours_later,
        "shipping_address_2": "",
        "fullfilment_plant": "MAIN",
        "order_notes": "Don't Ring the bell",
        "client_first_name": "John",
        "client_middle_name": "Michael",
        "client_last_name": "Doe",
        "client_phone": "5551234567",
        "client_email": "john.doe@example.com",
        "client_birthdate": "1989-04-11T12:00:00.000Z",
        "client_gender": "Male",
        "client_country_code": "966",
        "order_address": "123 Main St, City, State 12345",
        "order_products": [
            {
                "item_code": "123456",
                "item_name": "Sample Product",
                "quantity": 2.0,
                "unit_price": 25.0,
                "unit_vat_amount": 3.75,
                "total_vat_amount": 7.5,
                "vat_percentage": 15.0,
                "offer_code": "",
                "offer_message": "",
                "row_total_discount": 0.0,
                "row_net_total": 57.5,
            }
        ],
        "payment_methods_with_options": [
            {
                "payment_method": "COD",
                "payment_status": "not_payment",
                "payment_amount": 57.5,
                "transaction_id": "",
                "payment_option": "COD",
                "option_commission": 0.0,
                "card_name": "",
                "bank_code": "",
                "credit_customer_info": None,
            }
        ],
    }


# For backward compatibility
DEFAULT_DATA = get_default_data()
