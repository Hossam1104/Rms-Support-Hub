import { Component, Input } from '@angular/core';

/**
 * Small, presentation-only image primitive for product, company, and module
 * marks. The fixed box and contain fit keep assets with different source
 * geometry from changing layout or being distorted.
 *
 * `size` is the box height and `width` is optional: square module marks omit
 * it, while wide wordmarks (RMS+, DBS) pass their own width so the artwork
 * fills the box instead of being letterboxed inside a square.
 */
@Component({
  selector: 'app-brand-mark',
  standalone: true,
  template: `
    <span
      class="brand-mark"
      [class.brand-mark--framed]="framed"
      [style.--brand-mark-size]="size"
      [style.--brand-mark-width]="width || size">
      <img
        [src]="src"
        [alt]="decorative ? '' : alt"
        [attr.aria-hidden]="decorative ? 'true' : null" />
    </span>
  `,
  styles: [`
    :host { display: inline-flex; vertical-align: middle; }
    .brand-mark {
      display: inline-flex;
      width: var(--brand-mark-width, var(--brand-mark-size));
      height: var(--brand-mark-size);
      align-items: center;
      justify-content: center;
      flex: 0 0 var(--brand-mark-width, var(--brand-mark-size));
      overflow: hidden;
      box-sizing: border-box;
    }
    .brand-mark--framed {
      padding: 4px;
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-md);
      /* The supplied GHC/Whites mark is black; keep its plate readable in dark mode. */
      background: var(--on-gradient);
    }
    img {
      display: block;
      width: 100%;
      height: 100%;
      object-fit: contain;
    }
  `]
})
export class BrandMarkComponent {
  @Input() src = '';
  @Input() alt = '';
  @Input() decorative = false;
  @Input() size = '2.5rem';
  /** Optional box width; defaults to `size`, keeping module marks square. */
  @Input() width = '';
  @Input() framed = false;
}
