

Session 1 — Scaffolding: module registry & config restructuring

Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context.
This is session 1 of 8 implementing that plan. Do ONLY the "Target architecture" scaffolding piece — no routes, no frontend yet.

1. Create a modules/ package: modules/__init__.py (MODULE_REGISTRY dict), modules/base.py (an OrderModule ABC / dataclass defining: key, label, environments with api_url/cancel_url/db_config per environment, available flag, default_state(), plus abstract hooks for serializer/validator/db lookups).
2. Create module definition files for all 5 modules described in the plan: ghc_ecommerce.py, upc_ecommerce.py, ghc_unicommerce.py, oms.py (available=False stub), call_center.py (available=False stub). For now these can have placeholder/minimal serializer & validator bodies (raise NotImplementedError) — the real logic comes in sessions 2-3. Port the existing CLIENT_ENDPOINTS entries from config.py (UPC Production/Testing -> upc_ecommerce, GHC Production/Testing -> ghc_ecommerce) into these module definitions' environments. Drop/replace the old "Whites UniCommerce" placeholder entries with the real ghc_unicommerce module (available=True, but api_url/db config left as None/placeholder since real URL isn't finalized in config yet — check config.py for whether a Uni-Commerce URL already exists before assuming it doesn't).
3. Add DB_CONFIGS scaffolding in config.py (or a new modules/db_config.py): one config block per module, sourced from module-prefixed env vars (GHC_ECOM_DB_*, UPC_ECOM_DB_*, GHC_UNICOM_DB_*), same shape as the existing DB_CONFIG dict in config.py, with the same defaults/placeholders pattern.
4. Do NOT touch app.py routes, index.html, or script.js yet — this session is additive scaffolding only. The existing app must still run exactly as before (import the new modules/ package but don't wire it into any route).
5. Verify: `python -c "from modules import MODULE_REGISTRY; print(MODULE_REGISTRY.keys())"` succeeds, and `python app.py` still starts and serves the app unchanged.

Report what you built and any deviations from the plan you had to make (e.g. if config.py already had partial Uni-Commerce config you didn't expect).
