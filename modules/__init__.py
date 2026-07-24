"""Module registry: one entry per client integration.

app.py (from session 4 onward) dispatches to MODULE_REGISTRY[module_key]
instead of hard-coding a single global schema/DB/URL for every client.
"""

from .call_center import MODULE as CALL_CENTER
from .ghc_ecommerce import MODULE as GHC_ECOMMERCE
from .ghc_unicommerce import MODULE as GHC_UNICOMMERCE
from .oms import MODULE as OMS
from .upc_ecommerce import MODULE as UPC_ECOMMERCE

MODULE_REGISTRY = {
    UPC_ECOMMERCE.key: UPC_ECOMMERCE,
    GHC_ECOMMERCE.key: GHC_ECOMMERCE,
    GHC_UNICOMMERCE.key: GHC_UNICOMMERCE,
    OMS.key: OMS,
    CALL_CENTER.key: CALL_CENTER,
}

# Reverse lookup: historical CLIENT_ENDPOINTS-style environment key -> owning module key.
# e.g. "GHC Production" -> "ghc_ecommerce", "UPC Testing" -> "upc_ecommerce".
ENV_TO_MODULE = {
    env_key: module.key
    for module in MODULE_REGISTRY.values()
    for env_key in module.environments
}

DEFAULT_MODULE_KEY = "ghc_ecommerce"
DEFAULT_ENVIRONMENT_KEY = "GHC Testing"

__all__ = [
    "MODULE_REGISTRY",
    "ENV_TO_MODULE",
    "DEFAULT_MODULE_KEY",
    "DEFAULT_ENVIRONMENT_KEY",
]
