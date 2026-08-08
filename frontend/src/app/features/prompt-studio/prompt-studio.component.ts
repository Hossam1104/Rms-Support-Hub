import { Component } from '@angular/core';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { ToolCardComponent } from '../../shared/ui';

interface PromptStudioGenerator {
  title: string;
  description: string;
  route: string;
  icon: string;
  accent: 'brand' | 'info' | 'amber';
  actionLabel: string;
  capabilities: readonly string[];
}

const PROMPT_STUDIO_GENERATORS: readonly PromptStudioGenerator[] = [
  {
    title: 'Bug Refinement',
    description: 'Transform raw defect notes into structured developer-ready QA prompts.',
    route: '/tools/prompt-studio/bugs',
    icon: 'bi-bug',
    accent: 'brand',
    actionLabel: 'Open Bug Refinement',
    capabilities: ['Defect Context', 'Reproduction Steps', 'Evidence']
  },
  {
    title: 'Story Refinement',
    description: 'Transform rough business requests into structured implementation-ready stories.',
    route: '/tools/prompt-studio/stories',
    icon: 'bi-journal-text',
    accent: 'info',
    actionLabel: 'Open Story Refinement',
    capabilities: ['Actor', 'Business Goal', 'Desired Behavior']
  },
  {
    title: 'Test Case Generation',
    description: 'Create structured manual QA test-case prompts from requirements and evidence.',
    route: '/tools/prompt-studio/test-cases',
    icon: 'bi-check2-square',
    accent: 'amber',
    actionLabel: 'Open Test Case Generation',
    capabilities: ['Scenario Type', 'Execution Steps', 'Expected Result']
  }
];

@Component({
  selector: 'app-prompt-studio',
  standalone: true,
  imports: [NavbarComponent, ToolCardComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="prompt-studio" aria-labelledby="prompt-studio-title">
      <div class="prompt-studio__inner">
        <header class="prompt-studio__header">
          <p class="prompt-studio__eyebrow">Deterministic prompt workspace</p>
          <h1 id="prompt-studio-title">QA Prompt Studio</h1>
          <p>Generate structured QA prompts for bug reports, user stories, and manual test cases.</p>
        </header>

        <section class="prompt-studio__grid" aria-label="Prompt generators">
          @for (generator of generators; track generator.route) {
            <app-tool-card
              [title]="generator.title"
              [description]="generator.description"
              [route]="generator.route"
              [icon]="generator.icon"
              [accent]="generator.accent"
              status="available"
              [actionLabel]="generator.actionLabel"
              [capabilities]="generator.capabilities">
            </app-tool-card>
          }
        </section>
      </div>
    </main>
  `,
  styles: [`
    .prompt-studio { min-height: 100vh; padding: calc(var(--navbar-height) + var(--space-7)) var(--space-6) var(--space-8); background: var(--scene-backdrop), var(--surface-page); }
    .prompt-studio__inner { width: min(100%, 1240px); margin: 0 auto; }
    .prompt-studio__header { position: relative; margin-bottom: var(--space-7); padding: var(--space-5) 0 var(--space-6); border-bottom: 1px solid var(--divider); }
    .prompt-studio__header::after { content: ''; position: absolute; bottom: -1px; left: 0; width: 120px; height: 2px; border-radius: var(--radius-pill); background: linear-gradient(90deg, var(--tool-brand-from), var(--tool-brand-to)); }
    .prompt-studio__eyebrow { margin: 0 0 var(--space-3); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    .prompt-studio__header h1 { margin: 0; color: var(--text-primary); font-size: clamp(2rem, 4vw, 2.6rem); font-weight: var(--weight-heavy); letter-spacing: -.02em; line-height: 1.08; }
    .prompt-studio__header > p:last-child { max-width: 680px; margin: var(--space-3) 0 0; color: var(--text-secondary); font-size: var(--text-md); line-height: var(--leading-normal); }
    /* Equal-height peers: the three generators share one card contract. */
    .prompt-studio__grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); grid-auto-rows: 1fr; align-items: stretch; gap: var(--space-5); }
    .prompt-studio__grid > app-tool-card { height: 100%; opacity: 0; animation: prompt-card-in var(--d-slow) var(--ease-out) forwards; }
    .prompt-studio__grid > app-tool-card:nth-child(1) { animation-delay: 0ms; }
    .prompt-studio__grid > app-tool-card:nth-child(2) { animation-delay: 70ms; }
    .prompt-studio__grid > app-tool-card:nth-child(3) { animation-delay: 140ms; }
    @keyframes prompt-card-in {
      from { opacity: 0; transform: translateY(12px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @media (max-width: 1024px) { .prompt-studio__grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 680px) {
      .prompt-studio { padding: calc(var(--navbar-height) + var(--space-5)) var(--space-4) var(--space-7); }
      .prompt-studio__grid { grid-template-columns: 1fr; grid-auto-rows: auto; gap: var(--space-4); }
    }
    @media (prefers-reduced-motion: reduce) {
      :host-context(html:not([data-motion="full"])) .prompt-studio__grid > app-tool-card { animation: none; opacity: 1; transform: none; }
    }
    :host-context(html[data-motion="reduce"]) .prompt-studio__grid > app-tool-card { animation: none; opacity: 1; transform: none; }
  `]
})
export class PromptStudioComponent {
  readonly generators = PROMPT_STUDIO_GENERATORS;
}
