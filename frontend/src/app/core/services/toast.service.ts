import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export interface ToastMessage {
  id: string;
  message: string;
  type: ToastType;
  duration: number;
  count: number;
}

interface ToastTimer {
  handle: ReturnType<typeof setTimeout> | null;
  dueAt: number;
  remaining: number;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly maxVisible = 3;
  readonly toasts = signal<ToastMessage[]>([]);
  readonly queued = signal<ToastMessage[]>([]);

  private nextId = 0;
  private readonly timers = new Map<string, ToastTimer>();

  show(message: string, type: ToastType = 'info', duration = 4000): string {
    const cleanMessage = message.trim() || 'Notification';
    const visible = this.toasts();
    const queued = this.queued();
    const last = queued.length ? queued[queued.length - 1] : visible[visible.length - 1];

    if (last && last.message === cleanMessage && last.type === type) {
      const updated = { ...last, count: last.count + 1 };
      if (queued.length) this.queued.update(items => items.map(item => item.id === last.id ? updated : item));
      else {
        this.toasts.update(items => items.map(item => item.id === last.id ? updated : item));
        this.restartTimer(last.id);
      }
      return last.id;
    }

    const toast: ToastMessage = {
      id: `toast-${++this.nextId}`,
      message: cleanMessage,
      type,
      duration: Math.max(0, duration),
      count: 1
    };

    if (visible.length < this.maxVisible) {
      this.toasts.update(items => [...items, toast]);
      this.startTimer(toast.id, toast.duration);
    } else {
      this.queued.update(items => [...items, toast]);
    }
    return toast.id;
  }

  showSuccess(message: string, duration = 4000) { return this.show(message, 'success', duration); }
  showError(message: string, duration = 4000) { return this.show(message, 'error', duration); }
  showWarning(message: string, duration = 4000) { return this.show(message, 'warning', duration); }
  showInfo(message: string, duration = 4000) { return this.show(message, 'info', duration); }

  remove(id: string) {
    this.clearTimer(id);
    if (this.queued().some(toast => toast.id === id)) {
      this.queued.update(items => items.filter(toast => toast.id !== id));
      return;
    }
    if (!this.toasts().some(toast => toast.id === id)) return;

    const promoted = this.queued()[0];
    this.toasts.update(items => items.filter(toast => toast.id !== id).concat(promoted ? [promoted] : []));
    if (promoted) {
      this.queued.update(items => items.slice(1));
      this.startTimer(promoted.id, promoted.duration);
    }
  }

  dismiss(id: string) { this.remove(id); }

  pause(id: string) {
    const timer = this.timers.get(id);
    if (!timer || timer.handle === null) return;
    clearTimeout(timer.handle);
    timer.handle = null;
    timer.remaining = Math.max(0, timer.dueAt - Date.now());
  }

  resume(id: string) {
    const timer = this.timers.get(id);
    if (!timer || timer.handle !== null || timer.remaining <= 0) return;
    this.schedule(id, timer.remaining, timer);
  }

  clearAll() {
    for (const id of Array.from(this.timers.keys())) this.clearTimer(id);
    this.toasts.set([]);
    this.queued.set([]);
  }

  private startTimer(id: string, duration: number) {
    this.clearTimer(id);
    if (duration > 0) this.schedule(id, duration, { handle: null, dueAt: 0, remaining: duration });
  }

  private restartTimer(id: string) {
    const toast = this.toasts().find(item => item.id === id);
    if (toast) this.startTimer(id, toast.duration);
  }

  private schedule(id: string, duration: number, timer: ToastTimer) {
    timer.remaining = duration;
    timer.dueAt = Date.now() + duration;
    timer.handle = setTimeout(() => {
      this.timers.delete(id);
      this.remove(id);
    }, duration);
    this.timers.set(id, timer);
  }

  private clearTimer(id: string) {
    const timer = this.timers.get(id);
    if (!timer) return;
    if (timer.handle !== null) clearTimeout(timer.handle);
    this.timers.delete(id);
  }
}
