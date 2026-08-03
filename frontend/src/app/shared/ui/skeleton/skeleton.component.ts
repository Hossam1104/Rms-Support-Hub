import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  imports: [CommonModule],
  template: `<span class="skeleton" [style.width]="width" [style.height]="height" [style.borderRadius]="radius"></span>`,
  styles: [`
    .skeleton {
      display: block;
      background: linear-gradient(90deg,
        var(--surface-raised) 25%,
        var(--surface-hover) 37%,
        var(--surface-raised) 63%);
      background-size: 400% 100%;
      animation: shimmerSweep 1.4s linear infinite;
    }
  `]
})
export class SkeletonComponent {
  @Input() width: string = '100%';
  @Input() height: string = '16px';
  @Input() radius: string = 'var(--radius-sm)';
}
