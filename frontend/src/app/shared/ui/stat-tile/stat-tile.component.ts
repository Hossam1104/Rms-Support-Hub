import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CountUpDirective } from '../../directives/count-up.directive';

export type StatTileVariant = 'brand' | 'success' | 'danger' | 'muted';

@Component({
  selector: 'app-stat-tile',
  standalone: true,
  imports: [CommonModule, CountUpDirective],
  template: `
    <button type="button" class="stat-tile" [class]="'variant-' + variant" [class.active]="active" (click)="clicked.emit()">
      <span class="stat-icon"><i class="bi" [class]="icon"></i></span>
      <span class="stat-meta">
        <span class="stat-label">{{ label }}</span>
        <span class="stat-value" [appCountUp]="value" [countUpDecimals]="decimals"></span>
      </span>
    </button>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .stat-tile {
      display: flex;
      align-items: center;
      gap: var(--panel-gap);
      padding: var(--panel-padding);
      width: 100%;
      min-width: 0;
      border: 1px solid var(--border-subtle);
      border-radius: var(--panel-radius);
      background: var(--surface-panel);
      cursor: pointer;
      text-align: left;
      transition: transform var(--d) var(--ease-spring), box-shadow var(--transition-normal), border-color var(--transition-fast);
    }
    .stat-tile:hover { transform: translateY(-3px); box-shadow: var(--shadow-lg); }
    .stat-tile.active { border-color: transparent; box-shadow: var(--shadow-glow); }
    .stat-icon {
      width: 48px; height: 48px;
      border-radius: var(--radius-md);
      display: flex; align-items: center; justify-content: center;
      font-size: 1.4rem;
      color: var(--on-gradient);
      background: var(--grad-brand);
      flex-shrink: 0;
    }
    .variant-success .stat-icon { background: var(--grad-success); }
    .variant-danger .stat-icon { background: var(--grad-danger); }
    .variant-muted .stat-icon { background: var(--grad-muted); }
    .stat-meta { display: flex; flex-direction: column; gap: 2px; }
    .stat-label { font-size: 0.8rem; color: var(--text-muted); font-weight: 500; }
    .stat-value { font-size: 1.6rem; font-weight: 800; color: var(--text-primary); }
  `]
})
export class StatTileComponent {
  @Input() label: string = '';
  @Input() value: number = 0;
  @Input() decimals: number = 0;
  @Input() icon: string = 'bi-graph-up';
  @Input() variant: StatTileVariant = 'brand';
  @Input() active: boolean = false;

  @Output() clicked = new EventEmitter<void>();
}
