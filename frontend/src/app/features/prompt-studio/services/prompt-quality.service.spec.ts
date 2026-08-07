import { SAMPLE_BUG_PROMPT_INPUT } from '../models/bug-prompt.model';
import { SAMPLE_STORY_PROMPT_INPUT } from '../models/story-prompt.model';
import { EMPTY_TEST_CASE_PROMPT_INPUT, SAMPLE_TEST_CASE_PROMPT_INPUT } from '../models/test-case-prompt.model';
import { PromptQualityService } from './prompt-quality.service';

describe('PromptQualityService', () => {
    let service: PromptQualityService;

    beforeEach(() => {
        service = new PromptQualityService();
    });

    it('returns deterministic complete results for every generator type', () => {
        const results = [
            service.analyze('bug', SAMPLE_BUG_PROMPT_INPUT),
            service.analyze('story', SAMPLE_STORY_PROMPT_INPUT),
            service.analyze('testCase', SAMPLE_TEST_CASE_PROMPT_INPUT)
        ];

        for (const result of results) {
            expect(result.score).toBe(100);
            expect(result.findings).toEqual([]);
            expect(result.facts.length).toBeGreaterThan(0);
            expect(result.allowedInference.length).toBeGreaterThan(0);
            expect(result.score).toBeGreaterThanOrEqual(0);
            expect(result.score).toBeLessThanOrEqual(100);
        }

        expect(results[0]).toEqual(service.analyze('bug', SAMPLE_BUG_PROMPT_INPUT));
    });

    it('reports missing and vague test-case information without blocking generation', () => {
        const result = service.analyze('testCase', {
            ...EMPTY_TEST_CASE_PROMPT_INPUT,
            title: 'Test',
            steps: '1. Do it properly.\n2. Do it properly.',
            expectedResult: 'It works.'
        });

        expect(result.score).toBeGreaterThanOrEqual(0);
        expect(result.score).toBeLessThanOrEqual(100);
        expect(result.missingFields).toContain('Module / Feature');
        expect(result.findings).toEqual(expect.arrayContaining([
            expect.objectContaining({ field: 'Steps', message: 'Vague wording was detected.' }),
            expect.objectContaining({ field: 'Steps', message: 'Duplicate execution steps were detected.' }),
            expect.objectContaining({ field: 'Expected Result', message: 'The expected result has no obvious observable outcome.' })
        ]));
    });

    it('detects equal and contradictory bug outcomes', () => {
        const equal = service.analyze('bug', {
            ...SAMPLE_BUG_PROMPT_INPUT,
            expectedResult: 'The confirmation is visible.',
            actualResult: 'The confirmation is visible.'
        });
        const contradictory = service.analyze('bug', {
            ...SAMPLE_BUG_PROMPT_INPUT,
            expectedResult: 'The confirmation is visible.',
            actualResult: 'The confirmation is not visible.'
        });

        expect(equal.findings).toEqual(expect.arrayContaining([
            expect.objectContaining({ message: 'Expected and Actual results are identical.' })
        ]));
        expect(contradictory.findings).toEqual(expect.arrayContaining([
            expect.objectContaining({ message: 'Potentially contradictory outcomes were detected.' })
        ]));
    });

    it('flags sensitive source text, ambiguous actors, and triage mismatch as advisory findings', () => {
        const story = service.analyze('story', {
            ...SAMPLE_STORY_PROMPT_INPUT,
            rawStory: 'The user should paste the password into the approval form.',
            actor: 'user'
        });
        const bug = service.analyze('bug', {
            ...SAMPLE_BUG_PROMPT_INPUT,
            severity: 'Critical',
            priority: 'P3'
        });

        expect(story.sensitiveContent).toBe(true);
        expect(story.findings).toEqual(expect.arrayContaining([
            expect.objectContaining({ field: 'Source information' }),
            expect.objectContaining({ field: 'User / Role' })
        ]));
        expect(bug.findings).toEqual(expect.arrayContaining([
            expect.objectContaining({ field: 'Severity / Priority', severity: 'info' })
        ]));
    });

    it('exposes assumptions and facts without making network calls', () => {
        const result = service.analyze('story', {
            ...SAMPLE_STORY_PROMPT_INPUT,
            businessGoal: 'Assume the reviewer has the required permission.'
        });

        expect(result.facts).toContain('Business Goal');
        expect(result.assumptions).toContain('Business Goal');
        expect(result.allowedInference).toEqual([
            'Refine wording and acceptance criteria structure',
            'Use [NEEDS CLARIFICATION] for unsupported acceptance outcomes'
        ]);
    });
});