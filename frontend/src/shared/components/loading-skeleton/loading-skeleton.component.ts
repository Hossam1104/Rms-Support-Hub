import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-loading-skeleton',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="skeleton-loader" [style.height]="height" [style.width]="width" [style.border-radius]="radius"></div>
  `,
  styles: [`
    .skeleton-loader {
      background: linear-gradient(90deg, var(--bg-secondary) 25%, var(--bg-tertiary) 50%, var(--bg-secondary) 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s infinite;
    }
  `]
})
export class LoadingSkeletonComponent {
  @Input() height: string = '20px';
  @Input() width: string = '100%';
  @Input() radius: string = 'var(--radius-sm)';
}
