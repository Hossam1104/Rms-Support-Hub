import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Product } from '../../../core/models';
import { UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent } from '../../../shared/ui';

/** Outcome of the parent's item lookup (U4, UI_Rework_Plan.md D2). */
export type ItemLookupOutcome =
  | { status: 'found'; product: Product }
  | { status: 'not-found'; code: string }
  | { status: 'error' }
  | { status: 'missing-branch' };

function blankProduct(): Product {
  return { itemCode: '', itemName: '', itemNameAr: null, quantity: 1, unitPrice: 0, vatPercentage: 15, discount: 0 };
}

function netUnitPriceOf(unitPrice: number, vatPercentage: number): number {
  const price = Number(unitPrice) || 0;
  const vat = Number(vatPercentage) || 0;
  return Math.round((price + (price * vat) / 100) * 100) / 100;
}

@Component({
  selector: 'app-add-product-dialog',
  standalone: true,
  imports: [CommonModule, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent],
  template: `
    <div class="modal-backdrop" (click)="close.emit()">
      <ui-card variant="raised" class="modal-dialog" (click)="$event.stopPropagation()">
        <div uiCardHeader class="modal-header">
          <h3><i class="bi bi-box-seam" aria-hidden="true"></i> Add Product</h3>
          <ui-button variant="ghost" size="sm" icon="bi-x-lg" ariaLabel="Close" (pressed)="close.emit()"></ui-button>
        </div>

        <div class="modal-body">
          <div class="search-bar">
            <div class="input-group">
              <ui-input inputId="product-lookup-code" [maxLength]="18" placeholder="Enter item/material number..." [disabled]="!branchCode.trim()" [value]="searchCode" (valueChange)="searchCode = $any($event) || ''"></ui-input>
              <ui-button variant="secondary" icon="bi-search" [disabled]="!branchCode.trim()" (pressed)="onItemLookup()">Find Item</ui-button>
            </div>
            <p class="branch-hint" *ngIf="!branchCode.trim()">Choose a branch in Order Header Information first - item pricing is branch-specific.</p>
            <p class="branch-selected" *ngIf="branchCode.trim()">Using branch: {{ branchName || branchCode }}</p>
            <p class="lookup-message" [class.lookup-error]="lookupMessage?.kind === 'error'" *ngIf="lookupMessage">{{ lookupMessage.text }}</p>
          </div>

          <div class="form-grid">
            <ui-field label="Item Code" forId="product-item-code" [required]="true" [labelStatus]="dbFilled.has('itemCode') ? 'DB' : ''">
              <ui-input inputId="product-item-code" [value]="product.itemCode" (valueChange)="setText('itemCode', $event)"></ui-input>
            </ui-field>
            <ui-field label="Item Name" forId="product-item-name" [required]="true" [labelStatus]="dbFilled.has('itemName') ? 'DB' : ''">
              <ui-input inputId="product-item-name" [value]="product.itemName" (valueChange)="setText('itemName', $event)"></ui-input>
            </ui-field>
            <ui-field label="Item Name (Arabic)" forId="product-item-name-ar" [labelStatus]="dbFilled.has('itemNameAr') ? 'DB' : ''">
              <ui-input inputId="product-item-name-ar" [value]="product.itemNameAr || ''" (valueChange)="setText('itemNameAr', $event)"></ui-input>
            </ui-field>
            <ui-field label="Quantity" forId="product-quantity" [required]="true">
              <ui-input inputId="product-quantity" type="number" step="0.01" [value]="product.quantity" (valueChange)="setNumber('quantity', $event)"></ui-input>
            </ui-field>
            <ui-field label="Unit Price (SAR)" forId="product-unit-price" [required]="true" [labelStatus]="dbFilled.has('unitPrice') ? 'DB' : ''">
              <ui-input inputId="product-unit-price" type="number" step="0.01" [value]="product.unitPrice" (valueChange)="setNumber('unitPrice', $event)"></ui-input>
            </ui-field>
            <ui-field label="VAT %" forId="product-vat" [labelStatus]="dbFilled.has('vatPercentage') ? 'DB' : ''">
              <ui-input inputId="product-vat" type="number" step="0.01" [value]="product.vatPercentage" (valueChange)="setNumber('vatPercentage', $event)"></ui-input>
            </ui-field>
            <ui-field label="Discount (SAR)" forId="product-discount">
              <ui-input inputId="product-discount" type="number" step="0.01" [value]="product.discount" (valueChange)="setNumber('discount', $event)"></ui-input>
            </ui-field>
            <ui-field label="Net Unit Price (incl. VAT)" forId="product-net-price">
              <div id="product-net-price" class="net-price" data-testid="net-unit-price">{{ netUnitPrice() | number:'1.2-2' }} SAR</div>
            </ui-field>
          </div>
        </div>

        <div uiCardFooter class="modal-footer">
          <ui-button variant="secondary" (pressed)="close.emit()">Cancel</ui-button>
          <ui-button icon="bi-plus-circle" (pressed)="onAdd()">Add Product</ui-button>
        </div>
      </ui-card>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; inset: 0; z-index: 2000; display: grid; place-items: center; padding: 16px; background: var(--backdrop); backdrop-filter: blur(3px); }
    .modal-dialog { width: min(650px, 100%); max-height: min(850px, calc(100vh - 32px)); overflow: auto; }
    .modal-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
    .modal-header h3 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 1.1rem; }
    .modal-header h3 i { color: var(--accent); }
    .input-group { display: flex; align-items: flex-end; gap: 8px; }
    .input-group ui-input { flex: 1; }
    .branch-hint, .branch-selected, .lookup-message { margin: 8px 0 0; color: var(--text-secondary); font-size: .78rem; }
    .branch-hint { color: var(--state-warning-fg); }
    .lookup-message.lookup-error { color: var(--state-danger-fg); }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; margin-top: 20px; }
    .db-badge { color: var(--text-accent); border: 1px solid var(--border-focus); border-radius: var(--radius-pill); padding: 0 5px; font-size: .62rem; font-weight: 800; }
    .net-price { display: flex; align-items: center; min-height: 42px; padding: 0 12px; border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); color: var(--text-primary); font-weight: 700; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; }
    @media (max-width: 560px) { .form-grid { grid-template-columns: 1fr; } .input-group { align-items: stretch; flex-direction: column; } .input-group ui-button { align-self: flex-start; } }
  `]
})
export class AddProductDialogComponent implements OnChanges {
  @Input() moduleKey = '';
  @Input() branchCode = '';
  @Input() branchName = '';
  @Input() lookupOutcome: ItemLookupOutcome | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<Product>();
  @Output() lookupItem = new EventEmitter<{ code: string, branchCode: string }>();

