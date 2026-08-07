import { StoryPromptInput } from '../models/story-prompt.model';

function valueOrMissing(value: string): string {
  return value.trim() || '[NEEDS INVESTIGATION]';
}

export class StoryPromptBuilder {
  build(input: StoryPromptInput): string {
    return `# Role
Act as a Senior Product Analyst. Refine the supplied rough request into a clear, implementation-ready user story foundation.

# Source of Truth
Preserve every supplied fact. Do not convert assumptions into confirmed requirements, and do not invent missing business rules.

# Input Story
${valueOrMissing(input.rawStory)}

# Provided Context
- **Proposed Title:** ${valueOrMissing(input.title)}
- **Actor / Persona:** ${valueOrMissing(input.actor)}
- **Business Goal:** ${valueOrMissing(input.businessGoal)}
- **Desired Behavior:** ${valueOrMissing(input.desiredBehavior)}

# Instructions
1. Produce a refined story title and one-sentence user story statement.
2. Summarize the business context and desired behavior without changing supplied facts.
3. Separate confirmed requirements from assumptions and open questions.
4. Mark unsupported details as [NEEDS INVESTIGATION].
5. Keep the result concise; advanced acceptance criteria, non-functional coverage, and QA impact analysis are intentionally out of scope for this foundation.

# Required Output
## Refined Title
## User Story
## Business Context
## Desired Behavior
## Assumptions
## Open Questions`;
  }
}
