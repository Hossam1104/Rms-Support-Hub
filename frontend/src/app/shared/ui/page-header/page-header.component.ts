import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Gradient-mesh hero with a slow ambient drift (meshDrift, _gradients.css). */
@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="page-header">
      <div class="header-content">
        <h1>{{ title }}</h1>
        <p *ngIf="subtitle">{{ subtitle }}</p>
      </div>
      <div class="header-actions"><ng-content></ng-content></div>
    </header>
  `,
  styles: [`
    .page-header {
      position: relative;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 24px;
      padding: 40px 32px;
      border-radius: var(--radius-xl);
      overflow: hidden;
      background-image: var(--grad-mesh);
      background-size: 180% 180%;
      background-color: var(--bg-secondary);
      animation: meshDrift 18s ease-in-out infinite;
      margin-bottom: 28px;
    }
    .header-content h1 {
      margin: 0 0 6px;
      font-size: 2rem;
      font-weight: 800;
      background: var(--grad-brand);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .header-content p { margin: 0; color: var(--text-secondary); max-width: 560px; }
    .header-actions { display: flex; gap: 12px; flex-shrink: 0; }
  `]
})
export class PageHeaderComponent {
  @Input() title: string = '';
  @Input() subtitle?: string;
}
