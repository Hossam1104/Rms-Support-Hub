
- Read [`remediation_plan.md`](remediation_plan.md) in full before touching anything, especially §2 (defect register) and §3 (guiding decisions).
- **Never invent a SQL column name or a JSON key.** The only two sources of truth are `docs/Prompts/UPC_Enhancments_Plan.md` §"Schema discovery" and `docs/request_examples/**`. `_legacy_flask/modules/flat_order.py` is the behavioural reference. If something is not in one of those, stop and ask.
- Work only inside the files listed for the session.
- Run the verification block and paste its real output before declaring done. Do not claim success from a build alone.
- End with `dotnet build` clean, `dotnet test` green, and a single clean commit using the given message.
- Shell is PowerShell on Windows, from the repo root `d:\AI Tools\DBS\online_order_tool`.

---

## Session R0 — Ground truth & safety net

The .NET 10 + Angular 22 rewrite is complete but its core is wrong: the payload schema was invented, the SQL columns do not exist, and the 21 passing tests validate the invention rather than the contract. This session changes **no production logic**. It commits the migration cleanly, removes secrets, deletes scaffolding, corrects the misleading schema doc, and — most importantly — introduces the contract tests that must **fail** so the following sessions have a real gate.

Read `remediation_plan.md` §2.1, §2.7 and §3 first. This is session 1 of 11.

1. Commit the pending migration as-is so remediation starts from a clean tree. `git status --short` currently shows ~102 entries mixing staged renames with unstaged deletes: stage everything, verify no secrets are being added beyond what is already tracked, and commit as a checkpoint.
2. Purge credentials from tracked files. Replace the four connection strings in `backend/src/OnlineOrderTool.Api/appsettings.json` with empty placeholders. Remove the hardcoded `DbConnectionConfig` literals (`sa` / `<redacted-password>` / `10.10.8.181`) from `UpcEcommerceModule.cs`, `GhcEcommerceModule.cs` and `GhcUnicommerceModule.cs`. Move real values to .NET user-secrets (`dotnet user-secrets init` + `set`) and document the keys in a new tracked `.env.example`-style section in `README.md`. Add a startup guard that throws a clear `ConfigurationException` naming the missing key rather than failing inside Dapper. Fix `.gitignore`: stop ignoring `appsettings.Development.json` as if it were the secret-bearing file, and ignore `appsettings.Local.json`, `var/`, `order_history_*.json`, `last_order_*.json`.
3. Delete scaffolding: `WeatherForecastController.cs`, `WeatherForecast.cs`, `OnlineOrderTool.Core/Class1.cs`, `OnlineOrderTool.Data/Class1.cs`, `tests/OnlineOrderTool.Tests/UnitTest1.cs`, `OnlineOrderTool.Api.http`.
4. Upgrade `Microsoft.OpenApi` past the NU1903 advisory in both `OnlineOrderTool.Api.csproj` and `OnlineOrderTool.Tests.csproj`.
5. Copy `docs/request_examples/**` to `backend/tests/fixtures/payloads/` and mark them `CopyToOutputDirectory`. Exclude the two `xx`-prefixed UPC files (superseded).
6. Add `backend/tests/OnlineOrderTool.Tests/ContractTests.cs`:
   - A key-for-key test that builds a GHC payload from a draft equivalent to `fixtures/payloads/GHC E-Commerce/request_body.json` and asserts the **exact top-level key set** matches, plus the key sets of `order_products[0]` and `payment_methods_with_options[0]`. On mismatch the failure message must print `missing` and `unexpected` key lists — the diff is the working spec for R1.
   - The same for UPC against `fixtures/payloads/UPC/4- Invoice without discount, with delivery and paid by visa.json`.
   - A SQL sanity test that reflects over `UpcOrderValidationRepository` (or reads its source file) and asserts none of these appear: `H.Status`, `CreatedDateTime`, `CustomerMobile`, `CustomerName`, `ShippingAddress`, `H.Notes`, `UpdatedDateTime`, `I.OrderNumber`, `ItemCode`, `DiscountAmount`, `VatAmount`, `LineTotal`, `TransactionId`.
   - Mark all of them `[Fact]` — they are **expected to fail now**. Do not add `Skip`.
7. Rewrite `docs/database-schema.md`. Delete the invented queries and replace them with the verified schema copied from `docs/Prompts/UPC_Enhancments_Plan.md` §"Schema discovery", plus the four real queries lifted verbatim from `_legacy_flask/modules/flat_order.py` (`lookup_item`, `lookup_upc_item`, `lookup_upc_consumer_by_phone`, `search_upc_orders`). Add a header stating this file is the SQL contract and that no query may deviate from it.

**Verify** (paste real output):
```powershell
dotnet build backend/OnlineOrderTool.slnx --nologo   # 0 errors, 0 NU1903 warnings
dotnet test  backend/OnlineOrderTool.slnx --nologo   # existing tests green, 3 NEW contract tests FAILING
git grep -in "password="   # expect no source hits (checks tracked file CONTENTS for any inline-password connection string)
git status --short                                    # clean
```
Report the exact missing/unexpected key lists printed by the two payload tests — R1 depends on them.

**Commit:** `chore(r0): clean migration, purge secrets, remove scaffolding, add failing contract tests`