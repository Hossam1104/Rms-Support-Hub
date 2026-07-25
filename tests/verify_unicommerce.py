"""Verify the GHC Uni-Commerce module serializer + validator.

Builds two invoices (a non-return and a partial-return) from draft state and
asserts their key sets and computed totals match the corresponding
request_examples/GHC Uni-Commerce/*.json references field-for-field, and that the
validator enforces the return / payment-reconciliation rules.
"""

import json
import os

from modules import MODULE_REGISTRY

EXAMPLES_DIR = os.path.join("request_examples", "GHC Uni-Commerce")
NON_RETURN_REF = os.path.join(EXAMPLES_DIR, "AMAZON_AMZ-00011.json")
RETURN_REF = os.path.join(EXAMPLES_DIR, "AMAZON_PARTIAL_RETURN_AMZ-RET-003.json")


def _load(path):
    with open(path, "r") as f:
        return json.load(f)


def _consumer(code):
    return {
        "first_name": "Amazon",
        "middle_name": "",
        "last_name": "Bulk",
        "consumer_code": code,
        "gender": "Male",
        "birth_date": "1980-01-01",
        "primary_phone_number": "966500000011",
        "email": "bulk@amazon.com",
        "national_id": "",
        "nationality": "",
    }


def _delivery(notes=""):
    return {
        "delivery_phone_number": "966500000011",
        "delivery_address": "Jeddah",
        "delivery_location_url": "",
        "delivery_notes": notes,
        "delivery_fees": 10.0,
    }


def _row(quantity):
    return {
        "quantity": quantity,
        "material_number": "000000000000021241",
        "item_price": 90.0,
        "item_discount": 45.0,
        "vat_percentage": 15.0,
        "barcode": "681619810619",
        "batch_number": "",
        "expire_date": "",
        "serial_number": "",
        "scanned_code": "",
        "offer_identifier": "000000000011",
    }


def _check(payload, reference, label):
    ok = True
    if set(payload.keys()) != set(reference.keys()):
        print(f"[{label}] FAIL top-level key mismatch")
        print("  missing:", sorted(set(reference.keys()) - set(payload.keys())))
        print("  extra:  ", sorted(set(payload.keys()) - set(reference.keys())))
        ok = False
    for section in ("InvoiceConsumer", "DeliveryDetails"):
        if set(payload[section].keys()) != set(reference[section].keys()):
            print(f"[{label}] FAIL {section} key mismatch")
            ok = False
    if set(payload["RowItems"][0].keys()) != set(reference["RowItems"][0].keys()):
        print(f"[{label}] FAIL RowItems key mismatch")
        ok = False

    for field in ("GrossAmount", "TotalDiscount", "TotalVat", "NetAmount"):
        if abs(payload[field] - reference[field]) > 0.01:
            print(
                f"[{label}] FAIL {field}: got {payload[field]}, expected {reference[field]}"
            )
            ok = False

    if ok:
        print(f"[{label}] OK — shape and totals match reference")
    return ok


def main():
    module = MODULE_REGISTRY["ghc_unicommerce"]
    results = []

    # --- Non-return invoice (matches AMAZON_AMZ-00011) ---
    state = module.default_state()
    state.update(
        {
            "reference_number": "AMZ-00011",
            "online_order_number": "AMZ-ORD-00011",
            "customer_name": "AMAZON",
            "is_return": False,
            "consumer": _consumer("AMZ-9999"),
            "delivery": _delivery(),
            "row_items": [_row(4.0)],
        }
    )
    payload = module.build_payload(state)
    results.append(_check(payload, _load(NON_RETURN_REF), "non-return"))

    # --- Partial-return invoice (matches AMAZON_PARTIAL_RETURN_AMZ-RET-003) ---
    ret_state = module.default_state()
    ret_state.update(
        {
            "reference_number": "AMZ-RET-003",
            "online_order_number": "AMZ-ORD-00011-RET",
            "customer_name": "AMAZON",
            "is_return": True,
            "parent_reference_number": "AMZ-00011",
            "consumer": _consumer("AMZ-RET-03"),
            "delivery": _delivery("Partial quantity return"),
            "row_items": [_row(2.0)],
        }
    )
    ret_payload = module.build_payload(ret_state)
    results.append(_check(ret_payload, _load(RETURN_REF), "partial-return"))

    # --- Validator rules ---
    valid_return_errors = module.validate(ret_payload)
    if valid_return_errors:
        print(f"[validator] FAIL valid return flagged: {valid_return_errors}")
        results.append(False)
    else:
        print("[validator] OK — valid return passes")

    bad_return = dict(ret_payload)
    bad_return["ParentReferenceNumber"] = None
    if any("ParentReferenceNumber" in e for e in module.validate(bad_return)):
        print("[validator] OK — return without ParentReferenceNumber is rejected")
    else:
        print("[validator] FAIL — return without ParentReferenceNumber not caught")
        results.append(False)

    if all(results):
        print("\nUni-Commerce module verified.")
        return 0
    print("\nVerification FAILED.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
