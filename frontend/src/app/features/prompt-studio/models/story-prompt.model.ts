export interface StoryPromptInput {
    rawStory: string;
    title: string;
    actor: string;
    businessGoal: string;
    requirement: string;
    businessRules: string;
    evidenceReferences: string;
}

export const EMPTY_STORY_PROMPT_INPUT: StoryPromptInput = {
    rawStory: '',
    title: '',
    actor: '',
    businessGoal: '',
    requirement: '',
    businessRules: '',
    evidenceReferences: ''
};

export const SAMPLE_STORY_PROMPT_INPUT: StoryPromptInput = {
    rawStory: 'Support users need a safer approval path for order requests that require review before fulfilment.',
    title: 'Review order requests before approval',
    actor: 'Order support reviewer',
    businessGoal: 'Reduce avoidable fulfilment failures by making review conditions and affected request details visible before approval.',
    requirement: 'When a request requires review, show the reviewer a structured summary with the supplied reason, affected details, and a path back to editing before approval.',
    businessRules: 'A request that requires review remains identifiable as requiring review until the reviewer completes an available action.',
    evidenceReferences: 'Existing order review layout reference; no external URL or client-specific reference supplied.'
};

function readText(value: unknown): string {
    return typeof value === 'string' ? value : '';
}

export function normalizeStoryPromptInput(draft: Partial<StoryPromptInput> | null | undefined): StoryPromptInput {
    const source = (draft ?? {}) as Record<string, unknown>;
    const requirement = readText(source['requirement']) || readText(source['desiredBehavior']);
    const evidenceReferences = readText(source['evidenceReferences']) || readText(source['uxReferences']);

    return {
        rawStory: readText(source['rawStory']),
        title: readText(source['title']),
        actor: readText(source['actor']),
        businessGoal: readText(source['businessGoal']),
        requirement,
        businessRules: readText(source['businessRules']),
        evidenceReferences
    };
}
