"""UPC E-Commerce module.

Shares the exact same JSON schema as GHC E-Commerce (see request_examples/UPC/*.json
vs request_examples/GHC E-Commerce/request_body.json) — only the environment
URLs/DB config differ. Serializer/validator/DB manager logic lives once in
modules/flat_order.py and is reused here via composition.

Note: UPC payloads do NOT include delivery_date, delivery_from_time,
delivery_to_time, shipping_address_2, fullfilment_plant fields that GHC uses.
"""

from typing import Any, Dict, List, Optional

from config import CLIENT_LOGOS

from . import flat_order
from .base import ModuleEnvironment, OrderModule
from .db_config import DB_CONFIGS


class UpcEcommerceModule(OrderModule):
    key = "upc_ecommerce"
    label = "UPC E-Commerce"
    client = "UPC"
    available = True

    def __init__(self):
        db_config = DB_CONFIGS["upc_ecommerce"]
        self.environments: Dict[str, ModuleEnvironment] = {
            "UPC Production": ModuleEnvironment(
                key="UPC Production",
                environment="Production",
                description="UPC live routing.",
                accent="sunrise",
                cue="Retail Ops",
                icon="bi-bag-check",
                route_label="Live lane",
                visual_url=CLIENT_LOGOS["UPC"],
                visual_alt="UPC logo",
                available=True,
                api_url="http://10.10.10.181/RmsMainServerApi/api/Order/CreateAndAssignOrder",
                cancel_url="http://10.10.10.181/RmsMainServerApi/api/Order/CancelOrder",
                db_config=db_config,
            ),
            "UPC Testing": ModuleEnvironment(
                key="UPC Testing",
                environment="Testing",
                description="UPC QA routing.",
                accent="electric",
                cue="QA Grid",
                icon="bi-sliders2-vertical",
                route_label="Test lane",
                visual_url=CLIENT_LOGOS["UPC"],
                visual_alt="UPC logo",
                available=True,
                api_url="http://10.10.9.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder",
                cancel_url="http://10.10.9.181:8080/RmsMainServerApi/api/Order/CancelOrder",
                db_config=db_config,
            ),
        }
        self._db = flat_order.FlatOrderDatabaseManager(db_config)

    def default_state(self) -> Dict[str, Any]:
        return flat_order.default_state()

    def build_payload(self, state: Dict[str, Any]) -> Dict[str, Any]:
        """Build UPC payload without GHC-specific delivery/fulfillment fields."""
        return flat_order.build_upc_payload(state)

    def validate(self, payload: Dict[str, Any]) -> List[str]:
        return flat_order.validate(payload)

    def lookup_item(self, code: str, **filters) -> Dict[str, Any]:
        return self._db.lookup_item(code, **filters)

    def lookup_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        return self._db.lookup_consumer_by_phone(phone)


MODULE = UpcEcommerceModule()
