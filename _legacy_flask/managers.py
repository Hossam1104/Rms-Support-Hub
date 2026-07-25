import logging
import json
import os
import copy
import requests
import socket
from urllib.parse import urlparse
from typing import Dict, Any, Optional
from flask import session

logger = logging.getLogger(__name__)


class ValidationError(Exception):
    """Custom validation error"""

    pass


class OrderManager:
    """Module-aware session & persistence layer for the multi-module tool.

    Keeps one session['modules'][module_key] draft + one last_order_<module_key>.json
    autosave file per module. The old global DatabaseManager / ProductCalculator and
    the legacy single-schema prepare_order_data / validate_order_data /
    save_last_order methods were removed in the session-8 cleanup — that flat-order
    serializer/DB logic now lives in modules/flat_order.py.
    """

    _module_draft_cache: Dict[str, Any] = {}
    _module_draft_mtime: Dict[str, float] = {}

    @classmethod
    def _module_draft_path(cls, module_key: str) -> str:
        return f"last_order_{module_key}.json"

    @classmethod
    def load_module_draft(cls, module_key: str) -> Optional[Dict[str, Any]]:
        """Load a module's autosaved draft from last_order_<module_key>.json, if present."""
        path = cls._module_draft_path(module_key)

        if not os.path.exists(path):
            cls._module_draft_cache.pop(module_key, None)
            cls._module_draft_mtime.pop(module_key, None)
            return None

        file_mtime = os.path.getmtime(path)
        if (
            module_key not in cls._module_draft_cache
            or cls._module_draft_mtime.get(module_key) != file_mtime
        ):
            with open(path, "r") as f:
                cls._module_draft_cache[module_key] = json.load(f)
            cls._module_draft_mtime[module_key] = file_mtime

        return copy.deepcopy(cls._module_draft_cache[module_key])

    @classmethod
    def save_module_draft(cls, module_key: str, state: Dict[str, Any]):
        """Persist a module's draft to its own last_order_<module_key>.json."""
        try:
            path = cls._module_draft_path(module_key)
            with open(path, "w") as f:
                json.dump(state, f, indent=2)
            cls._module_draft_cache[module_key] = copy.deepcopy(state)
            cls._module_draft_mtime[module_key] = os.path.getmtime(path)
        except Exception as e:
            logger.error(f"Error saving draft for module {module_key}: {str(e)}")

    @staticmethod
    def initialize_session_data():
        """Ensure the module-aware session skeleton exists, lazily seeding the
        active module's draft from its autosave file or default_state()."""
        from modules import DEFAULT_MODULE_KEY, MODULE_REGISTRY

        try:
            session_changed = False

            if "modules" not in session:
                session["modules"] = {}
                session_changed = True

            if "active_module" not in session:
                session["active_module"] = DEFAULT_MODULE_KEY
                session_changed = True

            if "active_environment" not in session:
                session["active_environment"] = None
                session_changed = True

            if "client_selected" not in session:
                session["client_selected"] = False
                session_changed = True

            active_module_key = session["active_module"]
            if active_module_key not in MODULE_REGISTRY:
                active_module_key = DEFAULT_MODULE_KEY
                session["active_module"] = active_module_key
                session_changed = True

            modules_state = session["modules"]
            if active_module_key not in modules_state:
                module = MODULE_REGISTRY[active_module_key]
                draft = OrderManager.load_module_draft(active_module_key)
                modules_state[active_module_key] = (
                    draft if draft else module.default_state()
                )
                session["modules"] = modules_state
                session_changed = True

            if session_changed:
                session.modified = True

        except Exception as e:
            logger.error(f"Error initializing session data: {str(e)}")
            session.clear()
            module = MODULE_REGISTRY[DEFAULT_MODULE_KEY]
            session.update(
                {
                    "modules": {DEFAULT_MODULE_KEY: module.default_state()},
                    "active_module": DEFAULT_MODULE_KEY,
                    "active_environment": None,
                    "client_selected": False,
                }
            )
            session.modified = True


class APIManager:
    """API communication management"""

    @staticmethod
    def send_order(url: str, order_data: Dict[str, Any]) -> Dict[str, Any]:
        """Send order data to API endpoint"""
        try:
            # Disable warnings for unverified HTTPS requests
            import urllib3

            urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

            headers = {"Content-Type": "application/json", "Accept": "application/json"}
            response = requests.post(
                url, json=order_data, headers=headers, timeout=30, verify=False
            )

            return {
                "status_code": response.status_code,
                "response_text": response.text,
                "url_sent": url,
                "success": response.status_code in [200, 201],
            }
        except requests.exceptions.RequestException as e:
            logger.error(f"API request failed: {str(e)}")
            raise

    @staticmethod
    def test_endpoint(url: str) -> Dict[str, Any]:
        """Test API endpoint connectivity"""
        try:
            parsed = urlparse(url)
            host = parsed.hostname
            port = parsed.port or (80 if parsed.scheme == "http" else 443)

            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(3)
            result = sock.connect_ex((host, port))
            sock.close()

            return {"status": "Online" if result == 0 else "Offline", "url": url}
        except Exception as e:
            return {"status": "Error", "error": str(e)}
