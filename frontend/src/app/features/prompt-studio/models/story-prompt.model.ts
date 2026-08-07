export type StoryAcceptanceCriteriaStyle = 'Checklist' | 'Given / When / Then' | 'Both';
export type StoryDetailLevel = 'Concise' | 'Standard' | 'Deep';
export type StoryTargetFormat = 'Generic Markdown' | 'Jira' | 'Azure DevOps';

export const STORY_ACCEPTANCE_CRITERIA_STYLES = ['Checklist', 'Given / When / Then', 'Both'] as const;
export const STORY_DETAIL_LEVELS = ['Concise', 'Standard', 'Deep'] as const;
export const STORY_TARGET_FORMATS = ['Generic Markdown', 'Jira', 'Azure DevOps'] as const;
export const DEFAULT_STORY_ACCEPTANCE_CRITERIA_STYLE: StoryAcceptanceCriteriaStyle = 'Both';
export const DEFAULT_STORY_DETAIL_LEVEL: StoryDetailLevel = 'Standard';
export const DEFAULT_STORY_TARGET_FORMAT: StoryTargetFormat = 'Generic Markdown';

export interface StoryPromptInput {
  rawStory: string;
  title: string;
  actor: string;
  businessGoal: string;
  problemStatement: string;
  currentBehavior: string;
  desiredBehavior: string;
  scope: string;
  outOfScope: string;
  businessRules: string;
  dependencies: string;
  assumptions: string;
  uxReferences: string;
  apiDataConsiderations: string;
  securityConsiderations: string;
  performanceConsiderations: string;
  accessibilityLocalizationConsiderations: string;
  acceptanceCriteriaStyle: StoryAcceptanceCriteriaStyle;
  detailLevel: StoryDetailLevel;
  targetFormat: StoryTargetFormat;
  includeOpenQuestions: boolean;
  includeQaImpact: boolean;
  includeDefinitionOfReady: boolean;
  includeSuggestedTestCoverage: boolean;
}

export const EMPTY_STORY_PROMPT_INPUT: StoryPromptInput = {
  rawStory: '',
  title: '',
  actor: '',
  businessGoal: '',
  problemStatement: '',
  currentBehavior: '',
  desiredBehavior: '',
  scope: '',
  outOfScope: '',
  businessRules: '',
  dependencies: '',
  assumptions: '',
  uxReferences: '',
  apiDataConsiderations: '',
  securityConsiderations: '',
  performanceConsiderations: '',
  accessibilityLocalizationConsiderations: '',
  acceptanceCriteriaStyle: DEFAULT_STORY_ACCEPTANCE_CRITERIA_STYLE,
  detailLevel: DEFAULT_STORY_DETAIL_LEVEL,
  targetFormat: DEFAULT_STORY_TARGET_FORMAT,
  includeOpenQuestions: true,
  includeQaImpact: true,
  includeDefinitionOfReady: false,
  includeSuggestedTestCoverage: false
};

