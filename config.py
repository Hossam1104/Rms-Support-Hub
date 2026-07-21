"""
Configuration settings for Online Order Tool
"""

import os
from datetime import datetime, timedelta

ASSET_BASE_URL = "/static/assets"
CLIENT_LOGOS = {
    "UPC": f"{ASSET_BASE_URL}/upc_logo.svg",
    "GHC": f"{ASSET_BASE_URL}/kunooz_logo.svg",
    "Whites UniCommerce": f"{ASSET_BASE_URL}/whites_logo.svg",
}

CLIENT_ENDPOINTS = {
    "UPC Production": {
        "client": "UPC",
        "environment": "Production",
        "description": "UPC live routing.",
        "accent": "sunrise",
        "cue": "Retail Ops",
        "icon": "bi-bag-check",
        "route_label": "Live lane",
        "visual_url": CLIENT_LOGOS["UPC"],
        "visual_alt": "UPC logo",
        "available": True,
        "api_url": "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
        "cancel_url": "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
    },
    "UPC Testing": {
        "client": "UPC",
        "environment": "Testing",
        "description": "UPC QA routing.",
        "accent": "electric",
        "cue": "QA Grid",
        "icon": "bi-sliders2-vertical",
        "route_label": "Test lane",
        "visual_url": CLIENT_LOGOS["UPC"],
        "visual_alt": "UPC logo",
        "available": True,
        "api_url": "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
        "cancel_url": "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CancelOrder",
    },
    "GHC Production": {
        "client": "GHC",
        "environment": "Production",
        "description": "GHC live routing.",
        "accent": "ember",
        "cue": "Warehouse",
        "icon": "bi-box-seam",
        "route_label": "Live lane",
        "visual_url": CLIENT_LOGOS["GHC"],
        "visual_alt": "GHC logo",
        "available": True,
        "api_url": "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CreateAndAssignOrder",
        "cancel_url": "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CancelOrder",
    },
    "GHC Testing": {
        "client": "GHC",
        "environment": "Testing",
        "description": "GHC QA routing.",
        "accent": "ocean",
        "cue": "Dispatch",
        "icon": "bi-truck",
        "route_label": "QA lane",
        "visual_url": CLIENT_LOGOS["GHC"],
        "visual_alt": "GHC logo",
        "available": True,
        "api_url": "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder",
        "cancel_url": "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CancelOrder",
    },
    "Whites UniCommerce Production": {
        "client": "Whites UniCommerce",
        "environment": "Production",
        "description": "UniCommerce live lane pending.",
        "accent": "aurora",
        "cue": "Automation",
        "icon": "bi-cpu",
        "route_label": "Pending lane",
        "visual_url": CLIENT_LOGOS["Whites UniCommerce"],
        "visual_alt": "Whites UniCommerce logo",
        "available": False,
        "api_url": None,
        "cancel_url": None,
    },
    "Whites UniCommerce Testing": {
        "client": "Whites UniCommerce",
        "environment": "Testing",
        "description": "UniCommerce QA lane pending.",
        "accent": "violet",
        "cue": "Staging",
        "icon": "bi-hourglass-split",
        "route_label": "Pending lane",
        "visual_url": CLIENT_LOGOS["Whites UniCommerce"],
        "visual_alt": "Whites UniCommerce logo",
        "available": False,
        "api_url": None,
        "cancel_url": None,
    },
}

LEGACY_ENDPOINT_ALIASES = {
    "UPC Pharmacy - Production": "UPC Production",
    "UPC Pharmacy - Testing": "UPC Testing",
    "Whites Pharmacy - Production": "GHC Production",
    "Whites Pharmacy - Testing": "GHC Testing",
}

API_URLS = {
    name: config["api_url"]
    for name, config in CLIENT_ENDPOINTS.items()
    if config["api_url"]
}

CANCEL_API_URLS = {
    name: config["cancel_url"]
    for name, config in CLIENT_ENDPOINTS.items()
    if config["cancel_url"]
}

CLIENT_OPTIONS = [
    {
        "key": name,
        "client": config["client"],
        "environment": config["environment"],
        "description": config["description"],
        "accent": config["accent"],
        "cue": config["cue"],
        "icon": config["icon"],
        "route_label": config["route_label"],
        "visual_url": config["visual_url"],
        "visual_alt": config["visual_alt"],
        "available": config["available"],
        "api_url": config["api_url"] or "",
        "cancel_url": config["cancel_url"] or "",
        "status_label": (
            "Live"
            if config["available"] and config["environment"] == "Production"
            else "Test" if config["available"] else "Soon"
        ),
    }
    for name, config in CLIENT_ENDPOINTS.items()
]

DEFAULT_API_ENDPOINT = "GHC Testing"

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

# Database Configuration
DB_CONFIG = {
    "server": os.environ.get("DB_SERVER", "."),
    "database": os.environ.get("DB_DATABASE", "RMSCashierSrv"),
    "username": os.environ.get("DB_USERNAME", "sa"),
    "password": os.environ.get("DB_PASSWORD", "P@ssw0rd"),
    "driver": os.environ.get("DB_DRIVER", "ODBC Driver 17 for SQL Server"),
}


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
