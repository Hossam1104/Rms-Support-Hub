"""OMS module — coming soon.

No schema/URL/DB details exist yet. This is a pure placeholder so the module
picker can render a disabled "coming soon" card; nothing here is implemented
until specs are provided.
"""

from typing import Any, Dict, List, Optional

from .base import OrderModule


class OmsModule(OrderModule):
    key = "oms"
    label = "OMS"
    client = "OMS"
    available = False

    def __init__(self):
        self.environments: Dict[str, Any] = {}

    def default_state(self) -> Dict[str, Any]:
        raise NotImplementedError("OmsModule: coming soon, not yet specified")

    def build_payload(self, state: Dict[str, Any]) -> Dict[str, Any]:
        raise NotImplementedError("OmsModule: coming soon, not yet specified")

    def validate(self, payload: Dict[str, Any]) -> List[str]:
        raise NotImplementedError("OmsModule: coming soon, not yet specified")

    def lookup_item(self, code: str, **filters) -> Dict[str, Any]:
        raise NotImplementedError("OmsModule: coming soon, not yet specified")

    def lookup_consumer_by_phone(self, phone: str) -> Optional[Dict[str, Any]]:
        raise NotImplementedError("OmsModule: coming soon, not yet specified")


MODULE = OmsModule()
