export interface StoryPromptInput {
  rawStory: string;
  title: string;
  actor: string;
  businessGoal: string;
  desiredBehavior: string;
}

export const EMPTY_STORY_PROMPT_INPUT: StoryPromptInput = {
  rawStory: '',
  title: '',
  actor: '',
  businessGoal: '',
  desiredBehavior: ''
};

export const SAMPLE_STORY_PROMPT_INPUT: StoryPromptInput = {
  rawStory: 'Support users need a safer way to review flagged order payloads before they are submitted for fulfilment.',
  title: 'Review flagged order payloads before submission',
  actor: 'QA support engineer',
  businessGoal: 'Prevent avoidable order failures by identifying incomplete or risky payloads before submission.',
  desiredBehavior: 'When an order is flagged, show the operator a structured review summary with clear reasons, affected fields, and a safe path back to editing.'
};
