import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EMPTY_STORY_PROMPT_INPUT, SAMPLE_STORY_PROMPT_INPUT } from '../models/story-prompt.model';
import { PROMPT_STUDIO_DRAFT_KEYS } from '../services/prompt-storage.service';
import { StoryRefinerComponent } from './story-refiner.component';

describe('StoryRefinerComponent', () => {
    beforeEach(async () => {
        localStorage.clear();
        await TestBed.configureTestingModule({
            imports: [StoryRefinerComponent],
            providers: [provideRouter([])]
        }).compileComponents();
    });

    function create() {
        const fixture = TestBed.createComponent(StoryRefinerComponent);
        fixture.detectChanges();
        return fixture;
    }

    it('renders useful source fields without retired configuration controls', () => {
        const fixture = create();

        expect(fixture.nativeElement.querySelector('#story-raw')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-title')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-actor')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-goal')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-requirement')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-rules')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-evidence')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('#story-acceptance-style')).toBeNull();
        expect(fixture.nativeElement.querySelector('#story-detail-level')).toBeNull();
        expect(fixture.nativeElement.querySelector('#story-target-format')).toBeNull();
        expect(fixture.nativeElement.querySelectorAll('input[type="checkbox"]')).toHaveLength(0);
        expect(fixture.nativeElement.querySelectorAll('.story-form__section')).toHaveLength(3);
    });

    it('loads, persists, restores, and clears the simplified sample draft', () => {
        const fixture = create();

        fixture.componentInstance.loadSample();
        expect(fixture.componentInstance.form.getRawValue()).toEqual(SAMPLE_STORY_PROMPT_INPUT);
        expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBe(JSON.stringify(SAMPLE_STORY_PROMPT_INPUT));

        const restored = create();
        expect(restored.componentInstance.form.getRawValue()).toEqual(SAMPLE_STORY_PROMPT_INPUT);

        restored.componentInstance.clearForm();
        expect(restored.componentInstance.form.getRawValue()).toEqual(EMPTY_STORY_PROMPT_INPUT);
        expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBeNull();
    });

    it('maps old semantic draft fields and ignores retired options', () => {
        localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS.story, JSON.stringify({
            title: 'Legacy story title',
            actor: 'Legacy actor',
            businessGoal: 'Legacy goal',
            desiredBehavior: 'Legacy desired behavior',
            uxReferences: 'legacy-reference.png',
            acceptanceCriteriaStyle: 'Both'
        }));

        const fixture = create();
        const value = fixture.componentInstance.form.getRawValue();

        expect(value.title).toBe('Legacy story title');
        expect(value.actor).toBe('Legacy actor');
        expect(value.businessGoal).toBe('Legacy goal');
        expect(value.requirement).toBe('Legacy desired behavior');
        expect(value.evidenceReferences).toBe('legacy-reference.png');
        expect(value).not.toHaveProperty('acceptanceCriteriaStyle');
    });

    it('does not recreate a draft from a pending write after clear', () => {
        vi.useFakeTimers();
        try {
            const fixture = create();
            fixture.componentInstance.form.patchValue({ title: 'Pending draft' });
            fixture.componentInstance.clearForm();
            vi.advanceTimersByTime(300);

            expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.story)).toBeNull();
        } finally {
            vi.useRealTimers();
        }
    });

    it('generates a deterministic prompt and exposes both downloads', () => {
        const fixture = create();
        fixture.componentInstance.form.setValue(SAMPLE_STORY_PROMPT_INPUT);

        fixture.componentInstance.generate();
        fixture.detectChanges();

        expect(fixture.componentInstance.generatedPrompt()).toContain('Review order requests before approval');
        expect(fixture.nativeElement.querySelector('.prompt-preview__output')?.textContent).toContain('📖 Story Title');
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Copy generated prompt"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as Markdown"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as plain text"]')).toBeTruthy();
    });

    it('updates source counters and supports Ctrl+Enter and Cmd+Enter', () => {
        const fixture = create();
        fixture.componentInstance.form.patchValue({ rawStory: 'raw', requirement: 'desired behavior', businessRules: 'rule one\nrule two' });

        expect(fixture.componentInstance.rawStoryCounter()).toBe('3 chars');
        expect(fixture.componentInstance.requirementCounter()).toBe('16 chars');
        expect(fixture.componentInstance.businessRulesCounter()).toBe('17 chars');

        const workspace = fixture.debugElement.children[0].componentInstance;
        const ctrlEvent = new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true, cancelable: true });
        const cmdEvent = new KeyboardEvent('keydown', { key: 'Enter', metaKey: true, cancelable: true });

        workspace.onGenerateShortcut(ctrlEvent);
        const first = fixture.componentInstance.generatedPrompt();
        workspace.onGenerateShortcut(cmdEvent);

        expect(ctrlEvent.defaultPrevented).toBe(true);
        expect(cmdEvent.defaultPrevented).toBe(true);
        expect(first).toContain('# Role');
        expect(fixture.componentInstance.generatedPrompt()).toBe(first);
    });
});
