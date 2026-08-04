import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * The single Saudi Riyal currency glyph for the whole app. Every human-visible
 * Riyal amount renders through this component instead of a textual currency
 * "ر.س" text, so the symbol, its color response and its accessible name stay
 * consistent across the summary rail, dense tables, dialogs and totals.
 *
 * The glyph is `public/assets/Saudi_Riyal.svg` painted via a CSS mask rather
 * than an `<img>`: the asset is single-color, and masking is what lets
 * `background-color: currentColor` actually resolve, so the symbol inherits
 * the surrounding text color and works unchanged in both themes and in print.
 *
 * Accessibility: the mask carries no text, so a visually-hidden "Saudi Riyal"
 * label is emitted alongside it -- a screen reader announces "Saudi Riyal
 * 402.50" rather than a bare number or an asset file name. Pass
 * `[decorative]="true"` where the currency is already stated by a nearby
 * label and repeating it would only add noise.
 */
@Component({
  selector: 'app-riyal',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="riyal" [class.is-decorative]="decorative"><span class="riyal-icon" data-asset-path="/assets/Saudi_Riyal.svg" [style.width.em]="size" [style.height.em]="size" aria-hidden="true"></span><span class="sr-only" *ngIf="!decorative">Saudi Riyal</span></span>`,
  styles: [`
    .riyal { display: inline-flex; align-items: center; }
    .riyal-icon {
      display: inline-block;
      vertical-align: -0.125em;
      background-color: currentColor;
      -webkit-mask: url('/assets/Saudi_Riyal.svg') no-repeat center / contain;
      mask: url('/assets/Saudi_Riyal.svg') no-repeat center / contain;
    }
    .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
  `]
})
export class RiyalComponent {
  @Input() size: number = 1;
  /** Suppresses the visually-hidden "Saudi Riyal" label when an adjacent
   * label already names the currency. */
  @Input() decorative = false;
}
