import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface ToastMessage {
  id: string;
  message: string;
  type: ToastType;
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  toasts = signal<ToastMessage[]>([]);

  show(message: string, type: ToastType = 'info', duration: number = 4000) {
    const id = Math.random().toString(36).substring(2, 9);
    const toast: ToastMessage = { id, message, type, duration };

    this.toasts.update(current => [...current, toast]);

    if (duration > 0) {
      setTimeout(() => this.remove(id), duration);
    }
  }

  showSuccess(message: string) { this.show(message, 'success'); }
  showError(message: string) { this.show(message, 'error'); }
  showWarning(message: string) { this.show(message, 'warning'); }
  showInfo(message: string) { this.show(message, 'info'); }

  remove(id: string) {
    this.toasts.update(current => current.filter(t => t.id !== id));
  }
}
