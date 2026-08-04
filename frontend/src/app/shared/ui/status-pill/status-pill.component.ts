import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ORDER_REQUEST_STATUS_LABELS } from '../../../core/models';

/** RequestOrderHeaders.OrderStatus (1..9) -- see
 * backend/src/OnlineOrderTool.Core/OrderRequestStatus.cs. */
/** Renders one of the nine order-status gradient pills defined in
 * _gradients.css. Pops on status change (spring scale via a CSS class
 * toggled for one animation cycle -- no @angular/animations dependency
 * needed for a single one-shot keyframe). */
@Component({
  selector: 'app-status-pill',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="status-pill" [class]="'status-pill--' + status" [class.pop]="popping">
      {{ label ?? fallbackLabel() }}
    </span>
  `,
  styles: [`
    :host { display: inline-flex; }
    .pop { animation: pillPop var(--d) var(--ease-spring); }
    @keyframes pillPop {
      0% { transform: scale(1); }
      50% { transform: scale(1.18); }
      100% { transform: scale(1); }
    }
  `]
})
export class StatusPillComponent implements OnChanges {
  @Input({ required: true }) status!: number;
  @Input() label?: string;

  popping = false;

  ngOnChanges(changes: SimpleChanges) {
    if (changes['status'] && !changes['status'].firstChange) {
      this.popping = false;
      // Re-trigger the animation on the next tick even if Angular hasn't
      // re-rendered the class list in between.
      queueMicrotask(() => { this.popping = true; });
      setTimeout(() => { this.popping = false; }, 260);
    }
  }

  fallbackLabel(): string {
    return ORDER_REQUEST_STATUS_LABELS[this.status] ?? `Status ${this.status}`;
  }
}
