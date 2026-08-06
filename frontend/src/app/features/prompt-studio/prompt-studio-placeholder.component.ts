import { Component } from '@angular/core';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import { EmptyStateComponent, PageHeaderComponent } from '../../shared/ui';

/** Session 01 route placeholder. The actual Bug Refiner, Story Refiner, and
 * Test Case Generator are migrated from prompt_generator/index.html in a
 * later session; this page only reserves /tools/prompt-studio. */
@Component({
  selector: 'app-prompt-studio-placeholder',
  standalone: true,
  imports: [NavbarComponent, PageHeaderComponent, EmptyStateComponent],
  template: `
    <app-navbar></app-navbar>

    <main class="placeholder-container">
      <app-page-header title="QA Prompt Studio" subtitle="Refine raw bugs into developer-ready reports, refine rough stories, and generate detailed test cases."></app-page-header>

      <app-empty-state icon="bi-tools" title="Migration in progress"
        description="The Prompt Studio workspace is being migrated into the QA Support Hub. The generators arrive in a later session.">
      </app-empty-state>
    </main>
  `,
  styles: [`
    .placeholder-container {
      margin-top: var(--navbar-height);
      padding: 40px 40px 60px;
      max-width: 1300px;
      margin-left: auto;
      margin-right: auto;
    }
    @media (max-width: 640px) {
      .placeholder-container { padding: 24px 16px 40px; }
    }
  `]
})
export class PromptStudioPlaceholderComponent {}
