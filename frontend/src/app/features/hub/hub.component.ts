import { Component } from '@angular/core';
import { EmptyStateComponent, ToolCardComponent } from '../../shared/ui';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { QaToolDefinition } from '../../core/models';
import { QA_TOOL_REGISTRY } from './tool-registry';

@Component({
  selector: 'app-hub',
  standalone: true,
  imports: [NavbarComponent, ToolCardComponent, EmptyStateComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="hub-page" aria-labelledby="hub-title">
      <div class="hub-page__inner">
        <header class="hub-intro">
          <p class="hub-eyebrow"><span aria-hidden="true"></span>QA Engineering Workspace</p>
          <h1 id="hub-title">QA Support Hub</h1>
          <p class="hub-intro__description">
            One workspace for QA engineering, online-order support, prompt engineering, and POS maintenance.
          </p>
        </header>

        <section class="hub-tools" aria-labelledby="hub-tools-title">
          <div class="hub-tools__heading">
            <div>
              <p class="hub-tools__eyebrow">Workspace directory</p>
              <h2 id="hub-tools-title">Choose a tool</h2>
            </div>
            <p class="hub-tools__hint">Select a workspace to continue.</p>
          </div>

          @if (tools.length > 0) {
            <div class="hub-tools__grid">
              @for (tool of tools; track tool.id) {
                <app-tool-card
                  [title]="tool.title"
                  [description]="tool.description"
                  [route]="tool.route"
                  [icon]="tool.icon"
                  [accent]="tool.accent"
                  [status]="tool.status"
                  [actionLabel]="tool.actionLabel"
                  [capabilities]="tool.capabilities"
                  [availabilityMessage]="tool.availabilityMessage">
                </app-tool-card>
              }
            </div>
          } @else {
            <div class="hub-tools__empty">
              <app-empty-state
                icon="bi-grid-3x3-gap"
                title="No QA tools are currently configured.">
              </app-empty-state>
            </div>
          }
        </section>
      </div>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100%; }
    .hub-page { min-height: 100vh; padding: calc(var(--navbar-height) + var(--space-7)) var(--space-6) var(--space-8); background: var(--surface-page); }
    .hub-page__inner { width: min(100%, 1240px); margin: 0 auto; }
    .hub-intro { position: relative; margin-bottom: var(--space-7); padding: var(--space-5) 0 var(--space-6); border-bottom: 1px solid var(--divider); }
    .hub-intro::after { content: ''; position: absolute; right: 0; bottom: -1px; left: 0; width: 120px; height: 2px; border-radius: var(--radius-pill); background: var(--grad-brand); }
    .hub-eyebrow, .hub-tools__eyebrow { display: flex; align-items: center; gap: var(--space-2); margin: 0 0 var(--space-3); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); text-transform: uppercase; }
    .hub-eyebrow span { width: 7px; height: 7px; border-radius: 50%; background: var(--accent); box-shadow: 0 0 0 4px var(--accent-soft); }
    .hub-intro h1 { margin: 0; color: var(--text-primary); font-size: 3rem; font-weight: var(--weight-heavy); line-height: 1.08; }
    .hub-intro__description { max-width: 680px; margin: var(--space-3) 0 0; color: var(--text-secondary); font-size: var(--text-md); line-height: var(--leading-normal); }
    .hub-tools__heading { display: flex; align-items: end; justify-content: space-between; gap: var(--space-5); margin-bottom: var(--space-5); }
    .hub-tools__eyebrow { margin-bottom: var(--space-1); color: var(--text-muted); }
    .hub-tools h2 { margin: 0; color: var(--text-primary); font-size: var(--text-xl); line-height: var(--leading-tight); }
    .hub-tools__hint { margin: 0; color: var(--text-muted); font-size: var(--text-sm); }
    .hub-tools__grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); align-items: stretch; gap: var(--space-5); }
    .hub-tools__grid > app-tool-card { opacity: 0; animation: hub-card-in var(--d-slow) var(--ease-out) forwards; }
    .hub-tools__grid > app-tool-card:nth-child(1) { animation-delay: 0ms; }
    .hub-tools__grid > app-tool-card:nth-child(2) { animation-delay: 70ms; }
    .hub-tools__grid > app-tool-card:nth-child(3) { animation-delay: 140ms; }
    .hub-tools__empty { border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); background: var(--surface-panel); }
    @keyframes hub-card-in {
      from { opacity: 0; transform: translateY(12px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @media (max-width: 1024px) {
      .hub-tools__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 680px) {
      .hub-page { padding: calc(var(--navbar-height) + var(--space-5)) var(--space-4) var(--space-7); }
      .hub-intro { margin-bottom: var(--space-6); padding-top: var(--space-3); }
      .hub-intro h1 { font-size: 2.2rem; }
      .hub-tools__heading { align-items: flex-start; flex-direction: column; gap: var(--space-2); }
      .hub-tools__grid { grid-template-columns: 1fr; gap: var(--space-4); }
    }
    @media (prefers-reduced-motion: reduce) {
      :host-context(html:not([data-motion="full"])) .hub-tools__grid > app-tool-card { animation: none; opacity: 1; transform: none; }
    }
    :host-context(html[data-motion="reduce"]) .hub-tools__grid > app-tool-card { animation: none; opacity: 1; transform: none; }
  `]
})
export class HubComponent {
  tools: readonly QaToolDefinition[] = QA_TOOL_REGISTRY;
}
