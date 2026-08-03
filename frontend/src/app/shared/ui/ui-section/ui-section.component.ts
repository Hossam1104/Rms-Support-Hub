import { CommonModule } from '@angular/common';
import { Component, effect, input, output, signal } from '@angular/core';

let nextSectionId = 0;

@Component({
  selector: 'ui-section',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="ui-section" [class.is-collapsed]="!expandedState()" [class.is-disabled]="disabled()" [class.has-issues]="hasIssues()">
      <header class="ui-section__header">
        <button
          type="button"
          class="ui-section__toggle"
          [disabled]="disabled() || !collapsible()"
          [attr.aria-controls]="bodyId"
          [attr.aria-expanded]="collapsible() ? expandedState() : true"
          (click)="toggle()">
          <span class="ui-section__marker" [class.is-complete]="completed() && !hasIssues()" [class.is-issue]="hasIssues()" [attr.aria-label]="hasIssues() ? issueLabel() : completed() ? 'Complete' : 'Incomplete'">
            <i class="bi" [class.bi-check-lg]="completed() && !hasIssues()" [class.bi-exclamation-lg]="hasIssues()" [class.bi-dash]="!completed() && !hasIssues()"></i>
          </span>
          <span class="ui-section__heading">
            <span class="ui-section__title">{{ title() }}</span>
            <span class="ui-section__description" *ngIf="description()">{{ description() }}</span>
          </span>
          <i class="bi ui-section__chevron" *ngIf="collapsible()" [class.bi-chevron-down]="expandedState()" [class.bi-chevron-right]="!expandedState()" aria-hidden="true"></i>
        </button>
        <span class="ui-section__issue" *ngIf="hasIssues()" role="status">
          <i class="bi bi-exclamation-circle" aria-hidden="true"></i>
          {{ issueLabel() }}
        </span>
        <div class="ui-section__actions"><ng-content select="[uiSectionActions]"></ng-content></div>
      </header>
      <div class="ui-section__body" [id]="bodyId" *ngIf="!collapsible() || expandedState()">
        <ng-content></ng-content>
      </div>
    </section>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-section { background: var(--surface-panel); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); color: var(--text-primary); overflow: hidden; }
    .ui-section__header { display: flex; align-items: center; gap: 12px; min-height: 64px; padding: 10px 16px 10px 18px; border-bottom: 1px solid var(--divider); }
    .ui-section.is-collapsed .ui-section__header { border-bottom-color: transparent; }
    .ui-section__toggle { display: flex; align-items: center; gap: 12px; min-width: 0; flex: 1; padding: 0; border: 0; background: transparent; color: inherit; text-align: left; cursor: pointer; font: inherit; }
    .ui-section__toggle:disabled { cursor: default; }
    .ui-section__toggle:focus-visible { outline: none; border-radius: var(--radius-sm); box-shadow: var(--focus-ring); }
    .ui-section__marker { display: grid; place-items: center; width: 26px; height: 26px; flex: 0 0 26px; border: 1px solid var(--border-strong); border-radius: 50%; color: var(--text-muted); font-size: .78rem; }
    .ui-section__marker.is-complete { border-color: var(--state-success-border); background: var(--state-success-bg); color: var(--state-success-fg); }
    .ui-section__marker.is-issue { border-color: var(--state-danger-border); background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .ui-section__heading { display: flex; flex-direction: column; min-width: 0; gap: 2px; }
    .ui-section__title { font-size: 1rem; font-weight: 750; }
    .ui-section__description { overflow: hidden; color: var(--text-secondary); font-size: .8rem; text-overflow: ellipsis; white-space: nowrap; }
    .ui-section__chevron { margin-left: auto; color: var(--text-muted); font-size: .8rem; }
    .ui-section__actions { display: flex; align-items: center; gap: 8px; }
    .ui-section__issue { display: inline-flex; align-items: center; gap: 5px; color: var(--state-danger-fg); font-size: .75rem; font-weight: 750; white-space: nowrap; }
    .ui-section__body { padding: 20px; }
    .ui-section.is-disabled { opacity: .58; }
    @media (max-width: 560px) { .ui-section__header { align-items: flex-start; } .ui-section__actions { align-self: center; } .ui-section__description { white-space: normal; } }
  `]
})
export class UiSectionComponent {
  readonly title = input.required<string>();
  readonly description = input('');
  readonly completed = input(false);
  readonly hasIssues = input(false);
  readonly issueCount = input(0);
  readonly collapsible = input(true);
  readonly expanded = input(true);
  readonly disabled = input(false);
  readonly expandedChange = output<boolean>();

  readonly bodyId = `ui-section-body-${nextSectionId++}`;
  readonly expandedState = signal(true);

  constructor() {
    effect(() => this.expandedState.set(this.expanded()));
  }

  issueLabel(): string {
    const count = this.issueCount();
    return count > 0 ? `${count} issue${count === 1 ? '' : 's'}` : 'Needs attention';
  }

  expand() {
    if (!this.disabled()) this.expandedState.set(true);
  }

  toggle() {
    if (this.disabled() || !this.collapsible()) return;
    const next = !this.expandedState();
    this.expandedState.set(next);
    this.expandedChange.emit(next);
  }
}