  searchCode = '';
  product: Product = blankProduct();
  dbFilled = new Set<string>();
  lookupMessage: { kind: 'info' | 'error', text: string } | null = null;

  ngOnChanges(changes: SimpleChanges) {
    const outcome = changes['lookupOutcome']?.currentValue as ItemLookupOutcome | null | undefined;
    if (!outcome) return;
    switch (outcome.status) {
      case 'found': this.applyFoundProduct(outcome.product); break;
      case 'not-found': this.applyNotFound(outcome.code); break;
      case 'missing-branch': this.lookupMessage = { kind: 'info', text: 'Select a branch in Order Header Information first - item pricing is branch-specific.' }; break;
      case 'error': this.lookupMessage = { kind: 'error', text: 'Item lookup failed (network or database error). Your entered values were kept.' }; break;
    }
  }

  private applyFoundProduct(found: Product) {
    this.dbFilled.clear(); this.lookupMessage = null;
    this.product = { ...this.product, itemCode: found.itemCode ?? '', itemName: found.itemName ?? '', itemNameAr: found.itemNameAr ?? null, unitPrice: found.unitPrice ?? 0, vatPercentage: found.vatPercentage ?? 0 };
    if (this.product.itemCode) this.dbFilled.add('itemCode');
    if (this.product.itemName) this.dbFilled.add('itemName');
    if (this.product.itemNameAr) this.dbFilled.add('itemNameAr');
    this.dbFilled.add('unitPrice'); this.dbFilled.add('vatPercentage');
  }

  private applyNotFound(code: string) {
    this.dbFilled.clear(); this.product = { ...blankProduct(), itemCode: code };
    this.lookupMessage = { kind: 'info', text: `Item ${code} was not found in the database. You can still enter it manually.` };
  }

  setText(field: 'itemCode' | 'itemName' | 'itemNameAr', value: unknown) {
    this.product = { ...this.product, [field]: String(value ?? '') || (field === 'itemNameAr' ? null : '') };
    this.onFieldEdit(field);
  }

  setNumber(field: 'quantity' | 'unitPrice' | 'vatPercentage' | 'discount', value: unknown) {
    this.product = { ...this.product, [field]: Number(value) || 0 };
    this.onFieldEdit(field);
  }

  onFieldEdit(field: string) { this.dbFilled.delete(field); }
  netUnitPrice(): number { return netUnitPriceOf(this.product.unitPrice, this.product.vatPercentage); }

  onItemLookup() {
    if (!this.branchCode.trim()) { this.lookupMessage = { kind: 'info', text: 'Select a branch in Order Header Information first - item pricing is branch-specific.' }; return; }
    if (!this.searchCode.trim()) { this.lookupMessage = { kind: 'info', text: 'Enter an item/material code first.' }; return; }
    this.clearStaleLookupValues(this.searchCode.trim()); this.lookupMessage = null;
    this.lookupItem.emit({ code: this.searchCode.trim(), branchCode: this.branchCode });
  }

  private clearStaleLookupValues(nextCode: string) {
    const nextProduct = { ...this.product };
    if (this.dbFilled.has('itemCode')) nextProduct.itemCode = nextCode;
    if (this.dbFilled.has('itemName')) nextProduct.itemName = '';
    if (this.dbFilled.has('itemNameAr')) nextProduct.itemNameAr = null;
    if (this.dbFilled.has('unitPrice')) nextProduct.unitPrice = 0;
    if (this.dbFilled.has('vatPercentage')) nextProduct.vatPercentage = 15;
    this.product = nextProduct; this.dbFilled.clear();
  }

  onAdd() {
    if (this.product.itemCode && this.product.itemName && this.product.quantity > 0) this.add.emit({ ...this.product });
  }
}
