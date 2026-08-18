# Rollback

Rollback is an operator-controlled IIS action and is not executed by the
release-candidate pipeline.

1. Stop the approved application pool.
2. Preserve the failed package and its logs as evidence.
3. Restore the previous application-directory backup as a complete directory,
   including its compatible server-owned configuration.
4. Confirm `var/drafts` exists and retains the required Modify ACL.
5. Start the application pool.
6. Run the liveness, readiness, root, module-catalogue, deep-link, build
   identity, and static-asset checks in `SMOKE.md`.
7. Confirm the restored build identity and record the rollback outcome.

Do not delete the only backup before the restored package has passed smoke.
Do not use rollback as permission to contact Production, mutate customer data,
or send/cancel/resend an order.
