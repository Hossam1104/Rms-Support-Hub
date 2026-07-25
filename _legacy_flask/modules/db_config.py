"""Per-module database connection scaffolding.

Real credentials are not available yet. Each module gets its own env-var-prefixed
config block, mirroring the shape of the previous single global DB_CONFIG in
config.py, so wiring in real credentials later is a config-only change.
"""

import os


def _db_config(
    prefix: str, default_database: str = "RMSCashierSrv", default_server: str = "."
) -> dict:
    return {
        "server": os.environ.get(f"{prefix}_DB_SERVER", default_server),
        "database": os.environ.get(f"{prefix}_DB_DATABASE", default_database),
        "username": os.environ.get(f"{prefix}_DB_USERNAME", "sa"),
        "password": os.environ.get(f"{prefix}_DB_PASSWORD", ""),
        "driver": os.environ.get(f"{prefix}_DB_DRIVER", "ODBC Driver 17 for SQL Server"),
    }


# UPC is the one module where Production and Testing point at two different
# databases on the same server/credentials (confirmed with the user), rather
# than sharing a single db_config like every other module below.
UPC_DB_CONFIGS = {
    "UPC Production": _db_config(
        "UPC_ECOM_PROD", default_database="RmsMainProd", default_server="10.10.8.181"
    ),
    "UPC Testing": _db_config(
        "UPC_ECOM_TEST", default_database="RmsMainTest2", default_server="10.10.8.181"
    ),
}

# TODO(db-creds): confirm real server/database/credentials per module with the user.
DB_CONFIGS = {
    "ghc_ecommerce": _db_config("GHC_ECOM"),
    # Back-compat alias for any generic module_key-keyed lookup (e.g. the
    # Database Connection tab's display defaults): resolves to the Testing
    # config. Per-environment lookups must use UPC_DB_CONFIGS instead.
    "upc_ecommerce": UPC_DB_CONFIGS["UPC Testing"],
    "ghc_unicommerce": _db_config("GHC_UNICOM"),
}
