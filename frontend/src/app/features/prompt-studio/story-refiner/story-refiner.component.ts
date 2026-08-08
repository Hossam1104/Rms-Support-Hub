import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, takeUntil } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { UiFieldComponent, UiInputComponent } from '../../../shared/ui';
import { StoryPromptBuilder } from '../builders/story-prompt.builder';
import {
    EMPTY_STORY_PROMPT_INPUT,
    SAMPLE_STORY_PROMPT_INPUT,
    StoryPromptInput,
    normalizeStoryPromptInput
} from '../models/story-prompt.model';
import { PromptStorageService } from '../services/prompt-storage.service';
import { PromptHistoryService } from '../services/prompt-history.service';
import { PromptQualityResult, PromptQualityService } from '../services/prompt-quality.service';
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
      formSubtitle="Capture the request, outcome, rules, and references"
      filename="story-prompt"
      [prompt]="generatedPrompt()"
      (generate)="generate()"
      (sample)="loadSample()"
      (clear)="clearForm()"
      [quality]="qualityResult()"
      (historyOpen)="openHistory($event)">

      <form [formGroup]="form" autocomplete="off" class="story-form">
        <section class="story-form__section" aria-labelledby="story-request-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">A</span>
            <div>
              <h3 id="story-request-heading">Source Information</h3>
              <p>Start with the raw request and the people and outcome it names.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field #rawStoryField class="story-form__field--full" label="Raw Story / Requirement" forId="story-raw" [hint]="rawStoryCounter()">
              <app-prompt-textarea textareaId="story-raw" formControlName="rawStory" [rows]="6" placeholder="Paste the rough request, notes, or stakeholder wording" [ariaDescribedBy]="rawStoryField.describedBy()"></app-prompt-textarea>
            </ui-field>
            <ui-field label="Story Title" forId="story-title">
              <ui-input inputId="story-title" formControlName="title" placeholder="Capability-focused title"></ui-input>
            </ui-field>
            <ui-field label="User / Role" forId="story-actor">
              <ui-input inputId="story-actor" formControlName="actor" placeholder="Who needs this capability?"></ui-input>
            </ui-field>
            <ui-field class="story-form__field--full" label="Business Goal" forId="story-goal">
              <ui-input inputId="story-goal" formControlName="businessGoal" placeholder="What outcome should this achieve?"></ui-input>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-requirement-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">B</span>
            <div>
              <h3 id="story-requirement-heading">Desired Behavior</h3>
              <p>Describe the requested behavior in observable terms without adding scope.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field #requirementField class="story-form__field--full" label="Requirement / Description" forId="story-requirement" [hint]="requirementCounter()">
              <app-prompt-textarea textareaId="story-requirement" formControlName="requirement" [rows]="8" placeholder="What should the product or workflow do?" [ariaDescribedBy]="requirementField.describedBy()"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>

        <section class="story-form__section" aria-labelledby="story-rules-heading">
          <header class="story-form__section-header">
            <span class="story-form__section-kicker">C</span>
            <div>
              <h3 id="story-rules-heading">Rules and Evidence</h3>
              <p>Keep confirmed rules and references close to the request.</p>
            </div>
          </header>
          <div class="story-form__grid">
            <ui-field #businessRulesField class="story-form__field--full" label="Business Rules" forId="story-rules" [hint]="businessRulesCounter()">
              <app-prompt-textarea textareaId="story-rules" formControlName="businessRules" [rows]="6" placeholder="List confirmed rules; leave unknown rules for clarification" [ariaDescribedBy]="businessRulesField.describedBy()"></app-prompt-textarea>
            </ui-field>
            <ui-field class="story-form__field--full" label="Evidence / References" forId="story-evidence">
              <app-prompt-textarea textareaId="story-evidence" formControlName="evidenceReferences" [rows]="4" placeholder="Screens, links, filenames, or reference notes"></app-prompt-textarea>
            </ui-field>
          </div>
        </section>
      </form>
    </app-generator-workspace>
  `,
    styles: [`
    .story-form { display: flex; flex-direction: column; gap: var(--panel-gap); }
    .story-form__section { display: flex; flex-direction: column; gap: var(--panel-gap); padding: var(--panel-padding-compact); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-raised); }
    .story-form__section-header { display: flex; align-items: flex-start; gap: var(--space-3); }
    .story-form__section-kicker { display: inline-flex; flex: 0 0 auto; align-items: center; justify-content: center; width: 28px; height: 28px; border: 1px solid var(--state-info-border); border-radius: var(--radius-sm); background: var(--state-info-bg); color: var(--state-info-fg); font-size: var(--text-xs); font-weight: var(--weight-heavy); }
    .story-form__section-header h3 { margin: 0; color: var(--text-primary); font-size: var(--text-md); }
    .story-form__section-header p { margin: 3px 0 0; color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .story-form__grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--form-gap); }
    .story-form__field--full { grid-column: 1 / -1; }
    @media (max-width: 760px) { .story-form__grid { grid-template-columns: 1fr; } .story-form__field--full { grid-column: auto; } }
  `]
})
export class StoryRefinerComponent implements OnInit, OnDestroy {
    private readonly fb = inject(FormBuilder);
    private readonly storage = inject(PromptStorageService);
    private readonly toast = inject(ToastService);
    private readonly history = inject(PromptHistoryService);
    private readonly quality = inject(PromptQualityService);
    private readonly builder = new StoryPromptBuilder();
    private readonly destroy$ = new Subject<void>();
    private readonly draftChanges$ = new Subject<{ value: StoryPromptInput; generation: number }>();
    private draftGeneration = 0;

    readonly generatedPrompt = signal('');
    readonly qualityResult = signal<PromptQualityResult | null>(null);
    readonly rawStoryCounter = signal('0 chars');
    readonly requirementCounter = signal('0 chars');
    readonly businessRulesCounter = signal('0 chars');

    readonly form = this.fb.nonNullable.group({
        rawStory: [''],
        title: [''],
        actor: [''],
        businessGoal: [''],
        requirement: [''],
        businessRules: [''],
        evidenceReferences: ['']
    });

    ngOnInit(): void {
        const draft = this.storage.load<StoryPromptInput>('story');
        this.form.setValue(normalizeStoryPromptInput(draft));
        this.updateCounters();
        this.updateQuality();
        this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
            this.updateCounters();
            this.updateQuality();
            this.draftChanges$.next({ value: this.form.getRawValue(), generation: this.draftGeneration });
        });
        this.draftChanges$.pipe(debounceTime(300), takeUntil(this.destroy$)).subscribe(change => {
            if (change.generation === this.draftGeneration) this.storage.save('story', change.value);
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
        this.history.add('Story', input.title, prompt);
        this.toast.showSuccess('Story prompt compiled!');
    }

    openHistory(prompt: string): void {
        this.generatedPrompt.set(prompt);
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
        this.updateQuality();
        this.toast.showSuccess('Story form cleared');
    }

    private updateCounters(): void {
        this.rawStoryCounter.set(`${this.form.controls.rawStory.value.length} chars`);
        this.requirementCounter.set(`${this.form.controls.requirement.value.length} chars`);
        this.businessRulesCounter.set(`${this.form.controls.businessRules.value.length} chars`);
    }

    private updateQuality(): void {
        this.qualityResult.set(this.quality.analyze('story', this.form.getRawValue()));
    }
}
