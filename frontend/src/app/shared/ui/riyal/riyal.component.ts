import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Saudi Riyal currency glyph, recolored via a CSS mask so it always matches
 * surrounding text color -- preserves the legacy app's mask technique
 * (a single-color SVG with `fill="currentColor"`, painted via
 * `background-color` + `mask-image` instead of an <img>, so `currentColor`
 * actually resolves).
 */
@Component({
  selector: 'app-riyal',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="riyal-icon" [style.width.em]="size" [style.height.em]="size" aria-hidden="true"></span>`,
  styles: [`
    .riyal-icon {
      display: inline-block;
      vertical-align: -0.125em;
      background-color: currentColor;
      -webkit-mask: url('/assets/Saudi_Riyal.svg') no-repeat center / contain;
      mask: url('/assets/Saudi_Riyal.svg') no-repeat center / contain;
    }
  `]
})
export class RiyalComponent {
  @Input() size: number = 1;
}
