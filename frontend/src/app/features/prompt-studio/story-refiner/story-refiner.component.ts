import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';
import { StoryPromptBuilder } from '../builders/story-prompt.builder';
import {
  DEFAULT_STORY_ACCEPTANCE_CRITERIA_STYLE,
  DEFAULT_STORY_DETAIL_LEVEL,
  DEFAULT_STORY_TARGET_FORMAT,
  EMPTY_STORY_PROMPT_INPUT,
  SAMPLE_STORY_PROMPT_INPUT,
  STORY_ACCEPTANCE_CRITERIA_STYLES,
  STORY_DETAIL_LEVELS,
  STORY_TARGET_FORMATS,
  StoryPromptInput,
  normalizeStoryPromptInput
} from '../models/story-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { GeneratorWorkspaceComponent } from '../components/generator-workspace/generator-workspace.component';
import { PromptTextareaComponent } from '../components/prompt-textarea/prompt-textarea.component';

@Component({
  selector: 'app-story-refiner',
  standalone: true,
  imports: [ReactiveFormsModule, GeneratorWorkspaceComponent, PromptTextareaComponent, UiFieldComponent, UiInputComponent, UiSelectComponent],
  template: `
    <app-generator-workspace
      title="Story Refinement"
      description="Transform rough business requests into structured implementation-ready stories."
      formTitle="Story Details"
      formSubtitle="Capture facts, boundaries, and the intended outcome"
      filename="story-prompt"
      [prompt]="generatedPrompt()"
      (generate)="generate()"
      (sample)="loadSample()"
      (clear)="clearForm()">

      <form [formGroup]="form" autocomplete="off" class="story-form">
        <section class="story-form__section" aria-labelledby="story-request-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">A</span>
            <div>
              <h3 id="story-request-heading">Request</h3>
              <p>Start with the request and the intended actor or outcome. Missing context stays a clarification.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field class="story-form__field--full" label="Raw Story / Request" forId="story-raw" [hint]="rawStoryCounter()">
              <app-prompt-textarea textareaId="story-raw" formControlName="rawStory" [rows]="6" placeholder="Paste the rough request, notes, or stakeholder wording"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Proposed Title" forId="story-title">
              <ui-input inputId="story-title" formControlName="title" placeholder="Short implementation title"></ui-input>
            </ui-field>
            <ui-field label="Actor / Persona" forId="story-actor">
              <ui-input inputId="story-actor" formControlName="actor" placeholder="Who needs this capability?"></ui-input>
            </ui-field>
            <ui-field class="story-form__field--full" label="Business Goal" forId="story-goal">
              <ui-input inputId="story-goal" formControlName="businessGoal" placeholder="What outcome should this achieve?"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-context-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">B</span>
            <div>
              <h3 id="story-context-heading">Business Context</h3>
              <p>Separate the problem, current behavior, and desired behavior so the change remains observable.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field class="story-form__field--full" label="Problem Statement" forId="story-problem">
              <app-prompt-textarea textareaId="story-problem" formControlName="problemStatement" [rows]="4" placeholder="What problem or user need is being addressed?"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Current Behavior" forId="story-current">
              <app-prompt-textarea textareaId="story-current" formControlName="currentBehavior" [rows]="4" placeholder="What happens today?"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Desired Behavior" forId="story-desired" [hint]="desiredBehaviorCounter()">
              <app-prompt-textarea textareaId="story-desired" formControlName="desiredBehavior" [rows]="4" placeholder="What should happen in observable terms?"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-scope-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">C</span>
            <div>
              <h3 id="story-scope-heading">Scope</h3>
              <p>Bound the work and preserve supplied rules without creating new exclusions or policies.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field label="In Scope" forId="story-scope">
              <app-prompt-textarea textareaId="story-scope" formControlName="scope" [rows]="5" placeholder="What is included in this request?"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Out of Scope" forId="story-out-of-scope">
              <app-prompt-textarea textareaId="story-out-of-scope" formControlName="outOfScope" [rows]="5" placeholder="What is explicitly excluded, if known?"></app-prompt-textarea>
            </ui-field>
            <ui-field class="story-form__field--full" label="Business Rules" forId="story-rules" [hint]="businessRulesCounter()">
              <app-prompt-textarea textareaId="story-rules" formControlName="businessRules" [rows]="6" placeholder="List confirmed rules exactly; leave unknown rules for clarification"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-dependencies-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">D</span>
            <div>
              <h3 id="story-dependencies-heading">Dependencies</h3>
              <p>Record surrounding systems, assumptions, and design references without storing files or secrets.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field label="Dependencies" forId="story-dependencies">
              <app-prompt-textarea textareaId="story-dependencies" formControlName="dependencies" [rows]="4" placeholder="Systems, teams, data, or prerequisites this depends on"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Assumptions" forId="story-assumptions" hint="Provided assumptions remain labeled and are not promoted to acceptance criteria.">
              <app-prompt-textarea textareaId="story-assumptions" formControlName="assumptions" [rows]="4" placeholder="What has been provided as an assumption?"></app-prompt-textarea>
            </ui-field>
            <ui-field class="story-form__field--full" label="UX / Design References" forId="story-ux-references">
              <app-prompt-textarea textareaId="story-ux-references" formControlName="uxReferences" [rows]="4" placeholder="Relevant screens, patterns, references, or known design constraints"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-technical-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">E</span>
            <div>
              <h3 id="story-technical-heading">Technical / Non-Functional</h3>
              <p>Add supplied considerations or useful questions; the generated prompt will not invent targets.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field label="API / Data Considerations" forId="story-api-data">
              <app-prompt-textarea textareaId="story-api-data" formControlName="apiDataConsiderations" [rows]="4" placeholder="Known data fields, contracts, or integration considerations"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Security Considerations" forId="story-security">
              <app-prompt-textarea textareaId="story-security" formControlName="securityConsiderations" [rows]="4" placeholder="Known access, privacy, or audit considerations"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Performance Considerations" forId="story-performance">
              <app-prompt-textarea textareaId="story-performance" formControlName="performanceConsiderations" [rows]="4" placeholder="Known targets or performance questions"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Accessibility / Localization Considerations" forId="story-accessibility">
              <app-prompt-textarea textareaId="story-accessibility" formControlName="accessibilityLocalizationConsiderations" [rows]="4" placeholder="Keyboard, screen reader, language, RTL, or formatting needs"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-output-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">F</span>
            <div>
              <h3 id="story-output-heading">Output Options</h3>
              <p>Choose acceptance criteria style, depth, paste target, and optional quality sections.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field label="Acceptance Criteria Style" forId="story-acceptance-style">
              <ui-select selectId="story-acceptance-style" formControlName="acceptanceCriteriaStyle" [options]="acceptanceCriteriaStyleOptions"></ui-select>
            </ui-field>
            <ui-field label="Detail Level" forId="story-detail-level">
              <ui-select selectId="story-detail-level" formControlName="detailLevel" [options]="detailLevelOptions"></ui-select>
            </ui-field>
            <ui-field class="story-form__field--full" label="Target Format" forId="story-target-format">
              <ui-select selectId="story-target-format" formControlName="targetFormat" [options]="targetFormatOptions"></ui-select>
            </ui-field>
          </div>
          <fieldset class="story-form__options">
            <legend>Optional output sections</legend>
            <div class="story-form__checks">
              <label class="story-form__check" for="story-open-questions"><input id="story-open-questions" type="checkbox" formControlName="includeOpenQuestions"><span>Open Questions</span></label>
              <label class="story-form__check" for="story-qa-impact"><input id="story-qa-impact" type="checkbox" formControlName="includeQaImpact"><span>QA Impact</span></label>
              <label class="story-form__check" for="story-ready"><input id="story-ready" type="checkbox" formControlName="includeDefinitionOfReady"><span>Definition of Ready</span></label>
              <label class="story-form__check" for="story-test-coverage"><input id="story-test-coverage" type="checkbox" formControlName="includeSuggestedTestCoverage"><span>Suggested Test Coverage</span></label>
            </div>
          </fieldset>
        </section>
      </form>
    </app-generator-workspace>
  `,
  styles: [`
    .story-form { display: flex; flex-direction: column; gap: var(--space-4); }
    .story-form__section { display: flex; flex-direction: column; gap: var(--space-4); padding: var(--space-4); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); }
    .story-form__section-header { display: flex; align-items: flex-start; gap: var(--space-3); }
    .story-form__section-kicker { display: inline-flex; flex: 0 0 auto; align-items: center; justify-content: center; width: 28px; height: 28px; border: 1px solid var(--state-info-border); border-radius: var(--radius-sm); background: var(--state-info-bg); color: var(--state-info-fg); font-size: var(--text-xs); font-weight: var(--weight-heavy); }
    .story-form__section-header h3 { margin: 0; color: var(--text-primary); font-size: var(--text-md); }
    .story-form__section-header p { margin: 3px 0 0; color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .story-form__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-4); }
    .story-form__field--full { grid-column: 1 / -1; }
    .story-form__options { margin: 0; padding: var(--space-3); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); }
    .story-form__options legend { padding: 0 var(--space-2); color: var(--text-secondary); font-size: var(--text-sm); font-weight: var(--weight-bold); }
    .story-form__checks { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-3); }
    .story-form__check { display: flex; align-items: center; gap: var(--space-2); min-width: 0; color: var(--text-secondary); font-size: var(--text-sm); cursor: pointer; }
    .story-form__check input { width: 16px; height: 16px; flex: 0 0 auto; accent-color: var(--accent); }
    @media (max-width: 760px) { .story-form__grid, .story-form__checks { grid-template-columns: 1fr; } .story-form__field--full { grid-column: auto; } }
  `]
})
export class StoryRefinerComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly storage = inject(PromptStorageService);
  private readonly toast = inject(ToastService);
  private readonly builder = new StoryPromptBuilder();
  private readonly destroy$ = new Subject<void>();
  private readonly draftChanges$ = new Subject<{ value: StoryPromptInput; generation: number }>();
  private draftGeneration = 0;

  readonly generatedPrompt = signal('');
  readonly rawStoryCounter = signal('0 chars');
  readonly desiredBehaviorCounter = signal('0 chars');
  readonly businessRulesCounter = signal('0 chars');
  readonly acceptanceCriteriaStyleOptions: UiSelectOption[] = STORY_ACCEPTANCE_CRITERIA_STYLES.map(value => ({ value, label: value }));
  readonly detailLevelOptions: UiSelectOption[] = STORY_DETAIL_LEVELS.map(value => ({ value, label: value }));
  readonly targetFormatOptions: UiSelectOption[] = STORY_TARGET_FORMATS.map(value => ({ value, label: value }));

  readonly form = this.fb.nonNullable.group({
    rawStory: [''],
    title: [''],
    actor: [''],
    businessGoal: [''],
    problemStatement: [''],
    currentBehavior: [''],
    desiredBehavior: [''],
    scope: [''],
    outOfScope: [''],
    businessRules: [''],
    dependencies: [''],
    assumptions: [''],
    uxReferences: [''],
    apiDataConsiderations: [''],
    securityConsiderations: [''],
    performanceConsiderations: [''],
    accessibilityLocalizationConsiderations: [''],
    acceptanceCriteriaStyle: [DEFAULT_STORY_ACCEPTANCE_CRITERIA_STYLE as StoryPromptInput['acceptanceCriteriaStyle']],
    detailLevel: [DEFAULT_STORY_DETAIL_LEVEL as StoryPromptInput['detailLevel']],
    targetFormat: [DEFAULT_STORY_TARGET_FORMAT as StoryPromptInput['targetFormat']],
    includeOpenQuestions: [EMPTY_STORY_PROMPT_INPUT.includeOpenQuestions],
    includeQaImpact: [EMPTY_STORY_PROMPT_INPUT.includeQaImpact],
    includeDefinitionOfReady: [EMPTY_STORY_PROMPT_INPUT.includeDefinitionOfReady],
    includeSuggestedTestCoverage: [EMPTY_STORY_PROMPT_INPUT.includeSuggestedTestCoverage]
  });

  ngOnInit(): void {
    const draft = this.storage.load<StoryPromptInput>('story');
    this.form.setValue(normalizeStoryPromptInput(draft));

    this.form.controls.detailLevel.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(detailLevel => {
      this.form.patchValue({
        includeQaImpact: detailLevel !== 'Concise',
        includeDefinitionOfReady: detailLevel === 'Deep',
        includeSuggestedTestCoverage: detailLevel === 'Deep'
      }, { emitEvent: false });
    });

    this.updateCounters();
    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.updateCounters();
      this.draftChanges$.next({ value: this.form.getRawValue(), generation: this.draftGeneration });
    });
    this.draftChanges$.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(change => {
      if (change.generation === this.draftGeneration) this.storage.save('story', change.value);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  generate(): void {
    this.generatedPrompt.set(this.builder.build(this.form.getRawValue()));
    this.toast.showSuccess('Story prompt compiled!');
  }

  loadSample(): void {
    this.draftGeneration += 1;
    this.form.setValue(SAMPLE_STORY_PROMPT_INPUT);
    this.storage.save('story', this.form.getRawValue());
    this.generatedPrompt.set('');
    this.toast.showSuccess('Sample story data loaded');
  }

  clearForm(): void {
    this.draftGeneration += 1;
    this.form.setValue(EMPTY_STORY_PROMPT_INPUT, { emitEvent: false });
    this.updateCounters();
    this.storage.clear('story');
    this.generatedPrompt.set('');
    this.toast.showSuccess('Story form cleared');
  }

  private updateCounters(): void {
    this.rawStoryCounter.set(`${this.form.controls.rawStory.value.length} chars`);
    this.desiredBehaviorCounter.set(`${this.form.controls.desiredBehavior.value.length} chars`);
    this.businessRulesCounter.set(`${this.form.controls.businessRules.value.length} chars`);
  }
}
