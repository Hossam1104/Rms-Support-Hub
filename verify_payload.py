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


def verify_upc_ui_and_db() -> bool:
    """UPC-only structural checks that aren't about the payload schema:
    the Delivery Information card must be gone from UPC's page (GHC keeps
    it), and UPC's two environments must resolve to two different databases
    on the same server (every other module shares one config across its
    environments).
    """
    ok = True

    import app as flask_app

    with flask_app.app.test_client() as client:
        with client.session_transaction() as sess:
            sess["active_module"] = "upc_ecommerce"
            sess["active_environment"] = "UPC Testing"
        upc_html = client.get("/modules/upc_ecommerce/").get_data(as_text=True)

        with client.session_transaction() as sess:
            sess["active_module"] = "ghc_ecommerce"
            sess["active_environment"] = "GHC Testing"
        ghc_html = client.get("/modules/ghc_ecommerce/").get_data(as_text=True)

    if "Delivery Information" in upc_html:
        print("[upc_ui] FAIL: UPC page still renders the Delivery Information card")
        ok = False
    if "validation-tab" not in upc_html:
        print("[upc_ui] FAIL: UPC page is missing the Order Validation tab")
        ok = False
    if "modalItemSearchBtn" not in upc_html:
        print("[upc_ui] FAIL: UPC page is missing the Add Product modal's item search")
        ok = False
    if "Item Lookup" in upc_html:
        print("[upc_ui] FAIL: UPC page still has the old Database-Connection-tab Item Lookup card")
        ok = False
    if "Delivery Information" not in ghc_html:
        print("[upc_ui] FAIL: GHC page lost its Delivery Information card (regression)")
        ok = False
    if "validation-tab" in ghc_html:
        print("[upc_ui] FAIL: Order Validation tab leaked into the GHC page")
        ok = False
    if "modalItemSearchBtn" in ghc_html:
        print("[upc_ui] FAIL: UPC's Add Product item search leaked into the GHC page")
        ok = False
    if "Item Lookup" not in ghc_html:
        print("[upc_ui] FAIL: GHC lost its Database-Connection-tab Item Lookup card (regression)")
        ok = False

    if ok:
        print("[upc_ui] OK — UPC/GHC page differences are exactly as designed")

    from modules import MODULE_REGISTRY

    upc = MODULE_REGISTRY["upc_ecommerce"]
    prod_db = upc.environments["UPC Production"].db_config
    test_db = upc.environments["UPC Testing"].db_config

    if prod_db["database"] != "RmsMainProd":
        print(f"[upc_db] FAIL: UPC Production database is '{prod_db['database']}', expected 'RmsMainProd'")
        ok = False
    if test_db["database"] != "RmsMainTest2":
        print(f"[upc_db] FAIL: UPC Testing database is '{test_db['database']}', expected 'RmsMainTest2'")
        ok = False
    if prod_db["database"] == test_db["database"]:
        print("[upc_db] FAIL: UPC Production and Testing resolved to the same database")
        ok = False
    if prod_db["server"] != test_db["server"]:
        print(
            "[upc_db] FAIL: UPC Production/Testing are on different servers "
            f"({prod_db['server']} vs {test_db['server']}), expected the same server"
        )
        ok = False

    if ok:
        print(
            f"[upc_db] OK — UPC Production -> {prod_db['database']}, "
            f"UPC Testing -> {test_db['database']}, both on {prod_db['server']}"
        )

    return ok


def verify_upc_material_number_normalization() -> bool:
    """UPC's Add Product item search accepts either the 6-digit item number
    or the full 18-digit material number (12 leading zeros + 6 digits); any
    other length must be rejected. Pure-function check, no DB needed.
    """
    from modules.base import ValidationError
    from modules.flat_order import normalize_upc_material_number

    ok = True

    if normalize_upc_material_number("207350") != "000000000000207350":
        print("[upc_item] FAIL: 6-digit input was not padded to 18 digits correctly")
        ok = False
    if normalize_upc_material_number("000000000000207350") != "000000000000207350":
        print("[upc_item] FAIL: an already-18-digit input was altered")
        ok = False
    for bad in ["12345", "1234567", "1234567890123456789", "abcdef", ""]:
        try:
            normalize_upc_material_number(bad)
            print(f"[upc_item] FAIL: invalid input {bad!r} was not rejected")
            ok = False
        except ValidationError:
            pass

    if ok:
        print("[upc_item] OK — material number normalization (6-digit <-> 18-digit) is correct")
    return ok


def verify_phone_search_normalization() -> bool:
    """Phone-number search (consumer lookup + Order Validation) must accept
    a number typed with or without '+', the '966' country code, or a leading
    '0' trunk prefix, and treat them all as the same number. Pure-function
    check, no DB needed.
    """
    from modules.base import ValidationError
    from modules.flat_order import normalize_phone_search

    ok = True
    variants = ["+966556028080", "966556028080", "0556028080", "556028080"]
    canonical = {normalize_phone_search(v) for v in variants}
    if canonical != {"556028080"}:
        print(f"[phone_search] FAIL: variants {variants} did not all normalize to '556028080', got {canonical}")
        ok = False

    for bad in ["12345", "", "abc"]:
        try:
            normalize_phone_search(bad)
            print(f"[phone_search] FAIL: too-short input {bad!r} was not rejected")
            ok = False
        except ValidationError:
            pass

    if ok:
        print("[phone_search] OK — +966/966/0/bare phone variants all normalize identically")
    return ok


def main():
    results = [
        verify_module("ghc_ecommerce", GHC_REFERENCE),
        verify_module("upc_ecommerce", UPC_REFERENCE),
        verify_upc_ui_and_db(),
        verify_upc_material_number_normalization(),
        verify_phone_search_normalization(),
    ]
    if all(results):
        print("\nAll flat-order modules verified.")
        return 0
    print("\nVerification FAILED.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
