import { Component, input } from '@angular/core';
import { PromptQualityResult } from '../../services/prompt-quality.service';

@Component({
  selector: 'app-prompt-quality-panel',
  standalone: true,
  template: `
    <section class="quality-panel" aria-labelledby="prompt-quality-title">
      <header class="quality-panel__header">
        <div class="quality-panel__title">
          <span class="quality-panel__icon" aria-hidden="true"><i class="bi bi-clipboard2-check"></i></span>
          <div>
            <h2 id="prompt-quality-title">Prompt Quality</h2>
            <p>{{ result().findings.length }} suggestion{{ result().findings.length === 1 ? '' : 's' }} · advisory only</p>
          </div>
        </div>
        <div class="quality-panel__score">
          <span>Score</span>
          <strong [attr.aria-label]="'Prompt quality score: ' + result().score + ' percent'">{{ result().score }}%</strong>
        </div>
      </header>
      <progress class="quality-panel__progress" max="100" [value]="result().score" aria-label="Prompt quality score"></progress>
      <div class="quality-panel__facts">
        <p><span>Facts</span> {{ result().facts.length ? result().facts.join(', ') : 'None supplied yet' }}</p>
        <p><span>Missing</span> {{ result().missingFields.length ? result().missingFields.join(', ') : 'None detected' }}</p>
        <p><span>Assumptions</span> {{ result().assumptions.length ? result().assumptions.join(', ') : 'None detected' }}</p>
        <p><span>Allowed inference</span> {{ result().allowedInference.join(' · ') }}</p>
      </div>
      @if (result().sensitiveContent) {
        <p class="quality-panel__sensitive"><i class="bi bi-shield-exclamation" aria-hidden="true"></i> Remove credentials, tokens, secrets, and private file contents before sharing.</p>
      } @else {
        <p class="quality-panel__notice"><i class="bi bi-shield-check" aria-hidden="true"></i> Keep credentials and private file contents out of prompts.</p>
      }
      @if (result().findings.length) {
        <ul class="quality-panel__findings">
          @for (finding of visibleFindings(); track $index) {
            <li [class.quality-panel__finding--info]="finding.severity === 'info'" [title]="finding.recommendation">
              <i class="bi" [class.bi-exclamation-triangle]="finding.severity === 'warning'" [class.bi-info-circle]="finding.severity === 'info'" aria-hidden="true"></i>
              <span><strong>{{ finding.field }}:</strong> {{ finding.message }}</span>
            </li>
          }
        </ul>
        @if (result().findings.length > visibleFindings().length) {
          <p class="quality-panel__more">{{ result().findings.length - visibleFindings().length }} more suggestions remain advisory.</p>
        }
      } @else {
        <p class="quality-panel__clear"><i class="bi bi-check-circle" aria-hidden="true"></i> No deterministic suggestions for the supplied information.</p>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .quality-panel { display: flex; flex-direction: column; gap: var(--space-2); padding: var(--panel-padding-compact) var(--panel-padding); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-muted); }
    .quality-panel__header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); }
    .quality-panel__title { display: flex; align-items: center; min-width: 0; gap: var(--space-2); }
    .quality-panel__icon { display: inline-grid; flex: 0 0 var(--control-height-compact); width: var(--control-height-compact); height: var(--control-height-compact); place-items: center; border: 1px solid var(--border-subtle); border-radius: var(--radius-sm); background: var(--surface-interactive); color: var(--text-accent); }
    .quality-panel__header h2 { margin: 0; color: var(--text-primary); font-size: var(--text-sm); }
    .quality-panel__header p, .quality-panel__more, .quality-panel__clear, .quality-panel__notice, .quality-panel__sensitive { margin: var(--space-1) 0 0; color: var(--text-muted); font-size: var(--text-xs); line-height: 1.35; }
    .quality-panel__score { display: flex; align-items: flex-end; flex-direction: column; gap: 2px; }
    .quality-panel__score span { color: var(--text-muted); font-size: .65rem; font-weight: var(--weight-semibold); text-transform: uppercase; }
    .quality-panel__score strong { color: var(--text-accent); font-size: var(--text-md); line-height: 1; }
    .quality-panel__progress { display: block; width: 100%; height: 5px; accent-color: var(--accent); }
    .quality-panel__facts { display: grid; gap: var(--space-1); color: var(--text-muted); font-size: var(--text-xs); line-height: 1.35; }
    .quality-panel__facts p { margin: 0; overflow-wrap: anywhere; }
    .quality-panel__facts span { color: var(--text-secondary); font-weight: var(--weight-bold); }
    .quality-panel__findings { display: grid; gap: 4px; margin: 0; padding: 0; list-style: none; }
    .quality-panel__findings li { display: flex; align-items: flex-start; gap: 7px; color: var(--state-warning-fg); font-size: var(--text-xs); line-height: 1.35; }
    .quality-panel__findings li i { flex: 0 0 auto; margin-top: 1px; }
    .quality-panel__findings li strong { font-weight: var(--weight-bold); }
    .quality-panel__finding--info { color: var(--state-info-fg) !important; }
    .quality-panel__sensitive { color: var(--state-danger-fg); }
    .quality-panel__sensitive i, .quality-panel__notice i, .quality-panel__clear i { margin-right: 4px; }
    .quality-panel__clear { color: var(--state-success-fg); }
  `]
})
export class PromptQualityPanelComponent {
  readonly result = input.required<PromptQualityResult>();

  visibleFindings() {
    return this.result().findings.slice(0, 4);
  }
}
