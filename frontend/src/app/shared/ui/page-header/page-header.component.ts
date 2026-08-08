import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Gradient-mesh hero with a slow ambient drift (meshDrift, _gradients.css). */
@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="page-header" [class.page-header--compact]="compact">
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
      gap: var(--panel-gap);
      padding: var(--panel-padding) var(--panel-padding);
      border-radius: var(--radius-xl);
      overflow: hidden;
      background-image: var(--grad-mesh);
      background-size: 180% 180%;
      background-color: var(--surface-panel);
      animation: meshDrift 18s ease-in-out infinite;
      margin-bottom: var(--section-gap);
    }
    .header-content { flex: 1 1 auto; min-width: 0; }
    .header-content h1 {
      margin: 0 0 6px;
      font-size: var(--text-2xl);
      font-weight: 800;
      overflow-wrap: anywhere;
      background: var(--grad-brand);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .header-content p { margin: 0; color: var(--text-secondary); max-width: 560px; }
    .header-actions { display: flex; flex: 0 1 auto; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: var(--space-3); min-width: 0; }
    .page-header--compact { padding-block: var(--panel-padding-compact); border-radius: var(--radius-lg); margin-bottom: var(--panel-gap); }
    .page-header--compact .header-content h1 { margin-bottom: 3px; font-size: var(--text-xl); }
    @media (max-width: 900px) {
      .page-header { align-items: flex-start; flex-direction: column; }
      .header-actions { width: 100%; justify-content: flex-start; }
    }
    @media (max-width: 620px) {
      .page-header { gap: var(--panel-gap); padding: var(--panel-padding) var(--panel-padding); }
      .header-content { width: 100%; }
      .header-content h1 { font-size: var(--text-xl); }
      .header-content p { max-width: 100%; }
    }
  `]
})
export class PageHeaderComponent {
  @Input() title: string = '';
  @Input() subtitle?: string;
  @Input() compact = false;
}
