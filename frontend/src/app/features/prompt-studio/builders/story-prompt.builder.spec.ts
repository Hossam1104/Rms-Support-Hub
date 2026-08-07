import { StoryPromptBuilder } from './story-prompt.builder';
import {
    EMPTY_STORY_PROMPT_INPUT,
    SAMPLE_STORY_PROMPT_INPUT,
    StoryPromptInput,
    normalizeStoryPromptInput
} from '../models/story-prompt.model';

describe('StoryPromptBuilder', () => {
    const builder = new StoryPromptBuilder();
    const headings = [
        '📖 Story Title',
        '👤 User / Role',
        '🎯 Business Goal',
        '📝 Requirement / Description',
        '📏 Business Rules',
        '✅ Acceptance Criteria',
        '📎 Evidence / References'
    ];

    it('uses the fixed seven-heading contract in order', () => {
        const output = builder.build(SAMPLE_STORY_PROMPT_INPUT);
        const positions = headings.map(heading => output.indexOf(heading));

        expect(positions.every(position => position >= 0)).toBe(true);
        expect(positions).toEqual([...positions].sort((left, right) => left - right));
        expect(output).toContain('no additional story headings');
        expect(output).not.toContain('Open Questions');
        expect(output).not.toContain('Definition of Ready');
        expect(output).not.toContain('Suggested Test Coverage');
    });

    it('preserves supplied facts and requests testable acceptance criteria', () => {
        const output = builder.build(SAMPLE_STORY_PROMPT_INPUT);

        expect(output).toBe(builder.build(SAMPLE_STORY_PROMPT_INPUT));
        expect(output).toContain(SAMPLE_STORY_PROMPT_INPUT.actor);
        expect(output).toContain(SAMPLE_STORY_PROMPT_INPUT.businessGoal);
        expect(output).toContain(SAMPLE_STORY_PROMPT_INPUT.businessRules);
        expect(output).toContain('observable, testable, specific');
        expect(output).toContain('Given / When / Then');
    });

    it('keeps missing actor and rules inline without inventing them', () => {
        const output = builder.build(EMPTY_STORY_PROMPT_INPUT);

        expect(output).toContain('- **User / Role:** [NEEDS CLARIFICATION]');
        expect(output).toContain('- **Business Rules:** [NEEDS CLARIFICATION]');
        expect(output).toContain('Never invent actors');
        expect(output).toContain('Put unknown outcomes in a criterion as [NEEDS CLARIFICATION]');
    });

    it('normalizes legacy semantic fields and ignores retired options', () => {
        const normalized = normalizeStoryPromptInput({
            title: 'Legacy title',
            actor: 'Legacy actor',
            desiredBehavior: 'Legacy requirement',
            uxReferences: 'legacy-reference.png',
            detailLevel: 'Deep',
            includeQaImpact: true
        } as Partial<StoryPromptInput> & Record<string, unknown>);

        expect(normalized.title).toBe('Legacy title');
        expect(normalized.actor).toBe('Legacy actor');
        expect(normalized.requirement).toBe('Legacy requirement');
        expect(normalized.evidenceReferences).toBe('legacy-reference.png');
        expect(normalized).not.toHaveProperty('detailLevel');
        expect(normalized).not.toHaveProperty('includeQaImpact');
    });
});