export const SAMPLE_STORY_PROMPT_INPUT: StoryPromptInput = {
  rawStory: 'Support users need a safer approval path for order requests that require review before fulfilment.',
  title: 'Review order requests before approval',
  actor: 'Order support reviewer',
  businessGoal: 'Reduce avoidable fulfilment failures by making review conditions and affected request details visible before approval.',
  problemStatement: 'Reviewers do not have a consistent place to understand why an order request needs attention before they approve it.',
  currentBehavior: 'A request that requires review is presented without a structured summary of the review reason or affected details.',
  desiredBehavior: 'When a request requires review, show the reviewer a structured summary with the supplied reason, affected details, and a path back to editing before approval.',
  scope: 'Show the review state, display supplied review details, allow the reviewer to inspect the request, and provide a clear path to continue or return to editing.',
  outOfScope: 'Changing review conditions, approval thresholds, or fulfilment routing is outside this request.',
  businessRules: 'A request that requires review remains identifiable as requiring review until the reviewer completes an available action.',
  dependencies: 'The review view depends on the existing order request details and approval state being available to the workspace.',
  assumptions: 'The business will confirm the review conditions and permitted reviewer roles before implementation.',
  uxReferences: 'Use the existing order review layout as the visual reference; no external URL or client-specific reference is required.',
  apiDataConsiderations: 'Confirm which fields provide the review reason, affected details, and current approval state.',
  securityConsiderations: 'Confirm which users may review or approve a request and whether any request details are sensitive.',
  performanceConsiderations: 'No response-time or throughput target was supplied; confirm whether the review experience has a measurable expectation.',
  accessibilityLocalizationConsiderations: 'Confirm keyboard access, screen-reader presentation, language support, and date or number formatting needs if the review view changes.',
  acceptanceCriteriaStyle: DEFAULT_STORY_ACCEPTANCE_CRITERIA_STYLE,
  detailLevel: DEFAULT_STORY_DETAIL_LEVEL,
  targetFormat: DEFAULT_STORY_TARGET_FORMAT,
  includeOpenQuestions: true,
  includeQaImpact: true,
  includeDefinitionOfReady: false,
  includeSuggestedTestCoverage: false
};

function readText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function readBoolean(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}

function readAcceptanceCriteriaStyle(value: unknown): StoryAcceptanceCriteriaStyle {
  return value === 'Checklist' || value === 'Given / When / Then' || value === 'Both'
    ? value
    : DEFAULT_STORY_ACCEPTANCE_CRITERIA_STYLE;
}

function readDetailLevel(value: unknown): StoryDetailLevel {
  return value === 'Concise' || value === 'Standard' || value === 'Deep' ? value : DEFAULT_STORY_DETAIL_LEVEL;
}

function readTargetFormat(value: unknown): StoryTargetFormat {
  return value === 'Generic Markdown' || value === 'Jira' || value === 'Azure DevOps'
    ? value
    : DEFAULT_STORY_TARGET_FORMAT;
}

export function normalizeStoryPromptInput(draft: Partial<StoryPromptInput> | null | undefined): StoryPromptInput {
  const source = draft ?? {};

  return {
    rawStory: readText(source.rawStory),
    title: readText(source.title),
    actor: readText(source.actor),
    businessGoal: readText(source.businessGoal),
    problemStatement: readText(source.problemStatement),
    currentBehavior: readText(source.currentBehavior),
    desiredBehavior: readText(source.desiredBehavior),
    scope: readText(source.scope),
    outOfScope: readText(source.outOfScope),
    businessRules: readText(source.businessRules),
    dependencies: readText(source.dependencies),
    assumptions: readText(source.assumptions),
    uxReferences: readText(source.uxReferences),
    apiDataConsiderations: readText(source.apiDataConsiderations),
    securityConsiderations: readText(source.securityConsiderations),
    performanceConsiderations: readText(source.performanceConsiderations),
    accessibilityLocalizationConsiderations: readText(source.accessibilityLocalizationConsiderations),
    acceptanceCriteriaStyle: readAcceptanceCriteriaStyle(source.acceptanceCriteriaStyle),
    detailLevel: readDetailLevel(source.detailLevel),
    targetFormat: readTargetFormat(source.targetFormat),
    includeOpenQuestions: readBoolean(source.includeOpenQuestions, EMPTY_STORY_PROMPT_INPUT.includeOpenQuestions),
    includeQaImpact: readBoolean(source.includeQaImpact, EMPTY_STORY_PROMPT_INPUT.includeQaImpact),
    includeDefinitionOfReady: readBoolean(source.includeDefinitionOfReady, EMPTY_STORY_PROMPT_INPUT.includeDefinitionOfReady),
    includeSuggestedTestCoverage: readBoolean(source.includeSuggestedTestCoverage, EMPTY_STORY_PROMPT_INPUT.includeSuggestedTestCoverage)
  };
}
