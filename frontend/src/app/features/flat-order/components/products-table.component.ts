import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Product } from '../../../core/models';
import { APP_ASSETS } from '../../../core/config/app-assets';
import { EmptyStateComponent, RiyalComponent, UiButtonComponent, UiIconButtonComponent } from '../../../shared/ui';

export interface ProductUpdate {
  index: number;
  patch: Partial<Product>;
}

/**
 * Product rows are a hybrid grid rather than a table: one product is one row
 * entity whose identity (name / Arabic name / code) owns the flexible column
 * while the editable financial cells stay fixed-width and compact. A column
 * header strip supplies the visual labels at wide container widths; each cell
 * still carries its own label, visually hidden there, so the row reflows into
 * a labelled stack when the container narrows without losing accessible names.
 */
@Component({
  selector: 'app-products-table',
  standalone: true,
  imports: [CommonModule, EmptyStateComponent, RiyalComponent, UiButtonComponent, UiIconButtonComponent],
  template: `
    <div id="products-card" class="pt">
      <div class="pt__errors" role="alert" *ngIf="errors.length > 0">
        <p *ngFor="let message of errors"><i class="bi bi-exclamation-circle" aria-hidden="true"></i>{{ message }}</p>
      </div>

      <ng-container *ngIf="products.length > 0; else productsEmpty">
        <div class="pt__head" aria-hidden="true">
          <span>Item</span>
          <span>Unit price</span>
          <span>Qty</span>
          <span>Discount</span>
          <span>VAT</span>
          <span>Row total</span>
          <span></span>
        </div>

        <ul class="pt__list">
          <li class="prow" *ngFor="let product of products; let index = index; trackBy: trackByIndex">
            <div class="prow__id">
              <span class="prow__seq" aria-hidden="true">{{ index + 1 }}</span>
              <span class="prow__names">
                <strong class="prow__name">{{ product.itemName }}</strong>
                <span class="prow__ar" *ngIf="product.itemNameAr">{{ product.itemNameAr }}</span>
                <span class="prow__marks" *ngIf="product.itemCode || product.offerCode || product.offerMessage">
                  <span class="prow__code" *ngIf="product.itemCode">{{ product.itemCode }}</span>
                  <span class="prow__offer" *ngIf="product.offerCode || product.offerMessage">
                    <img [src]="assets.commerce.offer" alt="" aria-hidden="true">
                    <span>{{ product.offerCode || 'Offer' }}</span>
                  </span>
                </span>
              </span>
            </div>

            <div class="cell cell--unit">
              <span class="cell__label">Unit price</span>
              <span class="cell__value"><app-riyal [size]="0.78"></app-riyal>{{ product.unitPrice | number:'1.2-2' }}</span>
            </div>

            <div class="cell cell--qty">
              <label class="cell__label" [for]="'product-quantity-' + index">Qty</label>
              <input
                [id]="'product-quantity-' + index"
                class="editor editor--qty"
                type="number" min="0" step="1"
                [value]="product.quantity"
                [attr.aria-label]="'Quantity for ' + product.itemName"
                (change)="onEdit(index, 'quantity', $event)">
            </div>

            <div class="cell cell--disc">
              <label class="cell__label" [for]="'product-discount-' + index">Discount</label>
              <span class="editor-wrap">
                <app-riyal [decorative]="true" [size]="0.72"></app-riyal>
                <input
                  [id]="'product-discount-' + index"
                  class="editor"
                  type="number" min="0" step="0.01"
                  [value]="product.discount"
                  [attr.aria-label]="'Discount for ' + product.itemName"
                  (change)="onEdit(index, 'discount', $event)">
              </span>
            </div>

            <div class="cell cell--vat">
              <span class="cell__label">VAT</span>
              <span class="cell__value cell__value--quiet">{{ product.vatPercentage | number:'1.0-2' }}%</span>
            </div>

            <div class="cell cell--total">
              <span class="cell__label">Row total</span>
              <span class="cell__value cell__value--total"><app-riyal [size]="0.82"></app-riyal>{{ getProductTotal(product) | number:'1.2-2' }}</span>
            </div>

            <div class="cell cell--act">
              <ui-icon-button icon="bi-trash3" size="sm" variant="danger" ariaLabel="Remove product" (pressed)="deleteProduct.emit(index)"></ui-icon-button>
            </div>
          </li>
        </ul>
      </ng-container>

      <ng-template #productsEmpty>
        <app-empty-state icon="bi-box-seam" title="No products yet" description="Add an item to start building the order.">
          <ui-button variant="secondary" size="sm" icon="bi bi-plus-lg" (pressed)="openAddDialog.emit()">Add product</ui-button>
        </app-empty-state>
      </ng-template>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; container-type: inline-size; container-name: products; }
    .pt { min-width: 0; }
    .pt__errors { display: flex; flex-direction: column; gap: 5px; margin-bottom: var(--space-3); padding: 10px 12px; border: 1px solid var(--state-danger-border); border-radius: var(--radius-md); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .pt__errors p { display: flex; align-items: flex-start; gap: 7px; margin: 0; font-size: .78rem; line-height: 1.35; }

    /* Rows bleed to the hosting panel's edges so a hovered row and its divider
     * read as one continuous surface instead of a floating inner table. */
    .pt__head, .prow { display: grid; grid-template-columns: minmax(0, 1fr) 104px 66px 108px 52px 118px 34px; column-gap: var(--space-3); align-items: center; margin-inline: calc(var(--panel-padding) * -1); padding-inline: var(--panel-padding); }
    .pt__head { padding-bottom: 7px; border-bottom: 1px solid var(--border-strong); color: var(--text-muted); font-size: .67rem; font-weight: 800; letter-spacing: .05em; text-transform: uppercase; }
    .pt__head > * { text-align: right; }
    .pt__head > :first-child { text-align: left; }

    .pt__list { margin: 0; padding: 0; list-style: none; }
    .prow { padding-block: 9px; border-bottom: 1px solid var(--divider); transition: background var(--transition-fast); }
    .prow:last-child { border-bottom: 0; }
    .prow:hover { background: var(--table-row-hover); }

    .prow__id { display: flex; align-items: baseline; gap: var(--space-2); min-width: 0; }
    .prow__seq { flex: 0 0 auto; min-width: 13px; color: var(--text-muted); font-size: .72rem; font-variant-numeric: tabular-nums; }
    .prow__names { display: flex; flex-direction: column; min-width: 0; }
    .prow__name { color: var(--text-primary); font-size: .92rem; font-weight: 750; line-height: 1.3; overflow-wrap: anywhere; }
    .prow__ar { margin-top: 1px; color: var(--text-secondary); font-size: .78rem; line-height: 1.35; }
    .prow__marks { display: flex; align-items: center; gap: var(--space-3); flex-wrap: wrap; margin-top: 3px; }
    .prow__code { color: var(--text-muted); font-family: var(--font-mono); font-size: .7rem; }
    .prow__offer { display: inline-flex; align-items: center; gap: 4px; color: var(--state-warning-fg); font-size: .7rem; font-weight: var(--weight-bold); }
    .prow__offer img { width: 13px; height: 13px; object-fit: contain; }

    .cell { display: flex; flex-direction: column; align-items: flex-end; gap: 3px; min-width: 0; }
    .cell > .editor, .cell > .editor-wrap { align-self: stretch; }
    .cell__label { position: absolute; width: 1px; height: 1px; margin: -1px; padding: 0; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
    .cell__value { display: inline-flex; align-items: center; gap: 3px; color: var(--text-secondary); font-size: .86rem; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .cell__value--quiet { color: var(--text-muted); font-size: .8rem; }
    .cell__value--total { color: var(--text-primary); font-size: 1rem; font-weight: 800; }

    .editor { width: 100%; min-height: 32px; box-sizing: border-box; padding: 0 8px; border: 1px solid var(--input-border); border-radius: var(--radius-sm); background: var(--input-bg); color: var(--text-primary); font: inherit; font-size: .85rem; text-align: right; font-variant-numeric: tabular-nums; }
    .editor:focus-visible { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .editor--qty { padding-inline: 4px; text-align: center; }
    .editor-wrap { display: flex; align-items: center; gap: 4px; }
    .editor-wrap app-riyal { flex: 0 0 auto; color: var(--text-muted); }

    @container products (max-width: 780px) {
      .pt__head { display: none; }
      .prow { grid-template-columns: repeat(4, minmax(0, 1fr)); grid-template-areas: "id id id id" "unit qty disc vat" "total total total act"; row-gap: 10px; padding-block: 12px; align-items: end; }
      .prow__id { grid-area: id; }
      .cell--unit { grid-area: unit; } .cell--qty { grid-area: qty; } .cell--disc { grid-area: disc; }
      .cell--vat { grid-area: vat; } .cell--total { grid-area: total; } .cell--act { grid-area: act; }
      .cell { align-items: stretch; }
      .cell--act { align-items: flex-end; }
      .cell__label { position: static; width: auto; height: auto; margin: 0; overflow: visible; clip-path: none; color: var(--text-muted); font-size: .65rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    }
    @container products (max-width: 470px) {
      .prow { grid-template-columns: repeat(2, minmax(0, 1fr)); grid-template-areas: "id id" "unit qty" "disc vat" "total act"; }
    }
  `]
})
export class ProductsTableComponent {
  readonly assets = APP_ASSETS;
  @Input() products: Product[] = [];
  /** U4: server send-validation errors routed to the products section. */
  @Input() errors: string[] = [];
  @Output() openAddDialog = new EventEmitter<void>();
  @Output() deleteProduct = new EventEmitter<number>();
  @Output() updateProduct = new EventEmitter<ProductUpdate>();

  trackByIndex(index: number): number { return index; }

  onEdit(index: number, field: 'quantity' | 'discount', event: Event) {
    const raw = (event.target as HTMLInputElement).value;
    const value = Number(raw);
    this.updateProduct.emit({ index, patch: { [field]: Number.isFinite(value) ? value : 0 } });
  }

  /** Existing display convention retained: flat row discount is applied once,
   * then VAT is calculated on the discounted row subtotal. Server totals stay
   * authoritative for the summary rail. */
  getProductTotal(prod: Product): number {
    const qty = prod.quantity || 0;
    const price = prod.unitPrice || 0;
    const discount = prod.discount || 0;
    const vatPct = prod.vatPercentage || 0;
    const rowSubtotal = qty * price;
    const totalVat = Math.round((rowSubtotal - discount) * (vatPct / 100) * 100) / 100;
    return Math.round((rowSubtotal - discount + totalVat) * 100) / 100;
  }
}
