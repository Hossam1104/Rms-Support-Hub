import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { UiCardComponent } from '../ui-card/ui-card.component';

export type ToolCardAccent = 'brand' | 'info' | 'amber' | 'teal';
export type ToolCardStatus = 'available' | 'migration-pending';

/**
 * Base card for a hub tool entry: gradient-accent icon, title, description,
 * and a semantic status badge. Built on the interactive `ui-card`, so hover,
 * keyboard activation (Enter/Space), and the shared focus ring come from the
 * same contract; hover motion moves the card transform only and never blurs
 * the text.
 */
@Component({
  selector: 'app-tool-card',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, UiCardComponent],
  template: `
    <ng-template #cardContent>
      <div class="tool-card__content">
        <div class="tool-card__header">
          <i class="bi tool-card__icon" [class]="icon() + ' tool-card__icon--' + accent()" aria-hidden="true"></i>
          <app-status-badge
            class="tool-card__status"
            [class.tool-card__status--pending]="status() === 'migration-pending'"
            [label]="statusLabel()"
            [variant]="statusVariant()"
            role="status">
          </app-status-badge>
        </div>
        <h3 class="tool-card__title">{{ title() }}</h3>
        <p class="tool-card__description" *ngIf="description()">{{ description() }}</p>
        <div class="tool-card__capabilities" *ngIf="capabilities().length" aria-label="Capabilities">
          <span class="tool-card__capability" *ngFor="let capability of capabilities()">{{ capability }}</span>
        </div>
        <p class="tool-card__availability" *ngIf="availabilityMessage()">{{ availabilityMessage() }}</p>
        <div class="tool-card__action">
          <span>{{ actionLabel() }}</span>
          <i class="bi bi-arrow-up-right" aria-hidden="true"></i>
        </div>
      </div>
    </ng-template>

    <a
      *ngIf="route() && !disabled(); else interactiveCard"
      class="tool-card__link"
      [routerLink]="route()"
      [attr.aria-label]="accessibleLabel()">
      <ui-card class="tool-card" variant="raised">
        <ng-container *ngTemplateOutlet="cardContent"></ng-container>
      </ui-card>
    </a>

    <ng-template #interactiveCard>
      <ui-card
        class="tool-card"
        variant="interactive"
        [disabled]="disabled()"
        [ariaLabel]="accessibleLabel()"
        (activated)="activated.emit()">
        <ng-container *ngTemplateOutlet="cardContent"></ng-container>
      </ui-card>
    </ng-template>
  `,
  styles: [`
    :host { display: block; min-width: 0; height: 100%; }
    .tool-card__link { display: block; height: 100%; color: inherit; text-decoration: none; border-radius: var(--radius-lg); transition: transform var(--transition-normal), box-shadow var(--transition-normal); }
    .tool-card__link:hover { transform: translateY(-3px); }
    .tool-card__link:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .tool-card { height: 100%; }
    .tool-card__content { display: flex; min-height: 276px; height: 100%; flex-direction: column; }
    .tool-card__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); }
    .tool-card__icon { font-size: 1.7rem; line-height: 1; -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
    .tool-card__icon--brand { background: var(--grad-brand); -webkit-background-clip: text; }
    .tool-card__icon--info { background: var(--grad-info); -webkit-background-clip: text; }
    .tool-card__icon--amber { background: var(--grad-amber); -webkit-background-clip: text; }
    .tool-card__icon--teal { background: var(--grad-teal); -webkit-background-clip: text; }
    .tool-card__status { flex-shrink: 0; white-space: nowrap; }
    .tool-card__title { margin: 0 0 var(--space-1); font-size: var(--text-lg); font-weight: var(--weight-bold); line-height: var(--leading-tight); }
    .tool-card__description { margin: 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .tool-card__capabilities { display: flex; flex-wrap: wrap; gap: var(--space-2); margin-top: var(--space-4); }
    .tool-card__capability { padding: 6px 10px; border: 1px solid var(--border-subtle); border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: var(--text-xs); line-height: 1.2; }
    .tool-card__availability { margin: auto 0 0; padding-top: var(--space-4); color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .tool-card__action { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); margin-top: var(--space-4); padding-top: var(--space-4); border-top: 1px solid var(--divider); color: var(--text-accent); font-size: var(--text-sm); font-weight: var(--weight-bold); }
    .tool-card__action i { transition: transform var(--transition-fast); }
    .tool-card__link:hover .tool-card__action i { transform: translate(2px, -2px); }
  `]
})
export class ToolCardComponent {
  readonly title = input.required<string>();
  readonly description = input<string>('');
  readonly icon = input('bi-grid');
  readonly accent = input<ToolCardAccent>('brand');
  readonly status = input<ToolCardStatus>('available');
  readonly route = input<string | null>(null);
  readonly actionLabel = input('Open tool');
  readonly capabilities = input<readonly string[]>([]);
  readonly availabilityMessage = input('');
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);
  readonly activated = output<void>();

  readonly statusLabel = computed(() =>
    this.status() === 'migration-pending' ? 'Migration Pending' : 'Available');
  readonly statusVariant = computed<'success' | 'warning'>(() =>
    this.status() === 'migration-pending' ? 'warning' : 'success');
  readonly accessibleLabel = computed(() => this.ariaLabel() || [
    `${this.actionLabel()}: ${this.title()}`,
    this.statusLabel(),
    this.capabilities().length ? `Capabilities: ${this.capabilities().join(', ')}` : '',
    this.availabilityMessage()
  ].filter(Boolean).join('. '));
}
