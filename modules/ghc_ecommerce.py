"""GHC E-Commerce module.

Schema/serializer/validator/DB manager live in modules/flat_order.py, shared with
UpcEcommerceModule since both clients use the identical JSON schema — only the
environments (URLs/DB config) differ here.
"""

from typing import Any, Dict, List, Optional

from config import CLIENT_LOGOS

from . import flat_order
from .base import ModuleEnvironment, OrderModule
from .db_config import DB_CONFIGS


class GhcEcommerceModule(OrderModule):
    key = "ghc_ecommerce"
    label = "GHC E-Commerce"
    client = "GHC"
    available = True

    def __init__(self):
        db_config = DB_CONFIGS["ghc_ecommerce"]
        self.environments: Dict[str, ModuleEnvironment] = {
            "GHC Production": ModuleEnvironment(
                key="GHC Production",
                environment="Production",
                description="GHC live routing.",
                accent="ember",
                cue="Warehouse",
                icon="bi-box-seam",
                route_label="Live lane",
                visual_url=CLIENT_LOGOS["GHC"],
                visual_alt="GHC logo",
                available=True,
                api_url="https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CreateAndAssignOrder",
                cancel_url="https://10.10.20.200/Gateway/RmsMainServerApi/api/Order/CancelOrder",
                db_config=db_config,
            ),
            "GHC Testing": ModuleEnvironment(
                key="GHC Testing",
                environment="Testing",
                description="GHC QA routing.",
                accent="ocean",
                cue="Dispatch",
                icon="bi-truck",
                route_label="QA lane",
                visual_url=CLIENT_LOGOS["GHC"],
                visual_alt="GHC logo",
                available=True,
                api_url="http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder",
                cancel_url="http://10.10.20.126:8090/RmsMainServerApi/api/Order/CancelOrder",
                db_config=db_config,
            ),
        }
        self._db = flat_order.FlatOrderDatabaseManager(db_config)

    def default_state(self) -> Dict[str, Any]:
        return flat_order.default_state()

    def build_payload(self, state: Dict[str, Any]) -> Dict[str, Any]:
        return flat_order.build_payload(state)

    def validate(self, payload: Dict[str, Any]) -> List[str]:
        return flat_order.validate(payload)

    def lookup_item(self, code: str, **filters) -> Dict[str, Any]:
        return self._db.lookup_item(code, **filters)

    def lookup_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        return self._db.lookup_consumer_by_phone(phone)


MODULE = GhcEcommerceModule()
