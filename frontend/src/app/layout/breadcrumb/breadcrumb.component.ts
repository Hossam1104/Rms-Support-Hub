import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <nav class="breadcrumb-nav">
      <a routerLink="/" class="crumb-item">Module Picker</a>
      <span class="crumb-separator">/</span>
      <span class="crumb-item active">{{ moduleLabel }}</span>
      <span class="crumb-separator" *ngIf="currentTab">/</span>
      <span class="crumb-item current" *ngIf="currentTab">{{ currentTab }}</span>
    </nav>
  `,
  styles: [`
    .breadcrumb-nav { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; color: var(--text-muted); margin-bottom: 20px; }
    .crumb-item { color: var(--text-secondary); text-decoration: none; }
    .crumb-item:hover { color: var(--primary); }
    .crumb-item.current { color: var(--text-primary); font-weight: 600; }
    .crumb-separator { color: var(--text-muted); }
  `]
})
export class BreadcrumbComponent {
  @Input() moduleLabel: string = '';
  @Input() currentTab?: string;
}
