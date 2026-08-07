import { Injectable, inject } from '@angular/core';
import { ToastService } from '../../../core/services/toast.service';

@Injectable({ providedIn: 'root' })
export class ClipboardService {
  private readonly toast = inject(ToastService);

  async copy(value: string, successMessage = 'Copied to clipboard!'): Promise<boolean> {
    try {
      await navigator.clipboard.writeText(value);
      this.toast.showSuccess(successMessage);
      return true;
    } catch {
      this.toast.showError('Copy failed, copy manually.');
      return false;
    }
  }
}
