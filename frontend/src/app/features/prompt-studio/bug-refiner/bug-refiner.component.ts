import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';
import { BugPromptBuilder } from '../builders/bug-prompt.builder';
import {
  BUG_DETAIL_LEVELS,
  BUG_TARGET_FORMATS,
  BugPromptInput,
  EMPTY_BUG_PROMPT_INPUT,
  SAMPLE_BUG_PROMPT_INPUT,
  normalizeBugPromptInput
} from '../models/bug-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { GeneratorWorkspaceComponent } from '../components/generator-workspace/generator-workspace.component';
import { PromptTextareaComponent } from '../components/prompt-textarea/prompt-textarea.component';

@Component({
  selector: 'app-bug-refiner',
  standalone: true,
  imports: [ReactiveFormsModule, GeneratorWorkspaceComponent, PromptTextareaComponent, UiFieldComponent, UiInputComponent, UiSelectComponent],
  template: `
    <app-generator-workspace
      title="Bug Refinement"
      description="Transform raw defect notes into structured developer-ready QA prompts."
      formTitle="Bug Details"
      formSubtitle="Enter defect notes below"
      filename="bug-prompt"
      [prompt]="generatedPrompt()"
      (generate)="generate()"
      (sample)="loadSample()"
      (clear)="clearForm()">

      <form [formGroup]="form" autocomplete="off" class="bug-form">
        <section class="bug-form__section" aria-labelledby="bug-issue-heading">
          <header class="bug-form__section-header">
            <span class="bug-form__section-kicker">A</span>
            <div>
              <h3 id="bug-issue-heading">Issue</h3>
              <p>Capture the reporter's raw context before refining the defect.</p>
            </div>
          </header>
          <div class="bug-form__grid">
            <ui-field class="bug-form__field--full" label="Raw Bug Notes" forId="bug-raw-notes" [hint]="rawNotesCounter()">
              <app-prompt-textarea textareaId="bug-raw-notes" formControlName="rawNotes" [rows]="5" placeholder="Paste rough notes, observations, or a short defect description"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Existing Bug Title" forId="bug-title">
              <ui-input inputId="bug-title" formControlName="title" placeholder="Short description of the issue"></ui-input>
            </ui-field>
            <ui-field label="Module / Feature" forId="bug-module">
              <ui-input inputId="bug-module" formControlName="module" placeholder="Area, screen, or capability affected"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="bug-form__section" aria-labelledby="bug-environment-heading">
          <header class="bug-form__section-header">
            <span class="bug-form__section-kicker">B</span>
            <div>
              <h3 id="bug-environment-heading">Environment</h3>
              <p>Record execution context and setup without guessing omitted details.</p>
            </div>
          </header>
          <div class="bug-form__grid">
            <ui-field label="Environment" forId="bug-environment">
              <ui-input inputId="bug-environment" formControlName="environment" placeholder="Testing, staging, browser, device, or platform"></ui-input>
            </ui-field>
            <ui-field label="Application / Build Version" forId="bug-build-version">
              <ui-input inputId="bug-build-version" formControlName="buildVersion" placeholder="Build, release, or version reference"></ui-input>
            </ui-field>
            <ui-field label="Preconditions" forId="bug-preconditions">
              <ui-input inputId="bug-preconditions" formControlName="preconditions" placeholder="Required account, state, or setup"></ui-input>
            </ui-field>
            <ui-field label="Test Data" forId="bug-test-data">
              <ui-input inputId="bug-test-data" formControlName="testData" placeholder="Non-sensitive data or input values used"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="bug-form__section" aria-labelledby="bug-reproduction-heading">
          <header class="bug-form__section-header">
            <span class="bug-form__section-kicker">C</span>
            <div>
              <h3 id="bug-reproduction-heading">Reproduction</h3>
              <p>Keep actions, expected behavior, and observed behavior distinct.</p>
            </div>
          </header>
          <div class="bug-form__grid">
            <ui-field class="bug-form__field--full" label="Steps to Reproduce" forId="bug-steps" [hint]="stepsCounter()">
              <app-prompt-textarea textareaId="bug-steps" formControlName="steps" [rows]="7" placeholder="1. Open the target screen&#10;2. Perform the action&#10;3. Observe the result"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Expected Result" forId="bug-expected" [hint]="expectedCounter()">
              <app-prompt-textarea textareaId="bug-expected" formControlName="expectedResult" [rows]="5" placeholder="What should have happened in observable terms"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Actual Result" forId="bug-actual" [hint]="actualCounter()">
              <app-prompt-textarea textareaId="bug-actual" formControlName="actualResult" [rows]="5" placeholder="What actually happened in observable terms"></app-prompt-textarea>
            </ui-field>
            <ui-field class="bug-form__field--full" label="Frequency / Reproducibility" forId="bug-frequency">
              <ui-input inputId="bug-frequency" formControlName="frequency" placeholder="Always, intermittent, once, or conditions that affect reproduction"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="bug-form__section" aria-labelledby="bug-impact-heading">
          <header class="bug-form__section-header">
            <span class="bug-form__section-kicker">D</span>
            <div>
              <h3 id="bug-impact-heading">Impact</h3>
              <p>Preserve supplied triage values and explain who or what is affected.</p>
            </div>
          </header>
          <div class="bug-form__grid">
            <ui-field label="Severity" forId="bug-severity">
              <ui-input inputId="bug-severity" formControlName="severity" placeholder="Reporter value, if known"></ui-input>
            </ui-field>
            <ui-field label="Priority" forId="bug-priority">
              <ui-input inputId="bug-priority" formControlName="priority" placeholder="Reporter value, if known"></ui-input>
            </ui-field>
            <ui-field class="bug-form__field--full" label="Business / User Impact" forId="bug-impact" [hint]="impactCounter()">
              <app-prompt-textarea textareaId="bug-impact" formControlName="impact" [rows]="4" placeholder="Describe user, operational, or business impact without adding unsupported scope"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="bug-form__section" aria-labelledby="bug-evidence-heading">
          <header class="bug-form__section-header">
            <span class="bug-form__section-kicker">E</span>
            <div>
              <h3 id="bug-evidence-heading">Evidence</h3>
              <p>Use references only; file contents are never stored in the draft.</p>
            </div>
          </header>
          <div class="bug-form__grid">
            <ui-field label="Error Message" forId="bug-error-message">
              <ui-input inputId="bug-error-message" formControlName="errorMessage" placeholder="Exact visible error or state message"></ui-input>
            </ui-field>
            <ui-field label="Related Ticket / Reference" forId="bug-related-reference">
              <ui-input inputId="bug-related-reference" formControlName="relatedReference" placeholder="Ticket, URL, filename, or reference"></ui-input>
            </ui-field>
            <ui-field class="bug-form__field--full" label="Logs / Technical Evidence" forId="bug-logs" [hint]="logsCounter()">
              <app-prompt-textarea textareaId="bug-logs" formControlName="logs" [rows]="4" placeholder="Paste log references or short technical observations, not sensitive file contents"></app-prompt-textarea>
            </ui-field>
            <ui-field class="bug-form__field--full" label="Attachments" forId="bug-attachments">
              <ui-input inputId="bug-attachments" formControlName="attachments" placeholder="Screenshot, recording, log, or filename references"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="bug-form__section" aria-labelledby="bug-output-heading">
          <header class="bug-form__section-header">
            <span class="bug-form__section-kicker">F</span>
            <div>
              <h3 id="bug-output-heading">Output Options</h3>
              <p>Choose the prompt depth, paste target, and optional report sections.</p>
            </div>
          </header>
          <div class="bug-form__grid">
            <ui-field label="Detail Level" forId="bug-detail-level">
              <ui-select selectId="bug-detail-level" formControlName="detailLevel" [options]="detailLevelOptions"></ui-select>
            </ui-field>
            <ui-field label="Target Format" forId="bug-target-format">
              <ui-select selectId="bug-target-format" formControlName="targetFormat" [options]="targetFormatOptions"></ui-select>
            </ui-field>
          </div>
          <fieldset class="bug-form__options">
            <legend>Optional output sections</legend>
            <div class="bug-form__checks">
              <label class="bug-form__check"><input type="checkbox" formControlName="includeMissingInformation"> <span>Missing Information</span></label>
              <label class="bug-form__check"><input type="checkbox" formControlName="includeAcceptanceCriteria"> <span>Fix Acceptance Criteria</span></label>
              <label class="bug-form__check"><input type="checkbox" formControlName="includeRetestChecklist"> <span>Retest Checklist</span></label>
              <label class="bug-form__check"><input type="checkbox" formControlName="includeRegressionScope"> <span>Regression Scope</span></label>
            </div>
          </fieldset>
        </section>
      </form>
    </app-generator-workspace>
  `,
  styles: [`
    .bug-form { display: flex; flex-direction: column; gap: var(--space-4); }
    .bug-form__section { display: flex; flex-direction: column; gap: var(--space-4); padding: var(--space-4); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); }
    .bug-form__section-header { display: flex; align-items: flex-start; gap: var(--space-3); }
    .bug-form__section-kicker { display: inline-flex; flex: 0 0 auto; align-items: center; justify-content: center; width: 28px; height: 28px; border: 1px solid var(--state-info-border); border-radius: var(--radius-sm); background: var(--state-info-bg); color: var(--state-info-fg); font-size: var(--text-xs); font-weight: var(--weight-heavy); }
    .bug-form__section-header h3 { margin: 0; color: var(--text-primary); font-size: var(--text-md); }
    .bug-form__section-header p { margin: 3px 0 0; color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .bug-form__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-4); }
    .bug-form__field--full { grid-column: 1 / -1; }
    .bug-form__options { margin: 0; padding: var(--space-3); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); }
    .bug-form__options legend { padding: 0 var(--space-2); color: var(--text-secondary); font-size: var(--text-sm); font-weight: var(--weight-bold); }
    .bug-form__checks { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-3); }
    .bug-form__check { display: flex; align-items: center; gap: var(--space-2); min-width: 0; color: var(--text-secondary); font-size: var(--text-sm); cursor: pointer; }
    .bug-form__check input { width: 16px; height: 16px; flex: 0 0 auto; accent-color: var(--accent); }
    @media (max-width: 760px) { .bug-form__grid, .bug-form__checks { grid-template-columns: 1fr; } .bug-form__field--full { grid-column: auto; } }
  `]
})
export class BugRefinerComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly storage = inject(PromptStorageService);
  private readonly toast = inject(ToastService);
  private readonly builder = new BugPromptBuilder();
  private readonly destroy$ = new Subject<void>();
  private readonly draftChanges$ = new Subject<BugPromptInput>();

  readonly generatedPrompt = signal('');
  readonly rawNotesCounter = signal('0 chars');
  readonly stepsCounter = signal('0 chars');
  readonly expectedCounter = signal('0 chars');
  readonly actualCounter = signal('0 chars');
  readonly impactCounter = signal('0 chars');
  readonly logsCounter = signal('0 chars');
  readonly detailLevelOptions: UiSelectOption[] = BUG_DETAIL_LEVELS.map(value => ({ value, label: value }));
  readonly targetFormatOptions: UiSelectOption[] = BUG_TARGET_FORMATS.map(value => ({ value, label: value }));

  readonly form = this.fb.nonNullable.group({
    rawNotes: [''],
    title: [''],
    module: [''],
    environment: [''],
    buildVersion: [''],
    preconditions: [''],
    testData: [''],
    steps: [''],
    expectedResult: [''],
    actualResult: [''],
    frequency: [''],
    severity: [''],
    priority: [''],
    impact: [''],
    errorMessage: [''],
    logs: [''],
    attachments: [''],
    relatedReference: [''],
    detailLevel: ['Standard' as BugPromptInput['detailLevel']],
    targetFormat: ['Generic Markdown' as BugPromptInput['targetFormat']],
    includeMissingInformation: [true],
    includeAcceptanceCriteria: [true],
    includeRetestChecklist: [false],
    includeRegressionScope: [false]
  });

  ngOnInit(): void {
    const draft = this.storage.load<BugPromptInput>('bug');
    this.form.setValue(normalizeBugPromptInput(draft));
    this.updateCounters();

    this.form.controls.detailLevel.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(detailLevel => {
      this.form.patchValue({
        includeAcceptanceCriteria: detailLevel !== 'Concise',
        includeRetestChecklist: detailLevel === 'Deep',
        includeRegressionScope: detailLevel === 'Deep'
      }, { emitEvent: false });
    });

    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.updateCounters();
      this.draftChanges$.next(this.form.getRawValue());
    });
    this.draftChanges$.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(value => this.storage.save('bug', value));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  generate(): void {
    this.generatedPrompt.set(this.builder.build(this.form.getRawValue()));
    this.toast.showSuccess('Bug prompt compiled!');
  }

  loadSample(): void {
    this.form.setValue(SAMPLE_BUG_PROMPT_INPUT);
    this.storage.save('bug', this.form.getRawValue());
    this.generatedPrompt.set('');
    this.toast.showSuccess('Sample defect data loaded');
  }

  clearForm(): void {
    this.form.setValue(EMPTY_BUG_PROMPT_INPUT);
    this.storage.clear('bug');
    this.generatedPrompt.set('');
    this.toast.showSuccess('Bug form cleared');
  }

  private updateCounters(): void {
    this.rawNotesCounter.set(`${this.form.controls.rawNotes.value.length} chars`);
    this.stepsCounter.set(`${this.form.controls.steps.value.length} chars`);
    this.expectedCounter.set(`${this.form.controls.expectedResult.value.length} chars`);
    this.actualCounter.set(`${this.form.controls.actualResult.value.length} chars`);
    this.impactCounter.set(`${this.form.controls.impact.value.length} chars`);
    this.logsCounter.set(`${this.form.controls.logs.value.length} chars`);
  }
}
