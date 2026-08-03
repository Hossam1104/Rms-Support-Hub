import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Product } from '../../../core/models';

/** Outcome of the parent's item lookup (U4, UI_Rework_Plan.md D2). The
 * dialog stays dumb about HTTP -- the parent owns module/env/branch state
 * and pushes the typed result back in, so a successful lookup populates the
 * form instead of being discarded as a toast. */
export type ItemLookupOutcome =
  | { status: 'found'; product: Product }
  | { status: 'not-found'; code: string }
  | { status: 'error' }
  | { status: 'missing-branch' };

function blankProduct(): Product {
  return {
    itemCode: '',
    itemName: '',
    itemNameAr: null,
    quantity: 1,
    unitPrice: 0,
    vatPercentage: 15,
    discount: 0
  };
}

/** Display-only net unit price (unit price including VAT), derived with the
 * project's verified convention -- Product.cs NetUnitPrice, documented as
 * net_price in the legacy lookup return shape. Never sent anywhere; the
 * server recomputes money on its side. */
function netUnitPriceOf(unitPrice: number, vatPercentage: number): number {
  const price = Number(unitPrice) || 0;
  const vat = Number(vatPercentage) || 0;
  return Math.round((price + (price * vat) / 100) * 100) / 100;
}

@Component({
  selector: 'app-add-product-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop glass-panel" (click)="close.emit()">
      <div class="modal-dialog glass-card fade-in-up" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h3><i class="bi bi-box-seam"></i> Add Product</h3>
          <button type="button" class="btn-close" (click)="close.emit()">&times;</button>
        </div>

        <div class="modal-body">
          <div class="search-bar mb-3">
            <div class="input-group">
              <input type="text" class="glass-input" [(ngModel)]="searchCode" placeholder="Enter item/material number..." maxlength="18" [disabled]="!branchCode.trim()" />
              <button type="button" class="glass-button" [disabled]="!branchCode.trim()" (click)="onItemLookup()"><i class="bi bi-search"></i> Find Item</button>
            </div>
            <p class="branch-hint" *ngIf="!branchCode.trim()">Choose a branch in Order Header Information first — item pricing is branch-specific.</p>
            <p class="branch-selected" *ngIf="branchCode.trim()">Using branch: {{ branchName || branchCode }}</p>
            <p class="lookup-message" [class.lookup-error]="lookupMessage?.kind === 'error'" *ngIf="lookupMessage">{{ lookupMessage.text }}</p>
          </div>

          <div class="form-grid">
            <div class="form-group">
              <label class="form-label">Item Code * <span class="db-badge" *ngIf="dbFilled.has('itemCode')" title="Filled from the database lookup">DB</span></label>
              <input type="text" class="glass-input" [(ngModel)]="product.itemCode" (ngModelChange)="onFieldEdit('itemCode')" required />
            </div>
            <div class="form-group">
              <label class="form-label">Item Name * <span class="db-badge" *ngIf="dbFilled.has('itemName')" title="Filled from the database lookup">DB</span></label>
              <input type="text" class="glass-input" [(ngModel)]="product.itemName" (ngModelChange)="onFieldEdit('itemName')" required />
            </div>
            <div class="form-group">
              <label class="form-label">Item Name (Arabic) <span class="db-badge" *ngIf="dbFilled.has('itemNameAr')" title="Filled from the database lookup">DB</span></label>
              <input type="text" class="glass-input" [(ngModel)]="product.itemNameAr" (ngModelChange)="onFieldEdit('itemNameAr')" dir="auto" />
            </div>
            <div class="form-group">
              <label class="form-label">Quantity *</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.quantity" required />
            </div>
            <div class="form-group">
              <label class="form-label">Unit Price (SAR) * <span class="db-badge" *ngIf="dbFilled.has('unitPrice')" title="Filled from the database lookup">DB</span></label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.unitPrice" (ngModelChange)="onFieldEdit('unitPrice')" required />
            </div>
            <div class="form-group">
              <label class="form-label">VAT % <span class="db-badge" *ngIf="dbFilled.has('vatPercentage')" title="Filled from the database lookup">DB</span></label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.vatPercentage" (ngModelChange)="onFieldEdit('vatPercentage')" />
            </div>
            <div class="form-group">
              <label class="form-label">Discount (SAR)</label>
              <input type="number" step="0.01" class="glass-input" [(ngModel)]="product.discount" />
            </div>
            <div class="form-group">
              <label class="form-label">Net Unit Price (incl. VAT)</label>
              <div class="net-price" data-testid="net-unit-price">{{ netUnitPrice() | number:'1.2-2' }} SAR</div>
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <button type="button" class="btn-secondary glass-input" (click)="close.emit()">Cancel</button>
          <button type="button" class="glass-button" (click)="onAdd()">Add Product</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 2000; display: flex; align-items: center; justify-content: center; }
    .modal-dialog { width: 100%; max-width: 600px; padding: 24px; border-radius: var(--radius-lg); }
    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
    .modal-header h3 { margin: 0; font-size: 1.2rem; display: flex; align-items: center; gap: 8px; }
    .btn-close { background: none; border: none; font-size: 1.5rem; color: var(--text-muted); cursor: pointer; }
    .input-group { display: flex; gap: 8px; }
    .input-group input { flex: 1; }
    .branch-hint { font-size: 0.78rem; color: var(--warning); margin: 8px 0 0; }
    .branch-selected { font-size: 0.78rem; color: var(--text-secondary); margin: 8px 0 0; }
    .lookup-message { font-size: 0.78rem; color: var(--text-secondary); margin: 8px 0 0; }
    .lookup-message.lookup-error { color: var(--danger); }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-top: 16px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .db-badge {
      font-size: 0.62rem; font-weight: 700; letter-spacing: 0.04em;
      color: var(--primary); border: 1px solid var(--primary);
      border-radius: var(--radius-pill); padding: 0 5px; margin-left: 4px;
      vertical-align: middle;
    }
    .net-price {
      padding: 10px 12px; border-radius: var(--radius-sm);
      background: var(--bg-tertiary); color: var(--text-primary);
      font-weight: 600; min-height: 40px; display: flex; align-items: center;
    }
    .modal-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 24px; }
  `]
})
export class AddProductDialogComponent implements OnChanges {
  @Input() moduleKey: string = '';
  @Input() branchCode: string = '';
  @Input() branchName: string = '';
  @Input() lookupOutcome: ItemLookupOutcome | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<Product>();
  @Output() lookupItem = new EventEmitter<{ code: string, branchCode: string }>();

  searchCode: string = '';
  product: Product = blankProduct();

  /** Fields the last successful lookup populated -- shown with a DB badge
   * but kept editable; the badge clears as soon as the operator edits. */
  dbFilled = new Set<string>();
  lookupMessage: { kind: 'info' | 'error', text: string } | null = null;

  ngOnChanges(changes: SimpleChanges) {
    const outcome = changes['lookupOutcome']?.currentValue as ItemLookupOutcome | null | undefined;
    if (!outcome) return;

    switch (outcome.status) {
      case 'found':
        this.applyFoundProduct(outcome.product);
        break;
      case 'not-found':
        this.applyNotFound(outcome.code);
        break;
      case 'missing-branch':
        this.lookupMessage = {
          kind: 'info',
          text: 'Select a branch in Order Header Information first — item pricing is branch-specific.'
        };
        break;
      case 'error':
        // Infrastructure failure (DB/network) is toasted once by
        // errorEnvelopeInterceptor; the inline note only states that nothing
        // was overwritten -- never presented as "not found".
        this.lookupMessage = {
          kind: 'error',
          text: 'Item lookup failed (network or database error). Your entered values were kept.'
        };
        break;
    }
  }

  /** Populates every verified value the lookup returned and marks the
   * filled fields. Quantity/discount are the operator's and stay put. */
  private applyFoundProduct(found: Product) {
    this.dbFilled.clear();
    this.lookupMessage = null;

    this.product = {
      ...this.product,
      itemCode: found.itemCode ?? '',
      itemName: found.itemName ?? '',
      itemNameAr: found.itemNameAr ?? null,
      unitPrice: found.unitPrice ?? 0,
      vatPercentage: found.vatPercentage ?? 0
    };

    if (this.product.itemCode) this.dbFilled.add('itemCode');
    if (this.product.itemName) this.dbFilled.add('itemName');
    if (this.product.itemNameAr) this.dbFilled.add('itemNameAr');
    this.dbFilled.add('unitPrice');
    this.dbFilled.add('vatPercentage');
  }

  /** A not-found result must not leave a previous item's values behind --
   * reset to blank, keeping only the searched code for manual entry. */
  private applyNotFound(code: string) {
    this.dbFilled.clear();
    this.product = { ...blankProduct(), itemCode: code };
    this.lookupMessage = {
      kind: 'info',
      text: `Item ${code} was not found in the database. You can still enter it manually.`
    };
  }

  onFieldEdit(field: string) {
    this.dbFilled.delete(field);
  }

  netUnitPrice(): number {
    return netUnitPriceOf(this.product.unitPrice, this.product.vatPercentage);
  }

  onItemLookup() {
    if (!this.branchCode.trim()) {
      this.lookupMessage = {
        kind: 'info',
        text: 'Select a branch in Order Header Information first — item pricing is branch-specific.'
      };
      return;
    }
    if (!this.searchCode.trim()) {
      this.lookupMessage = { kind: 'info', text: 'Enter an item/material code first.' };
      return;
    }
    this.clearStaleLookupValues(this.searchCode.trim());
    this.lookupMessage = null;
    this.lookupItem.emit({ code: this.searchCode.trim(), branchCode: this.branchCode });
  }

  /** Keep operator edits, but remove values that are still marked as coming
   * from the previous database lookup while the new lookup is in flight. */
  private clearStaleLookupValues(nextCode: string) {
    const nextProduct = { ...this.product };

    if (this.dbFilled.has('itemCode')) nextProduct.itemCode = nextCode;
    if (this.dbFilled.has('itemName')) nextProduct.itemName = '';
    if (this.dbFilled.has('itemNameAr')) nextProduct.itemNameAr = null;
    if (this.dbFilled.has('unitPrice')) nextProduct.unitPrice = 0;
    if (this.dbFilled.has('vatPercentage')) nextProduct.vatPercentage = 15;

    this.product = nextProduct;
    this.dbFilled.clear();
  }

  onAdd() {
    if (this.product.itemCode && this.product.itemName && this.product.quantity > 0) {
      this.add.emit({ ...this.product });
    }
  }
}
