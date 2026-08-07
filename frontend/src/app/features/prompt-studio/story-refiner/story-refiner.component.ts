import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent } from '../../../shared/ui';
import { StoryPromptBuilder } from '../builders/story-prompt.builder';
import { EMPTY_STORY_PROMPT_INPUT, SAMPLE_STORY_PROMPT_INPUT, StoryPromptInput } from '../models/story-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { GeneratorWorkspaceComponent } from '../components/generator-workspace/generator-workspace.component';
import { PromptTextareaComponent } from '../components/prompt-textarea/prompt-textarea.component';

@Component({
  selector: 'app-story-refiner',
  standalone: true,
  imports: [ReactiveFormsModule, GeneratorWorkspaceComponent, PromptTextareaComponent, UiFieldComponent, UiInputComponent],
  template: `
    <app-generator-workspace
      title="Story Refinement"
      description="Transform rough business requests into structured implementation-ready stories."
      formTitle="Story Details"
      formSubtitle="Capture the request and its business intent"
      filename="story-prompt"
      [prompt]="generatedPrompt()"
      (generate)="generate()"
      (sample)="loadSample()"
      (clear)="clearForm()">

      <form [formGroup]="form" autocomplete="off" class="story-form">
        <ui-field label="Raw Story / Request" forId="story-raw" [hint]="rawStoryCounter()">
          <app-prompt-textarea textareaId="story-raw" formControlName="rawStory" [rows]="6" placeholder="Paste the rough request, notes, or stakeholder wording..."></app-prompt-textarea>
        </ui-field>

        <ui-field label="Proposed Title" forId="story-title">
          <ui-input inputId="story-title" formControlName="title" placeholder="Short implementation title"></ui-input>
        </ui-field>

        <div class="story-form__row">
          <ui-field label="Actor / Persona" forId="story-actor">
            <ui-input inputId="story-actor" formControlName="actor" placeholder="Who needs this capability?"></ui-input>
          </ui-field>
          <ui-field label="Business Goal" forId="story-goal">
            <ui-input inputId="story-goal" formControlName="businessGoal" placeholder="What outcome should this achieve?"></ui-input>
          </ui-field>
        </div>

        <ui-field label="Desired Behavior" forId="story-desired">
          <app-prompt-textarea textareaId="story-desired" formControlName="desiredBehavior" [rows]="5" placeholder="Describe the desired user-visible behavior..."></app-prompt-textarea>
        </ui-field>
      </form>
    </app-generator-workspace>
  `,
  styles: [`
    .story-form { display: flex; flex-direction: column; gap: var(--space-4); }
    .story-form__row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-4); }
    @media (max-width: 760px) { .story-form__row { grid-template-columns: 1fr; } }
  `]
})
export class StoryRefinerComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly storage = inject(PromptStorageService);
  private readonly toast = inject(ToastService);
  private readonly builder = new StoryPromptBuilder();
  private readonly destroy$ = new Subject<void>();
  private readonly draftChanges$ = new Subject<StoryPromptInput>();

  readonly generatedPrompt = signal('');
  readonly rawStoryCounter = signal('0 chars');

  readonly form = this.fb.nonNullable.group({
    rawStory: [''],
    title: [''],
    actor: [''],
    businessGoal: [''],
    desiredBehavior: ['']
  });

  ngOnInit(): void {
    const draft = this.storage.load<StoryPromptInput>('story');
    if (draft) this.form.patchValue({ ...EMPTY_STORY_PROMPT_INPUT, ...draft });
    this.updateCounter();

    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.updateCounter();
      this.draftChanges$.next(this.form.getRawValue());
    });
    this.draftChanges$.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(value => this.storage.save('story', value));
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
    this.form.setValue(SAMPLE_STORY_PROMPT_INPUT);
    this.storage.save('story', this.form.getRawValue());
    this.generatedPrompt.set('');
    this.toast.showSuccess('Sample story data loaded');
  }

  clearForm(): void {
    this.form.setValue(EMPTY_STORY_PROMPT_INPUT);
    this.storage.clear('story');
    this.generatedPrompt.set('');
    this.toast.showSuccess('Story form cleared');
  }

  private updateCounter(): void {
    this.rawStoryCounter.set(`${this.form.controls.rawStory.value.length} chars`);
  }
}
