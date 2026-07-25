import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="empty-state">
      <i class="bi" [class]="icon"></i>
      <h3>{{ title }}</h3>
      <p *ngIf="description">{{ description }}</p>
      <div class="empty-state-action"><ng-content></ng-content></div>
    </div>
  `,
  styles: [`
    .empty-state {
      text-align: center;
      padding: 64px 24px;
      color: var(--text-muted);
    }
    .empty-state i {
      font-size: 3rem;
      margin-bottom: 16px;
      display: block;
      background: var(--grad-muted);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
    .empty-state h3 { font-size: 1.15rem; color: var(--text-primary); margin: 0 0 6px; }
    .empty-state p { margin: 0; font-size: 0.9rem; }
    .empty-state-action { margin-top: 20px; }
    .empty-state-action:empty { display: none; }
  `]
})
export class EmptyStateComponent {
  @Input() icon: string = 'bi-inbox';
  @Input() title: string = 'Nothing here yet';
  @Input() description?: string;
}
