import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-filter-chip',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="filter-chip">
      <span class="chip-label">{{ label }}</span>
      <button type="button" class="chip-remove" (click)="remove.emit()" aria-label="Remove filter">
        <i class="bi bi-x"></i>
      </button>
    </span>
  `,
  styles: [`
    .filter-chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 6px 4px 12px;
      border-radius: var(--radius-pill);
      background: var(--grad-brand);
      color: var(--on-gradient);
      font-size: 0.8rem;
      font-weight: 600;
      animation: chipSpringIn var(--d-slow) var(--ease-spring);
    }
    .chip-remove {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 18px;
      height: 18px;
      border-radius: 50%;
      background: rgba(255, 255, 255, .25);
      border: none;
      color: inherit;
      cursor: pointer;
      transition: background var(--transition-fast);
    }
    .chip-remove:hover { background: rgba(255, 255, 255, .4); }
    @keyframes chipSpringIn {
      from { opacity: 0; transform: scale(.8) translateY(4px); }
      to { opacity: 1; transform: scale(1) translateY(0); }
    }
  `]
})
export class FilterChipComponent {
  @Input({ required: true }) label!: string;
  @Output() remove = new EventEmitter<void>();
}
