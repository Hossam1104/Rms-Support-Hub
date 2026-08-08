import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'ui-toolbar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="ui-toolbar" [class.ui-toolbar--compact]="compact()" [class.ui-toolbar--nowrap]="!wrap()" [attr.role]="role() || null" [attr.aria-label]="ariaLabel() || null">
      <div class="ui-toolbar__start"><ng-content select="[uiToolbarStart]"></ng-content></div>
      <div class="ui-toolbar__center"><ng-content select="[uiToolbarCenter]"></ng-content></div>
      <div class="ui-toolbar__end"><ng-content select="[uiToolbarEnd]"></ng-content></div>
      <ng-content></ng-content>
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .ui-toolbar { display: flex; align-items: center; gap: var(--panel-gap); min-width: 0; flex-wrap: wrap; padding: var(--panel-padding-compact); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-panel); color: var(--text-primary); }
    .ui-toolbar--nowrap { flex-wrap: nowrap; }
    .ui-toolbar--compact { gap: var(--space-2); padding: var(--space-2); border-radius: var(--radius-sm); }
    .ui-toolbar__start, .ui-toolbar__center, .ui-toolbar__end { display: flex; align-items: center; gap: 8px; min-width: 0; }
    .ui-toolbar__start { flex: 1 1 auto; }
    .ui-toolbar__center { flex: 0 1 auto; justify-content: center; }
    .ui-toolbar__end { flex: 1 1 auto; justify-content: flex-end; }
    @media (max-width: 620px) { .ui-toolbar--nowrap { flex-wrap: wrap; } .ui-toolbar__start, .ui-toolbar__center, .ui-toolbar__end { flex-basis: 100%; justify-content: flex-start; } }
  `]
})
export class UiToolbarComponent {
  readonly compact = input(false);
  readonly wrap = input(true);
  readonly role = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
}
