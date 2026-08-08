import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiCardComponent } from '../ui-card/ui-card.component';

export type ToolCardAccent = 'brand' | 'info' | 'amber' | 'teal';
export type ToolCardStatus = 'available' | 'migration-pending';

/**
 * The navigation card of the shared card system: an identity-tinted icon
 * container, title, status pill, description, capability chips, and a pinned
 * footer action. Peer cards in one grid share `--card-*` geometry and pin the
 * action with `margin-top: auto`, so actions stay aligned no matter how much
 * description or how many capabilities each card carries.
 *
 * Built on the interactive `ui-card`, so hover, keyboard activation
 * (Enter/Space), and the shared focus ring come from the same contract. Hover
 * moves the card transform only and never blurs text; reduced motion drops
 * every transform and leaves the border/shadow feedback in place.
 */
@Component({
  selector: 'app-tool-card',
  standalone: true,
  imports: [CommonModule, RouterLink, UiCardComponent],
  template: `
    <ng-template #cardContent>
      <div class="tool-card__content">
        <div class="tool-card__header">
          <span class="tool-card__icon-shell" aria-hidden="true">
            <i class="bi tool-card__icon" [class]="icon()"></i>
          </span>
          <span
            class="tool-card__status"
            [class.tool-card__status--pending]="status() === 'migration-pending'"
            role="status">
            <span class="tool-card__status-dot" aria-hidden="true"></span>{{ statusLabel() }}
          </span>
        </div>

        <h3 class="tool-card__title">{{ title() }}</h3>
        <p class="tool-card__description" *ngIf="description()">{{ description() }}</p>

        <div class="tool-card__capabilities" *ngIf="capabilities().length" aria-label="Capabilities">
          <span class="tool-card__capability" *ngFor="let capability of capabilities()">{{ capability }}</span>
        </div>

        <p class="tool-card__availability" *ngIf="availabilityMessage()">{{ availabilityMessage() }}</p>

        <div class="tool-card__action">
          <span>{{ actionLabel() }}</span>
          <i class="bi bi-arrow-right" aria-hidden="true"></i>
        </div>
      </div>
    </ng-template>

    <a
      *ngIf="route() && !disabled(); else interactiveCard"
      class="tool-card__link"
      [routerLink]="route()"
      [attr.aria-label]="accessibleLabel()">
      <ui-card class="tool-card" [class]="accentClass()" variant="raised">
        <ng-container *ngTemplateOutlet="cardContent"></ng-container>
      </ui-card>
    </a>

    <ng-template #interactiveCard>
      <ui-card
        class="tool-card"
        [class]="accentClass()"
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

    /* Identity: one two-stop accent drives the icon, edge light, and hover glow. */
    .tool-card--brand { --card-accent-from: var(--tool-brand-from); --card-accent-to: var(--tool-brand-to); }
    .tool-card--info { --card-accent-from: var(--tool-info-from); --card-accent-to: var(--tool-info-to); }
    .tool-card--amber { --card-accent-from: var(--tool-amber-from); --card-accent-to: var(--tool-amber-to); }
    .tool-card--teal { --card-accent-from: var(--tool-teal-from); --card-accent-to: var(--tool-teal-to); }

    .tool-card__link { display: block; height: 100%; color: inherit; text-decoration: none; border-radius: var(--card-radius); }
    .tool-card__link:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .tool-card { height: 100%; }

    /* Card surface + top edge light, applied through ui-card's own element. */
    :host ::ng-deep .tool-card .ui-card {
      height: 100%;
      border-color: var(--card-border);
      border-radius: var(--card-radius);
      background: var(--card-surface);
      box-shadow: var(--card-shadow);
      transition: border-color var(--transition-fast), box-shadow var(--transition-normal), transform var(--transition-normal);
    }
    :host ::ng-deep .tool-card .ui-card::before {
      inset: 0 0 auto 0;
      width: auto;
      height: 2px;
      background: linear-gradient(90deg, var(--card-accent-from), var(--card-accent-to));
      opacity: .5;
      transition: opacity var(--transition-fast);
    }
    :host ::ng-deep .tool-card .ui-card__body { display: flex; height: 100%; padding: var(--card-padding); }

    .tool-card__content { display: flex; width: 100%; min-height: var(--card-min-height); flex-direction: column; }
    .tool-card__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); margin-bottom: var(--card-gap); }

    .tool-card__icon-shell {
      display: grid;
      place-items: center;
      width: 46px;
      height: 46px;
      border: 1px solid var(--card-border);
      border-radius: var(--radius-md);
      background: var(--card-sheen), var(--surface-interactive);
      transition: transform var(--transition-normal), border-color var(--transition-fast);
    }
    .tool-card__icon {
      font-size: 1.35rem;
      line-height: 1;
      background: linear-gradient(135deg, var(--card-accent-from), var(--card-accent-to));
      -webkit-background-clip: text;
      background-clip: text;
      -webkit-text-fill-color: transparent;
    }

    .tool-card__status {
      display: inline-flex;
      align-items: center;
      flex-shrink: 0;
      gap: var(--space-2);
      padding: 5px 11px;
      border: 1px solid var(--state-success-border);
      border-radius: var(--radius-pill);
      background: var(--state-success-bg);
      color: var(--state-success-fg);
      font-size: var(--text-xs);
      font-weight: var(--weight-bold);
      white-space: nowrap;
    }
    .tool-card__status-dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
    .tool-card__status--pending { border-color: var(--state-neutral-border); background: var(--state-neutral-bg); color: var(--text-muted); }

    .tool-card__title { margin: 0 0 var(--space-2); color: var(--text-primary); font-size: var(--text-lg); font-weight: var(--weight-bold); line-height: var(--leading-tight); }
    .tool-card__description { margin: 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }

    .tool-card__capabilities { display: flex; flex-wrap: wrap; gap: var(--space-2); margin-top: var(--card-gap); }
    .tool-card__capability { padding: 5px 10px; border: 1px solid var(--card-border); border-radius: var(--radius-pill); background: var(--surface-interactive); color: var(--text-secondary); font-size: var(--text-xs); line-height: 1.25; }

    /* margin-top:auto on the first pinned block pushes the footer group down. */
    .tool-card__availability { margin: auto 0 0; padding-top: var(--card-gap); color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .tool-card__action {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      margin-top: auto;
      padding-top: var(--card-gap);
      color: var(--text-accent);
      font-size: var(--text-sm);
      font-weight: var(--weight-bold);
    }
    .tool-card__availability + .tool-card__action { margin-top: var(--space-3); }
    .tool-card__action i { transition: transform var(--transition-fast); }

    /* Hover and keyboard focus get the same visual prominence. */
    :host ::ng-deep .tool-card__link:hover .ui-card,
    :host ::ng-deep .tool-card__link:focus-visible .ui-card,
    :host ::ng-deep .tool-card .ui-card--interactive:hover,
    :host ::ng-deep .tool-card .ui-card--interactive:focus-visible {
      transform: translateY(var(--card-lift));
      border-color: var(--card-border-hover);
      box-shadow: var(--card-shadow-hover);
    }
    :host ::ng-deep .tool-card__link:hover .ui-card::before,
    :host ::ng-deep .tool-card__link:focus-visible .ui-card::before,
    :host ::ng-deep .tool-card .ui-card--interactive:hover::before,
    :host ::ng-deep .tool-card .ui-card--interactive:focus-visible::before { opacity: 1; }
    .tool-card__link:hover .tool-card__icon-shell { transform: translateY(-2px); border-color: var(--card-border-hover); }
    .tool-card__link:hover .tool-card__action i { transform: translateX(3px); }
    :host ::ng-deep .tool-card .ui-card--disabled,
    :host ::ng-deep .tool-card .ui-card--disabled:hover { transform: none; box-shadow: var(--card-shadow); }

    @media (prefers-reduced-motion: reduce) {
      :host-context(html:not([data-motion="full"])) ::ng-deep .tool-card__link:hover .ui-card,
      :host-context(html:not([data-motion="full"])) ::ng-deep .tool-card__link:focus-visible .ui-card,
      :host-context(html:not([data-motion="full"])) ::ng-deep .tool-card .ui-card--interactive:hover,
      :host-context(html:not([data-motion="full"])) ::ng-deep .tool-card .ui-card--interactive:focus-visible,
      :host-context(html:not([data-motion="full"])) .tool-card__link:hover .tool-card__icon-shell,
      :host-context(html:not([data-motion="full"])) .tool-card__link:hover .tool-card__action i { transform: none; }
    }
    :host-context(html[data-motion="reduce"]) ::ng-deep .tool-card__link:hover .ui-card,
    :host-context(html[data-motion="reduce"]) ::ng-deep .tool-card__link:focus-visible .ui-card,
    :host-context(html[data-motion="reduce"]) ::ng-deep .tool-card .ui-card--interactive:hover,
    :host-context(html[data-motion="reduce"]) ::ng-deep .tool-card .ui-card--interactive:focus-visible,
    :host-context(html[data-motion="reduce"]) .tool-card__link:hover .tool-card__icon-shell,
    :host-context(html[data-motion="reduce"]) .tool-card__link:hover .tool-card__action i { transform: none; }
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

  readonly accentClass = computed(() => `tool-card--${this.accent()}`);
  readonly statusLabel = computed(() =>
    this.status() === 'migration-pending' ? 'Coming Soon' : 'Available');
  readonly accessibleLabel = computed(() => this.ariaLabel() || [
    `${this.actionLabel()}: ${this.title()}`,
    this.statusLabel(),
    this.capabilities().length ? `Capabilities: ${this.capabilities().join(', ')}` : '',
    this.availabilityMessage()
  ].filter(Boolean).join('. '));
}
