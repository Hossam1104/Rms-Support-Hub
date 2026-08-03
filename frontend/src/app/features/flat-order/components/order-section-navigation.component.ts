import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface OrderBuilderSection {
  id: string;
  label: string;
  description: string;
  completed: boolean;
  hasIssues: boolean;
  issueCount: number;
}

/**
 * Compact, keyboard-first navigation for the flat-order workflow. The parent
 * owns section visibility and active-section observation; this component only
 * renders the stable order and emits a requested anchor.
 */
@Component({
  selector: 'app-order-section-navigation',
  standalone: true,
  imports: [CommonModule],
  template: `
    <nav
      class="section-navigation"
      aria-label="Order workflow sections"
      data-testid="order-section-navigation"
    >
      <span class="section-navigation__label">Workflow</span>
      <div class="section-navigation__items" role="list">
        <button
          *ngFor="let section of sections; trackBy: trackById"
          type="button"
          class="section-navigation__item"
          [class.is-active]="section.id === activeSectionId"
          [class.has-issues]="section.hasIssues"
          [attr.aria-current]="section.id === activeSectionId ? 'step' : null"
          [attr.aria-label]="
            section.hasIssues
              ? section.label + ', ' + issueLabel(section.issueCount)
              : section.label
          "
          [attr.data-section-id]="section.id"
          (click)="sectionSelected.emit(section.id)"
        >
          <span
            class="section-navigation__state"
            [class.is-complete]="section.completed && !section.hasIssues"
            [class.is-issue]="section.hasIssues"
            aria-hidden="true"
          >
            <i
              class="bi"
              [class.bi-check-lg]="section.completed && !section.hasIssues"
              [class.bi-exclamation-lg]="section.hasIssues"
              [class.bi-dash]="!section.completed && !section.hasIssues"
            ></i>
          </span>
          <span class="section-navigation__text">{{ section.label }}</span>
          <span class="section-navigation__count" *ngIf="section.hasIssues">{{
            section.issueCount
          }}</span>
        </button>
      </div>
    </nav>
  `,
  styles: [
    `
      :host {
        display: block;
        min-width: 0;
      }
      .section-navigation {
        display: flex;
        align-items: center;
        gap: 14px;
        min-width: 0;
        padding: 8px;
        border: 1px solid var(--border-subtle);
        border-radius: var(--radius-lg);
        background: var(--surface-raised);
        box-shadow: var(--shadow-sm);
      }
      .section-navigation__label {
        flex: 0 0 auto;
        padding-inline: 8px;
        color: var(--text-muted);
        font-size: 0.7rem;
        font-weight: 850;
        letter-spacing: 0.1em;
        text-transform: uppercase;
      }
      .section-navigation__items {
        display: flex;
        align-items: center;
        gap: 4px;
        min-width: 0;
        overflow-x: auto;
        scrollbar-width: thin;
      }
      .section-navigation__item {
        display: inline-flex;
        align-items: center;
        gap: 7px;
        min-height: 36px;
        flex: 0 0 auto;
        padding: 0 10px;
        border: 1px solid transparent;
        border-radius: var(--radius-sm);
        background: transparent;
        color: var(--text-secondary);
        cursor: pointer;
        font: inherit;
        font-size: 0.78rem;
        font-weight: 750;
        white-space: nowrap;
        transition:
          background var(--transition-fast),
          border-color var(--transition-fast),
          color var(--transition-fast);
      }
      .section-navigation__item:hover {
        background: var(--surface-hover);
        color: var(--text-primary);
      }
      .section-navigation__item:focus-visible {
        outline: none;
        box-shadow: var(--focus-ring);
      }
      .section-navigation__item.is-active {
        border-color: var(--border-focus);
        background: var(--surface-interactive);
        color: var(--text-primary);
      }
      .section-navigation__item.has-issues {
        color: var(--state-danger-fg);
      }
      .section-navigation__state {
        display: grid;
        place-items: center;
        width: 18px;
        height: 18px;
        border: 1px solid var(--border-strong);
        border-radius: 50%;
        color: var(--text-muted);
        font-size: 0.62rem;
      }
      .section-navigation__state.is-complete {
        border-color: var(--state-success-border);
        background: var(--state-success-bg);
        color: var(--state-success-fg);
      }
      .section-navigation__state.is-issue {
        border-color: var(--state-danger-border);
        background: var(--state-danger-bg);
        color: var(--state-danger-fg);
      }
      .section-navigation__count {
        display: inline-grid;
        min-width: 18px;
        height: 18px;
        place-items: center;
        padding-inline: 4px;
        border-radius: var(--radius-pill);
        background: var(--state-danger-bg);
        color: var(--state-danger-fg);
        font-size: 0.65rem;
      }
      @media (max-width: 767px) {
        .section-navigation {
          align-items: flex-start;
          flex-direction: column;
          gap: 6px;
          padding: 8px 10px;
        }
        .section-navigation__label {
          padding-inline: 2px;
        }
        .section-navigation__items {
          width: 100%;
        }
      }
      @media (prefers-reduced-motion: reduce) {
        .section-navigation__item {
          transition: none;
        }
      }
    `,
  ],
})
export class OrderSectionNavigationComponent {
  @Input() sections: OrderBuilderSection[] = [];
  @Input() activeSectionId = '';
  @Output() sectionSelected = new EventEmitter<string>();

  trackById(_index: number, section: OrderBuilderSection): string {
    return section.id;
  }

  issueLabel(count: number): string {
    return `${count} issue${count === 1 ? '' : 's'}`;
  }
}
