import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="status-badge" [class]="'badge-' + variant">
      <span class="badge-dot"></span>
      <span class="badge-label">{{ label }}</span>
    </span>
  `,
  styles: [`
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      border-radius: var(--radius-pill);
      font-size: 0.75rem;
      font-weight: 600;
    }
    .badge-dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
    .badge-success { background: var(--success-bg); color: var(--success); }
    .badge-danger { background: var(--danger-bg); color: var(--danger); }
    .badge-warning { background: var(--warning-bg); color: var(--warning); }
    .badge-info { background: rgba(99, 102, 241, 0.15); color: var(--primary); }
    .badge-secondary { background: var(--bg-tertiary); color: var(--text-muted); }
  `]
})
export class StatusBadgeComponent {
  @Input() label: string = '';
  @Input() variant: 'success' | 'danger' | 'warning' | 'info' | 'secondary' = 'info';
}
