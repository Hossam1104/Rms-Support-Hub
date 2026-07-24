"""Shared types for the per-client module system.

Each client integration (GHC E-Commerce, UPC E-Commerce, GHC Uni-Commerce, ...)
is modeled as an OrderModule: its own Production/Testing environments (send/cancel
URLs + DB config), its own default session-draft shape, and its own
serializer/validator/DB-lookup hooks. app.py will dispatch to the active module
instead of hard-coding one schema for every client.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Any, Dict, List, Optional


class ValidationError(Exception):
    """Raised by a module's lookup_item/lookup_consumer_by_phone for bad input.

    Shared across all modules so app.py can catch one type regardless of which
    module raised it.
    """


@dataclass
class ModuleEnvironment:
    """One Production/Testing lane for a module (mirrors config.CLIENT_ENDPOINTS entries)."""

    key: str  # historical CLIENT_ENDPOINTS-style key, e.g. "GHC Production"
    environment: str  # "Production" | "Testing"
    description: str
    accent: str
    cue: str
    icon: str
    route_label: str
    visual_url: str
    visual_alt: str
    available: bool
    api_url: Optional[str]
    cancel_url: Optional[str]
    db_config: Optional[Dict[str, Any]] = None

    @property
    def status_label(self) -> str:
        if self.available and self.environment == "Production":
            return "Live"
        if self.available:
            return "Test"
        return "Soon"


class OrderModule(ABC):
    """Base class every client module implements."""

    key: str
    label: str
    client: str
    available: bool
    environments: Dict[str, ModuleEnvironment]

    def get_environment(self, env_key: Optional[str] = None) -> ModuleEnvironment:
        """Resolve an environment by its key, falling back to the first available one."""
        if env_key and env_key in self.environments:
            return self.environments[env_key]
        for env in self.environments.values():
            if env.available:
                return env
        return next(iter(self.environments.values()))

    @abstractmethod
    def default_state(self) -> Dict[str, Any]:
        """Blank/default session-draft shape for this module."""
        raise NotImplementedError

    @abstractmethod
    def build_payload(self, state: Dict[str, Any]) -> Dict[str, Any]:
        """Turn a session draft into the exact JSON this module's API expects."""
        raise NotImplementedError

    @abstractmethod
    def validate(self, payload: Dict[str, Any]) -> List[str]:
        """Return a list of validation error strings (empty list if valid)."""
        raise NotImplementedError

    @abstractmethod
    def lookup_item(
        self, code: str, env_key: Optional[str] = None, **filters
    ) -> Dict[str, Any]:
        """Look up an item/material by code in this module's own DB.

        env_key selects which environment's DB to query, for modules (like UPC)
        whose Production/Testing environments use different databases. Modules
        whose environments share one DB config may ignore it.
        """
        raise NotImplementedError

    @abstractmethod
    def lookup_consumer_by_phone(
        self, phone: str, env_key: Optional[str] = None
    ) -> Optional[Dict[str, Any]]:
        """Look up a consumer/customer by phone number in this module's own DB.

        See lookup_item for env_key semantics.
        """
        raise NotImplementedError
