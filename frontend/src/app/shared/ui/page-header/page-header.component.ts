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
    :host { display: block; min-width: 0; }
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
      background-color: var(--surface-panel);
      animation: meshDrift 18s ease-in-out infinite;
      margin-bottom: 28px;
    }
    .header-content { min-width: 0; }
    .header-content h1 {
      margin: 0 0 6px;
      font-size: 2rem;
      font-weight: 800;
      background: var(--grad-brand);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .header-content p { margin: 0; color: var(--text-secondary); max-width: 560px; }
    .header-actions { display: flex; gap: 12px; min-width: 0; flex-shrink: 0; }
    @media (max-width: 620px) {
      .page-header { align-items: flex-start; flex-direction: column; gap: 16px; padding: 28px 20px; }
      .header-content { width: 100%; }
      .header-content h1 { font-size: clamp(1.45rem, 8vw, 2rem); overflow-wrap: anywhere; }
      .header-content p { max-width: 100%; }
      .header-actions { width: 100%; flex-wrap: wrap; }
    }
  `]
})
export class PageHeaderComponent {
  @Input() title: string = '';
  @Input() subtitle?: string;
}
