"""Verify the flat-order modules (ghc_ecommerce, upc_ecommerce).

Both modules share the serializer in modules/flat_order.py, but UPC has a
different schema (no delivery/fulfillment fields). This script verifies each
module against its own reference example.
"""

import json
import os

from modules import MODULE_REGISTRY

GHC_REFERENCE = os.path.join("request_examples", "GHC E-Commerce", "request_body.json")
UPC_REFERENCE = os.path.join(
    "request_examples",
    "UPC",
    "4- Invoice without discount, with delivery and paid by visa.json",
)


def _load_reference(path):
    with open(path, "r") as f:
        return json.load(f)


def _sample_state():
    """A representative flat-order draft ({order_data, products, payments})."""
    return {
        "order_data": {
            "branch_code": "2000",
            "order_code": "ORD20260101000000",
            "parent_order_code": "",
            "order_delivery_cost": 10.0,
            "is_delivery": 1,
            "order_status": "new",
            "order_payment_status": "not_payment",
            "delivery_date": "2026-01-01",
            "delivery_from_time": "12:00:00",
            "delivery_to_time": "15:00:00",
            "shipping_address_2": "",
            "fullfilment_plant": "MAIN",
            "order_notes": "Don't Ring the bell",
            "client_first_name": "John",
            "client_middle_name": "Michael",
            "client_last_name": "Doe",
            "client_phone": "5551234567",
            "client_email": "john.doe@example.com",
            "client_birthdate": "1989-04-11",
            "client_gender": "Male",
            "client_country_code": "966",
            "order_address": "123 Main St",
        },
        "products": [
            {
                "item_code": "123456",
                "item_name": "Test Product",
                "quantity": 2.0,
                "unit_price": 50.0,
                "vat_percentage": 15.0,
                "row_total_discount": 0.0,
                "row_net_total": 115.0,
                "unit_vat_amount": 7.5,
                "total_vat_amount": 15.0,
                "offer_code": "",
                "offer_message": "",
            }
        ],
        "payments": [
            {
                "payment_method": "COD",
                "payment_status": "not_payment",
                "payment_amount": 125.0,
                "transaction_id": "",
                "payment_option": "COD",
                "option_commission": 0.0,
                "credit_customer_info": None,
            }
        ],
    }


def verify_module(module_key: str, reference_path: str) -> bool:
    module = MODULE_REGISTRY[module_key]
    reference = _load_reference(reference_path)
    payload = module.build_payload(_sample_state())

    ok = True

    # Top-level key set: the reference has two documentation-only placeholder keys
    # (order_country_code, order_phone) that neither the legacy nor the new
    # serializer ever produced. Everything else must match exactly.
    ref_keys = set(reference.keys()) - {"order_country_code", "order_phone"}
    pay_keys = set(payload.keys())
    if ref_keys != pay_keys:
        print(f"[{module_key}] FAIL top-level mismatch")
        print("  missing:", sorted(ref_keys - pay_keys))
        print("  extra:  ", sorted(pay_keys - ref_keys))
        ok = False

    # Product key set
    if set(reference["order_products"][0].keys()) != set(
        payload["order_products"][0].keys()
    ):
        print(f"[{module_key}] FAIL order_products key mismatch")
        print("  ref:", sorted(reference["order_products"][0].keys()))
        print("  got:", sorted(payload["order_products"][0].keys()))
        ok = False

    # Payment key set: the serializer intentionally omits card_name/bank_code
    # (legacy behavior — they were always filtered out). The reference example
    # itself omits payment_status (a documentation placeholder), so the expected
    # set is the reference keys plus payment_status, minus card_name/bank_code.
    expected_pay_keys = (
        set(reference["payment_methods_with_options"][0].keys()) | {"payment_status"}
    ) - {"card_name", "bank_code"}
    got_pay_keys = set(payload["payment_methods_with_options"][0].keys())
    if expected_pay_keys != got_pay_keys:
        print(f"[{module_key}] FAIL payment_methods_with_options key mismatch")
        print("  missing:", sorted(expected_pay_keys - got_pay_keys))
        print("  extra:  ", sorted(got_pay_keys - expected_pay_keys))
        ok = False

    if ok:
        print(f"[{module_key}] OK — payload matches flat-order schema")
    return ok


def main():
    results = [
        verify_module("ghc_ecommerce", GHC_REFERENCE),
        verify_module("upc_ecommerce", UPC_REFERENCE),
    ]
    if all(results):
        print("\nAll flat-order modules verified.")
        return 0
    print("\nVerification FAILED.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
