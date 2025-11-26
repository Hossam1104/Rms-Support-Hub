# API Configuration
API_URLS = {
    "UPC Pharmacy - Production": "http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
    "UPC Pharmacy - Testing": "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
    "Whites Pharmacy - Production": "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CreateAndAssignOrder",
    "Whites Pharmacy - Testing": "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder"
}

CANCEL_API_URLS = {
    "UPC Pharmacy - Production": "http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
    "UPC Pharmacy - Testing": "http://10.10.9.181:8080/RmsMainServerApi/api/Order/CancelOrder",
    "Whites Pharmacy - Production": "https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CancelOrder",
    "Whites Pharmacy - Testing": "http://10.10.20.126:8090/RmsMainServerApi/api/Order/CancelOrder"
}

DEFAULT_API_ENDPOINT = "Whites Pharmacy - Testing"

# Payment Configuration
PAYMENT_METHODS = [
    "cash",
    "credit_card",
    "debit_card",
    "bank_transfer",
    "wallet",
    "PostToCredit",
    "Points",
    "Visa",
    "Tamara",
    "Tabby",
    "MisPay",
    "Emkan",
    "YouGotaGift",
    "OgMoney"
]

PAYMENT_STATUSES = [
    "not_payment",
    "done_payment",
    "pending_payment",
    "failed_payment",
    "refunded"
]

PAYMENT_OPTIONS = {
    "cash": ["cash"],
    "credit_card": ["visa", "mastercard", "amex"],
    "debit_card": ["visa_debit", "mastercard_debit"],
    "bank_transfer": ["bank_transfer"],
    "wallet": ["apple_pay", "google_pay", "samsung_pay"],
    "PostToCredit": ["PostToCredit"],
    "Points": ["Points"],
    "Visa": ["visa"],
    "Tamara": ["tamara"],
    "Tabby": ["tabby"],
    "MisPay": ["mispay"],
    "Emkan": ["emkan"],
    "YouGotaGift": ["yougotagift"],
    "OgMoney": ["ogmoney"]
}

# Default Data
DEFAULT_DATA = {
    "branch_code": "2000",
    "order_code": "ORD123456",
    "parent_order_code": "",
    "order_delivery_cost": 10.0,
    "is_delivery": 1,
    "order_status": "new",
    "order_payment_status": "not_payment",
    "delivery_date": "2023-12-15",
    "delivery_from_time": "12:00:00",
    "delivery_to_time": "13:00:00",
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
            "vat_percentage": 0.15,
            "offer_code": "",
            "offer_message": "",
            "row_total_discount": 0.0,
            "row_net_total": 57.5
        }
    ],
    "payment_methods_with_options": [
        {
            "payment_method": "cash",
            "payment_status": "done_payment",
            "payment_amount": 57.5,
            "transaction_id": "TXN123456",
            "payment_option": "cash",
            "option_commission": 0.0,
            "card_name": "",
            "bank_code": "",
            "credit_customer_info": None
        }
    ]
}