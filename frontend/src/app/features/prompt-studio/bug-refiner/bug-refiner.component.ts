import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent } from '../../../shared/ui';
import { BugPromptBuilder } from '../builders/bug-prompt.builder';
import { BugPromptInput, EMPTY_BUG_PROMPT_INPUT, SAMPLE_BUG_PROMPT_INPUT } from '../models/bug-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { GeneratorWorkspaceComponent } from '../components/generator-workspace/generator-workspace.component';
import { PromptTextareaComponent } from '../components/prompt-textarea/prompt-textarea.component';

@Component({
  selector: 'app-bug-refiner',
  standalone: true,
  imports: [ReactiveFormsModule, GeneratorWorkspaceComponent, PromptTextareaComponent, UiFieldComponent, UiInputComponent],
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
        <ui-field label="Bug Title" forId="bug-title">
          <ui-input inputId="bug-title" formControlName="title" placeholder="Short description (e.g., Checkout total mismatch)"></ui-input>
        </ui-field>

        <ui-field label="Preconditions" forId="bug-preconditions">
          <ui-input inputId="bug-preconditions" formControlName="preconditions" placeholder="Environment, version, or user state"></ui-input>
        </ui-field>

        <ui-field label="Steps to Reproduce" forId="bug-steps" [hint]="stepsCounter()">
          <app-prompt-textarea textareaId="bug-steps" formControlName="steps" [rows]="7" placeholder="1. Open the target screen&#10;2. Perform the action&#10;3. Observe the result"></app-prompt-textarea>
        </ui-field>

        <div class="bug-form__row">
          <ui-field label="Expected Result" forId="bug-expected">
            <app-prompt-textarea textareaId="bug-expected" formControlName="expectedResult" [rows]="6" placeholder="What should have happened..."></app-prompt-textarea>
          </ui-field>
          <ui-field label="Actual Result" forId="bug-actual">
            <app-prompt-textarea textareaId="bug-actual" formControlName="actualResult" [rows]="6" placeholder="What actually happened..."></app-prompt-textarea>
          </ui-field>
        </div>

        <ui-field label="Attachments" forId="bug-attachments">
          <ui-input inputId="bug-attachments" formControlName="attachments" placeholder="Screenshots, recordings, logs, or links"></ui-input>
        </ui-field>
      </form>
    </app-generator-workspace>
  `,
  styles: [`
    .bug-form { display: flex; flex-direction: column; gap: var(--space-4); }
    .bug-form__row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-4); }
    @media (max-width: 760px) { .bug-form__row { grid-template-columns: 1fr; } }
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
  readonly stepsCounter = signal('0 chars');

  readonly form = this.fb.nonNullable.group({
    title: [''],
    preconditions: [''],
    steps: [''],
    expectedResult: [''],
    actualResult: [''],
    attachments: ['']
  });

  ngOnInit(): void {
    const draft = this.storage.load<BugPromptInput>('bug');
    if (draft) this.form.patchValue({ ...EMPTY_BUG_PROMPT_INPUT, ...draft });
    this.updateCounter();

    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.updateCounter();
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

  private updateCounter(): void {
    this.stepsCounter.set(`${this.form.controls.steps.value.length} chars`);
  }
}
