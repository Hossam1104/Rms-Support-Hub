import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-copy-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button type="button" class="copy-btn" (click)="copy()" [title]="copied() ? 'Copied!' : 'Copy'">
      <i class="bi" [class.bi-clipboard]="!copied()" [class.bi-check2]="copied()"></i>
      <span *ngIf="label">{{ copied() ? 'Copied' : label }}</span>
    </button>
  `,
  styles: [`
    .copy-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: none;
      border: none;
      color: var(--text-secondary);
      cursor: pointer;
      font-size: 0.85rem;
      padding: 2px 6px;
      border-radius: var(--radius-sm);
      transition: color var(--transition-fast), background var(--transition-fast);
    }
    .copy-btn:hover { color: var(--text-primary); background: var(--surface-hover); }
  `]
})
export class CopyButtonComponent {
  @Input({ required: true }) value!: string;
  @Input() label?: string;

  copied = signal(false);

  copy() {
    navigator.clipboard.writeText(this.value);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1600);
  }
}
