import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from '../../core/services/api.service';
import { OrderDraft } from '../../core/models';

const PATCH_DEBOUNCE_MS = 300;

function emptyDraft(): OrderDraft {
  return { orderData: {}, products: [], payments: [], consumer: {}, delivery: { deliveryFees: 0 }, rowItems: [] };
}

/**
 * Route-scoped signal store owning the flat-order draft (U2,
 * UI_Rework_Plan.md D1/D9). `patch()` is the only way callers mutate
 * `orderData`: it applies the fields to local state immediately, then
 * debounces ~300ms and coalesces every pending field -- across as many
 * `patch()` calls as arrive in that window -- into one
 * `PATCH modules/{key}/order-data` request. The server response is never
 * assigned back into local state; local edits are authoritative until an
 * explicit full reload (`setDraft`, driven by `GET state`, `load-default` or
 * `clear-all`) replaces the draft wholesale.
 *
 * This is what closes the D1 lost-update race: the old per-field
 * `PUT order-field` path fired one request per keystroke and re-assigned
 * `this.draft.set(res.state)` on every response, so a consumer lookup's
 * eight parallel requests raced and late responses built from stale reads
 * clobbered fields a later request had already written.
 *
 * `products`/`payments` mutation stays on their own dedicated endpoints
 * (ProductController/PaymentController) -- out of U2 scope -- so
 * `updateLocal` exists only to let the component keep those slices of the
 * same draft signal in sync after those calls succeed.
 */
@Injectable()
export class DraftStore {
  private api = inject(ApiService);

  private moduleKey = '';
  draft = signal<OrderDraft>(emptyDraft());
  saving = signal(false);

  /** Incremented every time a PATCH order-data flush settles (success or
   * failure -- the server draft is then known to be current or the fields
   * have rejoined the pending batch). U4 totals refresh hooks onto this so
   * GET calculate-totals never races ahead of the PATCH it depends on. */
  flushVersion = signal(0);

  private pendingFields: Record<string, unknown> = {};
  private debounceHandle: ReturnType<typeof setTimeout> | null = null;
  private sendInFlight = false;

  setModuleKey(key: string) {
    if (this.moduleKey !== key) this.cancelPending();
    this.moduleKey = key;
  }

  /** Full authoritative replacement of the draft -- the only path allowed
   * to overwrite local edits wholesale (GET state / load-default / clear-all). */
  setDraft(draft: OrderDraft) {
    this.cancelPending();
    this.draft.set(draft);
  }

  /** Non-order-data slices (products/payments) are owned by their own
   * endpoints; this keeps them in sync on the same draft signal. */
  updateLocal(mutator: (draft: OrderDraft) => OrderDraft) {
    this.draft.update(mutator);
  }

  /** Merges every field into local `orderData` immediately and schedules
   * one coalesced PATCH. Safe to call repeatedly in a row -- a consumer
   * lookup calls this once with every prefilled field; per-field edits call
   * it once per keystroke and are coalesced by the debounce. */
  patch(fields: Record<string, unknown>) {
    if (!fields || Object.keys(fields).length === 0) return;

    this.draft.update(d => ({ ...d, orderData: { ...d.orderData, ...fields } }));
    this.pendingFields = { ...this.pendingFields, ...fields };

    if (this.debounceHandle) clearTimeout(this.debounceHandle);
    this.debounceHandle = setTimeout(() => this.flush(), PATCH_DEBOUNCE_MS);
  }

  /** Sends the coalesced batch immediately, bypassing the debounce window --
   * used before a validate/send/preview so the server draft is current. */
  flushNow() {
    if (this.debounceHandle) {
      clearTimeout(this.debounceHandle);
      this.debounceHandle = null;
    }
    this.flush();
  }

  private cancelPending() {
    if (this.debounceHandle) {
      clearTimeout(this.debounceHandle);
      this.debounceHandle = null;
    }
    this.pendingFields = {};
  }

  private flush() {
    this.debounceHandle = null;
    if (Object.keys(this.pendingFields).length === 0) return;
    // An overlapping send is already in flight; its own completion handler
    // re-flushes whatever accumulated in pendingFields meanwhile, so the
    // later edits always win without two concurrent PATCH calls racing.
    if (this.sendInFlight) return;

    const fields = this.pendingFields;
    this.pendingFields = {};
    this.sendInFlight = true;
    this.saving.set(true);

    const key = this.moduleKey;
    this.api.patch(`modules/${key}/order-data`, { fields }).subscribe({
      next: () => this.onSendSettled(),
      // errorEnvelopeInterceptor already surfaces the failure via a toast.
      // The fields stay applied locally and rejoin the next batch (newer
      // edits take precedence) so a transient failure never silently drops
      // an edit.
      error: () => {
        this.pendingFields = { ...fields, ...this.pendingFields };
        this.onSendSettled();
      }
    });
  }

  private onSendSettled() {
    this.sendInFlight = false;
    this.saving.set(false);
    this.flushVersion.update(v => v + 1);
    if (Object.keys(this.pendingFields).length > 0) this.flush();
  }
}
