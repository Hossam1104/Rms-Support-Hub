import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent, UiSelectComponent, UiSelectOption } from '../../../shared/ui';
import { TestCasePromptBuilder } from '../builders/test-case-prompt.builder';
import {
  EMPTY_TEST_CASE_PROMPT_INPUT,
  SAMPLE_TEST_CASE_PROMPT_INPUT,
  TEST_CASE_PRIORITIES,
  TEST_CASE_SCENARIO_TYPES,
  TestCasePromptInput
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
        <div class="test-case-form__row">
          <ui-field label="Test Case ID" forId="tc-id">
            <ui-input inputId="tc-id" formControlName="testCaseId" placeholder="e.g. TC-HP-001 (or blank)"></ui-input>
          </ui-field>
          <ui-field label="Scenario Type" forId="tc-scenario-type">
            <ui-select selectId="tc-scenario-type" formControlName="scenarioType" [options]="scenarioOptions" placeholder="Let AI recommend/infer"></ui-select>
          </ui-field>
        </div>

        <div class="test-case-form__row">
          <ui-field label="Test Case Name" forId="tc-name">
            <ui-input inputId="tc-name" formControlName="name" placeholder="e.g. Verify request submission"></ui-input>
          </ui-field>
          <ui-field label="Targeted Table / Section" forId="tc-target">
            <ui-input inputId="tc-target" formControlName="targetSection" placeholder="e.g. Checkout sheet"></ui-input>
          </ui-field>
        </div>

        <div class="test-case-form__row">
          <ui-field label="Priority" forId="tc-priority">
            <ui-select selectId="tc-priority" formControlName="priority" [options]="priorityOptions" placeholder="Let AI recommend/infer"></ui-select>
          </ui-field>
          <ui-field label="Preconditions" forId="tc-preconditions">
            <ui-input inputId="tc-preconditions" formControlName="preconditions" placeholder="App state or environment setup"></ui-input>
          </ui-field>
        </div>

        <ui-field label="Steps to Execute" forId="tc-steps" [hint]="stepsCounter()">
          <app-prompt-textarea textareaId="tc-steps" formControlName="steps" [rows]="6" placeholder="1. Navigate to screen...&#10;2. Input details..."></app-prompt-textarea>
        </ui-field>

        <ui-field label="Expected Result" forId="tc-expected" [hint]="expectedCounter()">
          <app-prompt-textarea textareaId="tc-expected" formControlName="expectedResult" [rows]="5" placeholder="Define success states or observable results..."></app-prompt-textarea>
        </ui-field>

        <ui-field label="Screenshot References" forId="tc-attachments">
          <ui-input inputId="tc-attachments" formControlName="attachments" placeholder="e.g. dashboard_state.png, error_state.png"></ui-input>
        </ui-field>
      </form>
    </app-generator-workspace>
  `,
  styles: [`
    .test-case-form { display: flex; flex-direction: column; gap: var(--space-4); }
    .test-case-form__row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-4); }
    @media (max-width: 760px) { .test-case-form__row { grid-template-columns: 1fr; } }
  `]
})
export class TestCaseGeneratorComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly storage = inject(PromptStorageService);
  private readonly toast = inject(ToastService);
  private readonly builder = new TestCasePromptBuilder();
  private readonly destroy$ = new Subject<void>();
  private readonly draftChanges$ = new Subject<TestCasePromptInput>();

  readonly generatedPrompt = signal('');
  readonly stepsCounter = signal('0 chars');
  readonly expectedCounter = signal('0 chars');
  readonly scenarioOptions: UiSelectOption[] = TEST_CASE_SCENARIO_TYPES.map(value => ({ value, label: value }));
  readonly priorityOptions: UiSelectOption[] = TEST_CASE_PRIORITIES.map(value => ({ value, label: value }));

  readonly form = this.fb.nonNullable.group({
    testCaseId: [''],
    scenarioType: [''],
    name: [''],
    targetSection: [''],
    priority: [''],
    preconditions: [''],
    steps: [''],
    expectedResult: [''],
    attachments: ['']
  });

  ngOnInit(): void {
    const draft = this.storage.load<TestCasePromptInput>('testCase');
    if (draft) this.form.patchValue({ ...EMPTY_TEST_CASE_PROMPT_INPUT, ...draft });
    this.updateCounters();

    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.updateCounters();
      this.draftChanges$.next(this.form.getRawValue());
    });
    this.draftChanges$.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(value => this.storage.save('testCase', value));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  generate(): void {
    this.generatedPrompt.set(this.builder.build(this.form.getRawValue()));
    this.toast.showSuccess('Test case prompt compiled!');
  }

  loadSample(): void {
    this.form.setValue(SAMPLE_TEST_CASE_PROMPT_INPUT);
    this.storage.save('testCase', this.form.getRawValue());
    this.generatedPrompt.set('');
    this.toast.showSuccess('Sample test case loaded');
  }

  clearForm(): void {
    this.form.setValue(EMPTY_TEST_CASE_PROMPT_INPUT);
    this.storage.clear('testCase');
    this.generatedPrompt.set('');
    this.toast.showSuccess('Test case form cleared');
  }

  private updateCounters(): void {
    this.stepsCounter.set(`${this.form.controls.steps.value.length} chars`);
    this.expectedCounter.set(`${this.form.controls.expectedResult.value.length} chars`);
  }
}
