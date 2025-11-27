"""
Configuration settings for Online Order Tool
"""

import os
from datetime import datetime, timedelta

# API Configuration
API_URLS = {
    "UPC Pharmacy - Production": "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
    "UPC Pharmacy - Testing": "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
    "Whites Pharmacy - Production": "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CreateAndAssignOrder",
    "Whites Pharmacy - Testing": "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder",
}

CANCEL_API_URLS = {
    "UPC Pharmacy - Production": "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
    "UPC Pharmacy - Testing": "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CancelOrder",
    "Whites Pharmacy - Production": "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CancelOrder",
    "Whites Pharmacy - Testing": "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CancelOrder",
}

DEFAULT_API_ENDPOINT = "Whites Pharmacy - Testing"

# Payment Configuration
PAYMENT_METHODS = [
    "cash",
    "PostToCredit",
    "Points",
    "credit_card",
    "Tamara",
    "Tabby",
    "MisPay",
    "Emkan",
    "YouGotaGift",
    "OgMoney",
]

PAYMENT_STATUSES = ["not_payment", "done_payment", "partially_paid"]

PAYMENT_OPTIONS = {
    "cash": ["cash"],
    "credit_card": ["visa", "mastercard", "mada", "amex"],
    "PostToCredit": ["PostToCredit"],
    "Points": ["Points"],
    "Tamara": ["tamara"],
    "Tabby": ["tabby"],
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


# Default Data Template
def get_default_data():
    """Get default order data with current timestamps"""
    current_date = datetime.now().strftime("%Y-%m-%d")
    current_time = datetime.now().strftime("%H:%M:%S")
    one_hour_later = (datetime.now() + timedelta(hours=1)).strftime("%H:%M:%S")

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
        "delivery_to_time": one_hour_later,
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
                "vat_percentage": 15.0,  # Store as percentage for consistency
                "offer_code": "",
                "offer_message": "",
                "row_total_discount": 0.0,
                "row_net_total": 57.5,
            }
        ],
        "payment_methods_with_options": [
            {
                "payment_method": "cash",
                "payment_status": "not_payment",
                "payment_amount": 57.5,
                "transaction_id": f"TXN{datetime.now().strftime('%Y%m%d%H%M%S')}",
                "payment_option": "cash",
                "option_commission": 0.0,
                "card_name": "",
                "bank_code": "",
                "credit_customer_info": None,
            }
        ],
    }


# For backward compatibility
DEFAULT_DATA = get_default_data()
