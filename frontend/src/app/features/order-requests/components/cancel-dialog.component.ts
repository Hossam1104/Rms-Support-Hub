import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-cancel-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-backdrop glass-panel" (click)="close.emit()">
      <div class="modal-dialog glass-card fade-in-up" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <h3><i class="bi bi-x-circle text-danger"></i> Cancel Order Request</h3>
          <button type="button" class="btn-close" (click)="close.emit()">&times;</button>
        </div>

        <div class="modal-body">
          <p class="modal-desc">Are you sure you want to send a cancellation request for order <strong>{{ orderCode }}</strong>?</p>

          <div class="form-group">
            <label class="form-label">Cancellation Reason *</label>
            <textarea class="glass-input" rows="3" [(ngModel)]="cancelReason" placeholder="Specify reason for cancelling this order..." required></textarea>
          </div>
        </div>

        <div class="modal-footer">
          <button type="button" class="btn-secondary glass-input" (click)="close.emit()">Close</button>
          <button type="button" class="glass-button btn-danger" [disabled]="!cancelReason.trim()" (click)="onConfirm()">
            Confirm Cancellation
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .modal-backdrop { position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 2000; display: flex; align-items: center; justify-content: center; }
    .modal-dialog { width: 100%; max-width: 500px; padding: 24px; border-radius: var(--radius-lg); }
    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .modal-header h3 { margin: 0; font-size: 1.15rem; display: flex; align-items: center; gap: 8px; }
    .modal-desc { color: var(--text-secondary); margin-bottom: 16px; font-size: 0.9rem; }
    .btn-close { background: none; border: none; font-size: 1.5rem; color: var(--text-muted); cursor: pointer; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 12px; margin-top: 20px; }
    .btn-danger { background: var(--danger); }
  `]
})
export class CancelDialogComponent {
  @Input() orderCode: string = '';
  @Output() close = new EventEmitter<void>();
  @Output() confirmCancel = new EventEmitter<string>();

  cancelReason: string = 'User requested cancellation from Order Requests tab';

  onConfirm() {
    if (this.cancelReason.trim()) {
      this.confirmCancel.emit(this.cancelReason.trim());
    }
  }
}
