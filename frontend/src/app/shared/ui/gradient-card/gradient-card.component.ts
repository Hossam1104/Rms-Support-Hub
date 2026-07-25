import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type GradientCardVariant = 'brand' | 'success' | 'danger' | 'info' | 'muted';

/** Large rounded card with a gradient top accent and a lift-on-hover glow --
 * the successor to the .glass-card utility class (see _gradients.css) for
 * newly-built/restyled UI. */
@Component({
  selector: 'app-gradient-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="gradient-card" [class]="'variant-' + variant" [class.flat]="flat">
      <ng-content></ng-content>
    </div>
  `,
  styles: [`
    .gradient-card {
      position: relative;
      background: var(--glass-bg);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border: 1px solid var(--glass-border);
      border-radius: var(--radius-xl);
      box-shadow: var(--shadow-md);
      transition: transform var(--d) var(--ease-spring), box-shadow var(--transition-normal);
      overflow: hidden;
    }
    .gradient-card::before {
      content: '';
      position: absolute;
      top: 0; left: 0; right: 0;
      height: 4px;
      background: var(--accent-grad, var(--grad-brand));
    }
    .gradient-card.variant-success { --accent-grad: var(--grad-success); }
    .gradient-card.variant-danger { --accent-grad: var(--grad-danger); }
    .gradient-card.variant-info { --accent-grad: var(--grad-info); }
    .gradient-card.variant-muted { --accent-grad: var(--grad-muted); }
    .gradient-card.flat::before { display: none; }
    .gradient-card:not(.flat):hover {
      transform: translateY(-4px);
      box-shadow: var(--shadow-lg), var(--shadow-glow);
    }
  `]
})
export class GradientCardComponent {
  @Input() variant: GradientCardVariant = 'brand';
  /** Suppresses the top accent bar and hover lift -- for cards used purely
   * as a layout surface (e.g. inside a drawer). */
  @Input() flat: boolean = false;
}
