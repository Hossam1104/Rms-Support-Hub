import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';
import { TestCasePromptBuilder } from '../builders/test-case-prompt.builder';
import {
  DEFAULT_TEST_CASE_EXPECTED_RESULT_MODE,
  DEFAULT_TEST_CASE_OUTPUT_TYPE,
  DEFAULT_TEST_CASE_SCENARIO_TYPE,
  EMPTY_TEST_CASE_PROMPT_INPUT,
  SAMPLE_TEST_CASE_PROMPT_INPUT,
  TEST_CASE_EXPECTED_RESULT_MODES,
  TEST_CASE_OUTPUT_TYPES,
  TEST_CASE_PRIORITIES,
  TEST_CASE_SCENARIO_TYPES,
  TestCasePromptInput,
  normalizeTestCasePromptInput
} from '../models/test-case-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { GeneratorWorkspaceComponent } from '../components/generator-workspace/generator-workspace.component';
import { PromptTextareaComponent } from '../components/prompt-textarea/prompt-textarea.component';

@Component({
  selector: 'app-test-case-generator',
  standalone: true,
  imports: [ReactiveFormsModule, GeneratorWorkspaceComponent, PromptTextareaComponent, UiFieldComponent, UiInputComponent, UiSelectComponent],
  template: `
    <app-generator-workspace
      title="Test Case Generation"
      description="Create structured manual QA test-case prompts from requirements and evidence."
      formTitle="Test Case Specifications"
      formSubtitle="Complete fields or leave blank to infer from screenshots"
      filename="test-case-prompt"
      previewSubtitle="Copy and run this prompt with your screenshots in an approved AI workflow."
      [prompt]="generatedPrompt()"
      (generate)="generate()"
      (sample)="loadSample()"
      (clear)="clearForm()">

      <form [formGroup]="form" autocomplete="off" class="test-case-form">
        <section class="test-case-form__section" aria-labelledby="test-case-metadata-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">A</span>
            <div>
              <h3 id="test-case-metadata-heading">Test Case Metadata</h3>
              <p>Capture the requirement, scenario, target area, and execution context.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field label="Test Case ID" forId="tc-id">
              <ui-input inputId="tc-id" formControlName="testCaseId" placeholder="e.g. TC-HP-001 (or blank)"></ui-input>
            </ui-field>
            <ui-field label="Requirement / Story Reference" forId="tc-reference">
              <ui-input inputId="tc-reference" formControlName="requirementReference" placeholder="Ticket, story, or requirement reference"></ui-input>
            </ui-field>
            <ui-field label="Scenario Category" forId="tc-scenario-type">
              <ui-select selectId="tc-scenario-type" formControlName="scenarioType" [options]="scenarioOptions"></ui-select>
            </ui-field>
            <ui-field label="Test Case Name" forId="tc-name">
              <ui-input inputId="tc-name" formControlName="name" placeholder="e.g. Verify request submission"></ui-input>
            </ui-field>
            <ui-field label="Module / Target Section" forId="tc-target">
              <ui-input inputId="tc-target" formControlName="targetSection" placeholder="e.g. Checkout or Payments"></ui-input>
            </ui-field>
            <ui-field label="Environment" forId="tc-environment">
              <ui-input inputId="tc-environment" formControlName="environment" placeholder="Testing, browser, device, or platform"></ui-input>
            </ui-field>
            <ui-field label="Priority" forId="tc-priority">
              <ui-select selectId="tc-priority" formControlName="priority" [options]="priorityOptions" placeholder="Select priority"></ui-select>
            </ui-field>
          </div>
        </section>

        <section class="test-case-form__section" aria-labelledby="test-case-setup-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">B</span>
            <div>
              <h3 id="test-case-setup-heading">Setup and Data</h3>
              <p>Record only the known setup and test values needed to execute the scenario.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Preconditions" forId="tc-preconditions" [hint]="preconditionsCounter()">
              <app-prompt-textarea textareaId="tc-preconditions" formControlName="preconditions" [rows]="4" placeholder="Account, state, permissions, or environment setup"></app-prompt-textarea>
            </ui-field>
            <ui-field class="test-case-form__field--full" label="Test Data" forId="tc-test-data" [hint]="testDataCounter()">
              <app-prompt-textarea textareaId="tc-test-data" formControlName="testData" [rows]="4" placeholder="Non-sensitive values, records, files, or inputs required"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="test-case-form__section" aria-labelledby="test-case-execution-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">C</span>
            <div>
              <h3 id="test-case-execution-heading">Execution and Results</h3>
              <p>Use atomic actions and observable outcomes; expected results can be paired per step or kept final.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Steps to Execute" forId="tc-steps" [hint]="stepsCounter()">
              <app-prompt-textarea textareaId="tc-steps" formControlName="steps" [rows]="7" placeholder="1. Navigate to the target screen&#10;2. Perform one action&#10;3. Observe the resulting state"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Expected Result Mode" forId="tc-expected-mode">
              <ui-select selectId="tc-expected-mode" formControlName="expectedResultMode" [options]="expectedResultModeOptions"></ui-select>
            </ui-field>
            <ui-field label="Expected Result" forId="tc-expected" [hint]="expectedCounter()">
              <app-prompt-textarea textareaId="tc-expected" formControlName="expectedResult" [rows]="5" placeholder="State the visible, measurable, or recorded outcome"></app-prompt-textarea>
            </ui-field>
            <ui-field class="test-case-form__field--full" label="Postconditions" forId="tc-postconditions" [hint]="postconditionsCounter()">
              <app-prompt-textarea textareaId="tc-postconditions" formControlName="postconditions" [rows]="4" placeholder="State left after the test completes"></app-prompt-textarea>
            </ui-field>
            <ui-field class="test-case-form__field--full" label="Cleanup" forId="tc-cleanup" [hint]="cleanupCounter()">
              <app-prompt-textarea textareaId="tc-cleanup" formControlName="cleanup" [rows]="4" placeholder="Approved cleanup or reset actions, if known"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="test-case-form__section" aria-labelledby="test-case-governance-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">D</span>
            <div>
              <h3 id="test-case-governance-heading">Evidence and Coverage</h3>
              <p>Keep evidence as references and make automation and regression intent explicit.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Attachments / Evidence References" forId="tc-attachments">
              <ui-input inputId="tc-attachments" formControlName="attachments" placeholder="Screenshot, recording, log, or filename references"></ui-input>
            </ui-field>
            <ui-field label="Automation Candidacy" forId="tc-automation">
              <ui-input inputId="tc-automation" formControlName="automationCandidacy" placeholder="Candidate, manual-only, or assess"></ui-input>
            </ui-field>
            <ui-field label="Regression Tag" forId="tc-regression">
              <ui-input inputId="tc-regression" formControlName="regressionTag" placeholder="Suite, area, or risk tag"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="test-case-form__section" aria-labelledby="test-case-output-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">E</span>
            <div>
              <h3 id="test-case-output-heading">Output Options</h3>
              <p>Choose the structure for the generated test-case prompt.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Output Type" forId="tc-output-type">
              <ui-select selectId="tc-output-type" formControlName="outputType" [options]="outputTypeOptions"></ui-select>
            </ui-field>
          </div>
        </section>
      </form>
    </app-generator-workspace>
  `,
  styles: [`
    .test-case-form { display: flex; flex-direction: column; gap: var(--space-4); }
    .test-case-form__section { display: flex; flex-direction: column; gap: var(--space-4); padding: var(--space-4); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); }
    .test-case-form__section-header { display: flex; align-items: flex-start; gap: var(--space-3); }
    .test-case-form__section-kicker { display: inline-flex; flex: 0 0 auto; align-items: center; justify-content: center; width: 28px; height: 28px; border: 1px solid var(--state-info-border); border-radius: var(--radius-sm); background: var(--state-info-bg); color: var(--state-info-fg); font-size: var(--text-xs); font-weight: var(--weight-heavy); }
    .test-case-form__section-header h3 { margin: 0; color: var(--text-primary); font-size: var(--text-md); }
    .test-case-form__section-header p { margin: 3px 0 0; color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .test-case-form__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-4); }
    .test-case-form__field--full { grid-column: 1 / -1; }
    @media (max-width: 760px) { .test-case-form__grid { grid-template-columns: 1fr; } .test-case-form__field--full { grid-column: auto; } }
  `]
})
export class TestCaseGeneratorComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly storage = inject(PromptStorageService);
  private readonly toast = inject(ToastService);
  private readonly builder = new TestCasePromptBuilder();
  private readonly destroy$ = new Subject<void>();
  private readonly draftChanges$ = new Subject<{ value: TestCasePromptInput; generation: number }>();
  private draftGeneration = 0;

  readonly generatedPrompt = signal('');
  readonly preconditionsCounter = signal('0 chars');
  readonly testDataCounter = signal('0 chars');
  readonly stepsCounter = signal('0 chars');
  readonly expectedCounter = signal('0 chars');
  readonly postconditionsCounter = signal('0 chars');
  readonly cleanupCounter = signal('0 chars');
  readonly scenarioOptions: UiSelectOption[] = TEST_CASE_SCENARIO_TYPES.map(value => ({ value, label: value }));
  readonly priorityOptions: UiSelectOption[] = TEST_CASE_PRIORITIES.map(value => ({ value, label: value }));
  readonly expectedResultModeOptions: UiSelectOption[] = TEST_CASE_EXPECTED_RESULT_MODES.map(value => ({ value, label: value }));
  readonly outputTypeOptions: UiSelectOption[] = TEST_CASE_OUTPUT_TYPES.map(value => ({ value, label: value }));

  readonly form = this.fb.nonNullable.group({
    testCaseId: [''],
    requirementReference: [''],
    scenarioType: [DEFAULT_TEST_CASE_SCENARIO_TYPE as TestCasePromptInput['scenarioType']],
    name: [''],
    targetSection: [''],
    environment: [''],
    priority: [''],
    preconditions: [''],
    testData: [''],
    steps: [''],
    expectedResult: [''],
    expectedResultMode: [DEFAULT_TEST_CASE_EXPECTED_RESULT_MODE as TestCasePromptInput['expectedResultMode']],
    postconditions: [''],
    cleanup: [''],
    attachments: [''],
    automationCandidacy: [''],
    regressionTag: [''],
    outputType: [DEFAULT_TEST_CASE_OUTPUT_TYPE as TestCasePromptInput['outputType']]
  });

  ngOnInit(): void {
    const draft = this.storage.load<TestCasePromptInput>('testCase');
    this.form.setValue(draft ? normalizeTestCasePromptInput(draft) : EMPTY_TEST_CASE_PROMPT_INPUT);
    this.updateCounters();

    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.updateCounters();
      this.draftChanges$.next({ value: this.form.getRawValue() as TestCasePromptInput, generation: this.draftGeneration });
    });
    this.draftChanges$.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(change => {
      if (change.generation === this.draftGeneration) this.storage.save('testCase', change.value);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  generate(): void {
    this.generatedPrompt.set(this.builder.build(this.form.getRawValue() as TestCasePromptInput));
    this.toast.showSuccess('Test case prompt compiled!');
  }

  loadSample(): void {
    this.draftGeneration += 1;
    this.form.setValue(SAMPLE_TEST_CASE_PROMPT_INPUT);
    this.storage.save('testCase', this.form.getRawValue() as TestCasePromptInput);
    this.generatedPrompt.set('');
    this.toast.showSuccess('Sample test case loaded');
  }

  clearForm(): void {
    this.draftGeneration += 1;
    this.form.setValue(EMPTY_TEST_CASE_PROMPT_INPUT, { emitEvent: false });
    this.updateCounters();
    this.storage.clear('testCase');
    this.generatedPrompt.set('');
    this.toast.showSuccess('Test case form cleared');
  }

  private updateCounters(): void {
    this.preconditionsCounter.set(`${this.form.controls.preconditions.value.length} chars`);
    this.testDataCounter.set(`${this.form.controls.testData.value.length} chars`);
    this.stepsCounter.set(`${this.form.controls.steps.value.length} chars`);
    this.expectedCounter.set(`${this.form.controls.expectedResult.value.length} chars`);
    this.postconditionsCounter.set(`${this.form.controls.postconditions.value.length} chars`);
    this.cleanupCounter.set(`${this.form.controls.cleanup.value.length} chars`);
  }
}
