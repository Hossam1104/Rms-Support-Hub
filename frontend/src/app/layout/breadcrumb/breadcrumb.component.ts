import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <nav class="breadcrumb-nav" aria-label="Breadcrumb">
      <ol class="breadcrumb-list">
        <li class="breadcrumb-segment">
          <a routerLink="/" class="crumb-item">Dashboard</a>
        </li>
        <li class="crumb-separator" aria-hidden="true">/</li>
        @if (moduleLabel) {
          <li class="breadcrumb-segment">
            <a routerLink="/tools/online-orders" class="crumb-item">Online Orders</a>
          </li>
          <li class="crumb-separator" aria-hidden="true">/</li>
          <li class="breadcrumb-segment">
            @if (currentTab && moduleRoute.length > 0) {
              <a [routerLink]="moduleRoute" class="crumb-item">{{ moduleLabel }}</a>
            } @else {
              <span class="crumb-item current" aria-current="page">{{ moduleLabel }}</span>
            }
          </li>
          @if (currentTab) {
            <li class="crumb-separator" aria-hidden="true">/</li>
            <li class="breadcrumb-segment">
              <span class="crumb-item current" aria-current="page">{{ currentTab }}</span>
            </li>
          }
        } @else {
          <li class="breadcrumb-segment">
            <span class="crumb-item current" aria-current="page">Online Orders</span>
          </li>
        }
      </ol>
    </nav>
  `,
  styles: [`
    .breadcrumb-nav { margin-bottom: var(--space-5); color: var(--text-muted); font-size: var(--text-sm); }
    .breadcrumb-list { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-2); margin: 0; padding: 0; list-style: none; }
    .breadcrumb-segment { display: inline-flex; min-width: 0; }
    .crumb-item { color: var(--text-secondary); text-decoration: none; }
    .crumb-item:hover { color: var(--text-accent); }
    .crumb-item.current { color: var(--text-primary); font-weight: 600; }
    .crumb-separator { color: var(--text-muted); }
    .crumb-item:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }
  `]
})
export class BreadcrumbComponent {
  @Input() moduleLabel: string = '';
  @Input() moduleRoute: string[] = [];
  @Input() currentTab?: string;
}
