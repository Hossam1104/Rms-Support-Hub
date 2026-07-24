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
from .db_config import UPC_DB_CONFIGS


class UpcEcommerceModule(OrderModule):
    key = "upc_ecommerce"
    label = "UPC E-Commerce"
    client = "UPC"
    available = True

    def __init__(self):
        prod_db_config = UPC_DB_CONFIGS["UPC Production"]
        test_db_config = UPC_DB_CONFIGS["UPC Testing"]
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
                db_config=prod_db_config,
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
                db_config=test_db_config,
            ),
        }
        # One DB manager per environment (Production -> RmsMainProd, Testing ->
        # RmsMainTest2), built lazily so a missing driver/connection to one
        # environment doesn't prevent using the other.
        self._db_by_env: Dict[str, flat_order.FlatOrderDatabaseManager] = {}

    def _db_for(self, env_key: Optional[str]) -> flat_order.FlatOrderDatabaseManager:
        environment = self.get_environment(env_key)
        if environment.key not in self._db_by_env:
            self._db_by_env[environment.key] = flat_order.FlatOrderDatabaseManager(
                environment.db_config
            )
        return self._db_by_env[environment.key]

    def default_state(self) -> Dict[str, Any]:
        return flat_order.default_state()

    def build_payload(self, state: Dict[str, Any]) -> Dict[str, Any]:
        """Build UPC payload without GHC-specific delivery/fulfillment fields."""
        return flat_order.build_upc_payload(state)

    def validate(self, payload: Dict[str, Any]) -> List[str]:
        return flat_order.validate(payload)

    def lookup_item(
        self, code: str, env_key: Optional[str] = None, **filters
    ) -> Dict[str, Any]:
        # UPC pricing is branch-specific (confirmed schema), distinct from
        # GHC's still-guessed, branch-agnostic lookup_item.
        return self._db_for(env_key).lookup_upc_item(
            code, filters.get("branch_code", "")
        )

    def lookup_consumer_by_phone(
        self, phone: str, env_key: Optional[str] = None
    ) -> Optional[Dict[str, Any]]:
        # UPC has its own Consumers/LoyaltyConsumerAddresses schema, distinct
        # from GHC's (still-placeholder) dbo.Customers lookup.
        return self._db_for(env_key).lookup_upc_consumer_by_phone(phone)

    # ----- Order Validation (UPC-only feature; not part of the OrderModule
    # base interface since GHC/OMS/call-center don't have this yet) -----

    def search_orders(
        self, filters: Dict[str, Any], env_key: Optional[str] = None
    ) -> List[Dict[str, Any]]:
        return self._db_for(env_key).search_upc_orders(filters)

    def get_order_details(
        self,
        order_number: str,
        env_key: Optional[str] = None,
        order_header_id: Optional[int] = None,
    ) -> Optional[Dict[str, Any]]:
        return self._db_for(env_key).get_upc_order_details(
            order_number, order_header_id=order_header_id
        )

    def get_latest_request_json(
        self, order_number: str, env_key: Optional[str] = None
    ) -> Optional[str]:
        return self._db_for(env_key).get_latest_request_json(order_number)


MODULE = UpcEcommerceModule()
