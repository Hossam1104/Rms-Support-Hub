import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RowItem } from '../../../core/models';
import { RiyalComponent, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent } from '../../../shared/ui';

@Component({
  selector: 'app-add-row-item-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, RiyalComponent, UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent],
  template: `
    <div class="modal-backdrop" (click)="close.emit()">
      <ui-card variant="raised" class="modal-dialog" (click)="$event.stopPropagation()">
        <div uiCardHeader class="modal-header">
          <h3><i class="bi bi-list-stars" aria-hidden="true"></i> Add Row Item</h3>
          <ui-button variant="ghost" size="sm" icon="bi-x-lg" ariaLabel="Close" (pressed)="close.emit()"></ui-button>
        </div>

        <div class="form-grid">
          <ui-field label="Material Number" forId="row-material-number" [required]="true">
            <ui-input inputId="row-material-number" placeholder="6-digit material #" [(ngModel)]="item.materialNumber"></ui-input>
          </ui-field>
          <ui-field label="Barcode" forId="row-barcode" [required]="true">
            <ui-input inputId="row-barcode" [(ngModel)]="item.barcode"></ui-input>
          </ui-field>
          <ui-field label="Quantity" forId="row-quantity" [required]="true">
            <ui-input inputId="row-quantity" type="number" step="0.01" [(ngModel)]="item.quantity"></ui-input>
          </ui-field>
          <ui-field label="Item Price" forId="row-price" [required]="true">
            <ui-input inputId="row-price" type="number" step="0.01" [(ngModel)]="item.itemPrice">
              <app-riyal uiInputSuffix [size]=".9"></app-riyal>
            </ui-input>
          </ui-field>
          <ui-field label="Item Discount" forId="row-discount">
            <ui-input inputId="row-discount" type="number" step="0.01" [(ngModel)]="item.itemDiscount">
              <app-riyal uiInputSuffix [size]=".9"></app-riyal>
            </ui-input>
          </ui-field>
          <ui-field label="VAT %" forId="row-vat">
            <ui-input inputId="row-vat" type="number" step="0.01" [(ngModel)]="item.vatPercentage"></ui-input>
          </ui-field>
        </div>

        <div uiCardFooter class="modal-footer">
          <ui-button variant="secondary" (pressed)="close.emit()">Cancel</ui-button>
          <ui-button icon="bi-plus-circle" (pressed)="onAdd()">Add Row Item</ui-button>
        </div>
      </ui-card>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; inset: 0; z-index: 2000; display: grid; place-items: center; padding: 16px; background: var(--backdrop); backdrop-filter: blur(3px); }
    .modal-dialog { width: min(600px, 100%); max-height: min(760px, calc(100vh - 32px)); overflow: auto; }
    .modal-header { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
    .modal-header h3 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 1.1rem; }
    .modal-header h3 i { color: var(--accent); }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; }
    @media (max-width: 560px) { .form-grid { grid-template-columns: 1fr; } }
  `]
})
export class AddRowItemDialogComponent {
  @Output() close = new EventEmitter<void>();
  @Output() add = new EventEmitter<RowItem>();

  item: RowItem = { materialNumber: '', barcode: '', quantity: 1, itemPrice: 0, itemDiscount: 0, vatPercentage: 15 };

  onAdd() {
    if (this.item.materialNumber && this.item.quantity > 0) {
      if (!this.item.barcode) this.item.barcode = `BC${this.item.materialNumber}`;
      this.add.emit(this.item);
    }
  }
}
