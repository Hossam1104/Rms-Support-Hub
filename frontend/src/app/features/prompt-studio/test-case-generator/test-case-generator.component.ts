import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent } from '../../../shared/ui';
import { TestCasePromptBuilder } from '../builders/test-case-prompt.builder';
import {
    EMPTY_TEST_CASE_PROMPT_INPUT,
    SAMPLE_TEST_CASE_PROMPT_INPUT,
    TestCasePromptInput,
    normalizeTestCasePromptInput
} from '../models/test-case-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { PromptHistoryService } from '../services/prompt-history.service';
import { PromptQualityResult, PromptQualityService } from '../services/prompt-quality.service';
import { GeneratorWorkspaceComponent } from '../components/generator-workspace/generator-workspace.component';
import { PromptTextareaComponent } from '../components/prompt-textarea/prompt-textarea.component';

@Component({
    selector: 'app-test-case-generator',
    standalone: true,
    imports: [ReactiveFormsModule, GeneratorWorkspaceComponent, PromptTextareaComponent, UiFieldComponent, UiInputComponent],
    template: `
    <app-generator-workspace
      title="Test Case Generation"
      description="Create structured manual QA test-case prompts from requirements and evidence."
      formTitle="Test Case Details"
      formSubtitle="Capture only the information needed to execute the scenario"
      filename="test-case-prompt"
      previewSubtitle="Copy and run this prompt with your screenshots in an approved AI workflow."
      [prompt]="generatedPrompt()"
      (generate)="generate()"
      (sample)="loadSample()"
      (clear)="clearForm()"
      [quality]="qualityResult()"
      (historyOpen)="openHistory($event)">

      <form [formGroup]="form" autocomplete="off" class="test-case-form">
        <section class="test-case-form__section" aria-labelledby="test-case-source-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">A</span>
            <div>
              <h3 id="test-case-source-heading">Source Information</h3>
              <p>Identify the scenario, target feature, and requirement reference.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Requirement / Story Reference" forId="tc-reference">
              <ui-input inputId="tc-reference" formControlName="requirementReference" placeholder="Ticket, story, or requirement reference (optional)"></ui-input>
            </ui-field>
            <ui-field label="Test Case Title" forId="tc-title">
              <ui-input inputId="tc-title" formControlName="title" placeholder="Action or condition plus expected behavior"></ui-input>
            </ui-field>
            <ui-field label="Module / Feature" forId="tc-module">
              <ui-input inputId="tc-module" formControlName="module" placeholder="Area, screen, or capability under test"></ui-input>
            </ui-field>
            <ui-field class="test-case-form__field--full" label="Scenario / Objective" forId="tc-scenario">
              <app-prompt-textarea textareaId="tc-scenario" formControlName="scenario" [rows]="4" placeholder="What scenario or objective should this test verify?"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="test-case-form__section" aria-labelledby="test-case-setup-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">B</span>
            <div>
              <h3 id="test-case-setup-heading">Setup and Data</h3>
              <p>Record the known setup and non-sensitive values required to run the test.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Preconditions" forId="tc-preconditions" [hint]="preconditionsCounter()">
              <app-prompt-textarea textareaId="tc-preconditions" formControlName="preconditions" [rows]="4" placeholder="Account, state, permissions, or setup"></app-prompt-textarea>
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
              <p>Use atomic actions and an observable expected outcome.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field class="test-case-form__field--full" label="Steps" forId="tc-steps" [hint]="stepsCounter()">
              <app-prompt-textarea textareaId="tc-steps" formControlName="steps" [rows]="8" placeholder="1. Navigate to the target screen&#10;2. Perform one action&#10;3. Observe the resulting state"></app-prompt-textarea>
            </ui-field>
            <ui-field class="test-case-form__field--full" label="Expected Result" forId="tc-expected" [hint]="expectedCounter()">
              <app-prompt-textarea textareaId="tc-expected" formControlName="expectedResult" [rows]="5" placeholder="State the visible, measurable, or recorded outcome"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="test-case-form__section" aria-labelledby="test-case-triage-heading">
          <header class="test-case-form__section-header">
            <span class="test-case-form__section-kicker">D</span>
            <div>
              <h3 id="test-case-triage-heading">Priority and Evidence</h3>
              <p>Keep priority simple and preserve evidence as references only.</p>
            </div>
          </header>
          <div class="test-case-form__grid">
            <ui-field label="Priority" forId="tc-priority">
              <ui-input inputId="tc-priority" formControlName="priority" placeholder="P0, P1, P2, or P3"></ui-input>
            </ui-field>
            <ui-field label="Evidence / Attachments" forId="tc-attachments">
              <ui-input inputId="tc-attachments" formControlName="attachments" placeholder="Screenshot, recording, log, or filename references"></ui-input>
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
    private readonly history = inject(PromptHistoryService);
    private readonly quality = inject(PromptQualityService);
    private readonly builder = new TestCasePromptBuilder();
    private readonly destroy$ = new Subject<void>();
    private readonly draftChanges$ = new Subject<{ value: TestCasePromptInput; generation: number }>();
    private draftGeneration = 0;

    readonly generatedPrompt = signal('');
    readonly qualityResult = signal<PromptQualityResult | null>(null);
    readonly preconditionsCounter = signal('0 chars');
    readonly testDataCounter = signal('0 chars');
    readonly stepsCounter = signal('0 chars');
    readonly expectedCounter = signal('0 chars');

    readonly form = this.fb.nonNullable.group({
        requirementReference: [''],
        title: [''],
        module: [''],
        scenario: [''],
        preconditions: [''],
        testData: [''],
        steps: [''],
        expectedResult: [''],
        priority: [''],
        attachments: ['']
    });

    ngOnInit(): void {
        const draft = this.storage.load<TestCasePromptInput>('testCase');
        this.form.setValue(normalizeTestCasePromptInput(draft));
        this.updateCounters();
        this.updateQuality();
        this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
            this.updateCounters();
            this.updateQuality();
            this.draftChanges$.next({ value: this.form.getRawValue(), generation: this.draftGeneration });
        });
        this.draftChanges$.pipe(debounceTime(300), takeUntil(this.destroy$)).subscribe(change => {
            if (change.generation === this.draftGeneration) this.storage.save('testCase', change.value);
        });
    }

    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    generate(): void {
        const input = this.form.getRawValue();
        const prompt = this.builder.build(input);
        this.generatedPrompt.set(prompt);
        this.history.add('Test Case', input.title, prompt);
        this.toast.showSuccess('Test case prompt compiled!');
    }

    openHistory(prompt: string): void {
        this.generatedPrompt.set(prompt);
    }

    loadSample(): void {
        this.draftGeneration += 1;
        this.form.setValue(SAMPLE_TEST_CASE_PROMPT_INPUT);
        this.storage.save('testCase', this.form.getRawValue());
        this.generatedPrompt.set('');
        this.toast.showSuccess('Sample test case loaded');
    }

    clearForm(): void {
        this.draftGeneration += 1;
        this.form.setValue(EMPTY_TEST_CASE_PROMPT_INPUT, { emitEvent: false });
        this.updateCounters();
        this.storage.clear('testCase');
        this.generatedPrompt.set('');
        this.updateQuality();
        this.toast.showSuccess('Test case form cleared');
    }

    private updateCounters(): void {
        this.preconditionsCounter.set(`${this.form.controls.preconditions.value.length} chars`);
        this.testDataCounter.set(`${this.form.controls.testData.value.length} chars`);
        this.stepsCounter.set(`${this.form.controls.steps.value.length} chars`);
        this.expectedCounter.set(`${this.form.controls.expectedResult.value.length} chars`);
    }

    private updateQuality(): void {
        this.qualityResult.set(this.quality.analyze('testCase', this.form.getRawValue()));
    }
}
