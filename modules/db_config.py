"""Per-module database connection scaffolding.

Real credentials are not available yet. Each module gets its own env-var-prefixed
config block, mirroring the shape of the previous single global DB_CONFIG in
config.py, so wiring in real credentials later is a config-only change.
"""

import os


def _db_config(prefix: str, default_database: str = "RMSCashierSrv") -> dict:
    return {
        "server": os.environ.get(f"{prefix}_DB_SERVER", "."),
        "database": os.environ.get(f"{prefix}_DB_DATABASE", default_database),
        "username": os.environ.get(f"{prefix}_DB_USERNAME", "sa"),
        "password": os.environ.get(f"{prefix}_DB_PASSWORD", "P@ssw0rd"),
        "driver": os.environ.get(f"{prefix}_DB_DRIVER", "ODBC Driver 17 for SQL Server"),
    }


# TODO(db-creds): confirm real server/database/credentials per module with the user.
DB_CONFIGS = {
    "ghc_ecommerce": _db_config("GHC_ECOM"),
    "upc_ecommerce": _db_config("UPC_ECOM"),
    "ghc_unicommerce": _db_config("GHC_UNICOM"),
}
