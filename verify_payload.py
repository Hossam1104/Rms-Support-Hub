from app import app
from managers import OrderManager
import json
import logging

# Disable logging
logging.disable(logging.CRITICAL)


def verify_payload():
    with app.test_request_context():
        # Setup session with mocked valid data
        from flask import session

        # Valid COD Payment
        session["payments"] = [
            {
                "payment_method": "COD",
                "payment_status": "not_payment",
                "payment_amount": 110.00,
                "transaction_id": "",
                "payment_option": "COD",
                "option_commission": 0,
                "credit_customer_info": None,
                "card_name": "Should Be Ignored",  # Extra field to test filtering
                "bank_code": "Should Be Ignored",
            }
        ]

        session["products"] = [
            {
                "item_code": "123456",
                "item_name": "Test Product",
                "quantity": 2.0,
                "unit_price": 50.00,
                "vat_percentage": 15.0,  # Will be converted to 0.15
                "row_total_discount": 0.0,
                "row_net_total": 100.00,
                "unit_vat_amount": 7.50,
                "offer_code": "",
                "offer_message": "",
            }
        ]

        session["order_data"] = {
            "order_delivery_cost": 10.00,
            "is_delivery": 1,
            "order_status": "new",
            "client_country_code": "966",
        }

        # Generate payload
        payload = OrderManager.prepare_order_data()

        # Verify Payment Structure
        print("Verifying Payment Structure...")
        payments = payload["payment_methods_with_options"]
        assert len(payments) == 1
        p = payments[0]

        expected_keys = {
            "payment_method",
            "payment_status",
            "payment_amount",
            "transaction_id",
            "payment_option",
            "option_commission",
            "credit_customer_info",
        }

        actual_keys = set(p.keys())

        # Check for extra keys
        extra_keys = actual_keys - expected_keys
        if extra_keys:
            print(f"FAIL: Found extra keys in payment payload: {extra_keys}")
            exit(1)

        # Check for missing keys
        missing_keys = expected_keys - actual_keys
        if missing_keys:
            print(f"FAIL: Missing keys in payment payload: {missing_keys}")
            exit(1)

        print("SUCCESS: Payment payload structure is strictly valid.")

        # Verify Values
        assert p["payment_method"] == "COD"
        assert p["payment_amount"] == 110.00
        assert p["credit_customer_info"] is None

        print("SUCCESS: Payment values are correct.")

        # Verify Product Structure (VAT conversion)
        print("Verifying Product Structure...")
        prod = payload["order_products"][0]
        assert prod["vat_percentage"] == 0.15
        print("SUCCESS: Product VAT percentage converted correctly.")


if __name__ == "__main__":
    verify_payload()
