import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'ui-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="ui-table-shell" [class.ui-table--dense]="dense()" [class.ui-table--sticky]="stickyHeader()" [class.ui-table--zebra]="zebra()">
      <table>
        <caption *ngIf="caption()">{{ caption() }}</caption>
        <ng-content select="thead"></ng-content>
        <ng-content select="tbody"></ng-content>
        <ng-content select="tfoot"></ng-content>
      </table>
      <div class="ui-table__empty" *ngIf="empty()"><ng-content select="[uiTableEmpty]"></ng-content></div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-table-shell { overflow: auto; max-width: 100%; border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); }
    table { width: 100%; min-width: 520px; border-collapse: separate; border-spacing: 0; color: var(--text-primary); font-size: .86rem; }
    caption { padding: 12px 16px; color: var(--text-secondary); text-align: left; }
    th, td { padding: 13px 16px; border-bottom: 1px solid var(--divider); text-align: left; vertical-align: middle; }
    th { color: var(--text-secondary); background: var(--surface-raised); font-size: .74rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    tbody tr { transition: background var(--transition-fast); }
    tbody tr:hover { background: var(--table-row-hover); }
    tbody tr:last-child td, tfoot tr:last-child td { border-bottom: 0; }
    .ui-table--zebra tbody tr:nth-child(even) { background: var(--table-row-zebra); }
    .ui-table--zebra tbody tr:nth-child(even):hover { background: var(--table-row-hover); }
    .ui-table--dense th, .ui-table--dense td { padding: 8px 12px; }
    .ui-table--sticky thead th { position: sticky; top: 0; z-index: 1; box-shadow: 0 1px 0 var(--border-subtle); }
    .ui-table__empty { display: grid; min-height: 100px; place-items: center; padding: 20px; color: var(--text-muted); }
  `]
})
export class UiTableComponent {
  readonly dense = input(false);
  readonly stickyHeader = input(false);
  readonly zebra = input(false);
  readonly empty = input(false);
  readonly caption = input('');
}
