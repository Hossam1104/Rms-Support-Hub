import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'ui-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="ui-table-shell" [class.ui-table--dense]="dense()" [class.ui-table--sticky]="stickyHeader()" [class.ui-table--zebra]="zebra()" [class.ui-table--wide]="wide()">
      <table>
        <caption *ngIf="caption()" [class.sr-only]="captionHidden()">{{ caption() }}</caption>
        <ng-content select="thead"></ng-content>
        <ng-content select="tbody"></ng-content>
        <ng-content select="tfoot"></ng-content>
      </table>
      <div class="ui-table__empty" *ngIf="empty()"><ng-content select="[uiTableEmpty]"></ng-content></div>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-table-shell { overflow-x: auto; max-width: 100%; margin: var(--table-inset-block) var(--table-inset-inline) 0; border: 1px solid var(--table-border); border-radius: var(--radius-md); background: var(--surface-panel); scrollbar-color: var(--border-strong) transparent; }
    table { width: 100%; min-width: var(--table-min-width); border-collapse: separate; border-spacing: 0; color: var(--text-primary); font-size: var(--text-sm); }
    .ui-table--wide table { min-width: var(--table-min-width-wide); }
    caption { padding: var(--panel-padding-compact) var(--table-cell-padding-x); color: var(--text-secondary); text-align: left; }
    th, td { padding: var(--table-cell-padding-y) var(--table-cell-padding-x); border-bottom: 1px solid var(--table-row-border); text-align: left; vertical-align: middle; line-height: 1.3; }
    th { height: var(--table-header-height); color: var(--text-secondary); background: var(--table-header-surface); border-bottom-color: var(--table-border); font-size: var(--text-xs); font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    th.numeric-cell, td.numeric-cell,
    th.align-right, td.align-right,
    th[data-align="numeric"], td[data-align="numeric"] { text-align: right; font-variant-numeric: tabular-nums; }
    tfoot { background: var(--table-footer-surface); }
    tfoot th, tfoot td { border-top: 2px solid var(--table-border); color: var(--text-primary); font-weight: 750; }
    tbody tr { transition: background var(--transition-fast); }
    tbody tr:hover { background: var(--table-row-hover); }
    tbody tr:last-child td, tfoot tr:last-child td { border-bottom: 0; }
    .ui-table--zebra tbody tr:nth-child(even) { background: var(--table-row-zebra); }
    .ui-table--zebra tbody tr:nth-child(even):hover { background: var(--table-row-hover); }
    .ui-table--dense th, .ui-table--dense td { padding: var(--table-cell-padding-y-compact) var(--table-cell-padding-x-compact); }
    .ui-table--sticky thead th { position: sticky; top: 0; z-index: 1; background: var(--table-header-surface); box-shadow: 0 1px 0 var(--table-border); }
    .ui-table__empty { display: grid; min-height: 100px; place-items: center; padding: var(--panel-padding); color: var(--text-muted); }
  `]
})
export class UiTableComponent {
  readonly dense = input(false);
  readonly stickyHeader = input(false);
  readonly zebra = input(false);
  readonly wide = input(false);
  readonly empty = input(false);
  readonly caption = input('');
  readonly captionHidden = input(false);
}
