import { StoryPromptInput } from '../models/story-prompt.model';

const CLARIFICATION_MARKER = '[NEEDS CLARIFICATION]';

export class StoryPromptBuilder {
  build(input: StoryPromptInput): string {
    return [
      this.buildRole(),
      this.buildSourceOfTruth(),
      this.buildStoryInput(input),
      this.buildRefinementInstructions(input),
      this.buildAcceptanceCriteriaInstructions(input),
      this.buildNonFunctionalInstructions(input),
      this.buildOutputContract(input),
      this.buildFormatInstructions(input.targetFormat),
      this.buildOptionalSections(input)
    ].join('\n\n');
  }

  private buildRole(): string {
    return `# Role
Act as a senior product analyst, requirements engineer, and QA-minded technical writer. Transform the supplied business request into an implementation-ready user story prompt for another AI to refine. Keep the result useful for product, engineering, and QA without inventing requirements.`;
  }

  private buildSourceOfTruth(): string {
    return `# Source of Truth and Safety
- User-provided requirements are the source of truth. Preserve every supplied fact, rule, assumption, reference, and qualification without changing its meaning.
- Distinguish these labels visibly: **Confirmed Requirement**, **Provided Assumption**, **Suggested Requirement**, and **Open Question**.
- Do not promote a Provided Assumption into a Confirmed Requirement or Acceptance Criteria unless the user explicitly confirms it.
- Do not invent actor permissions, workflows, API behavior, validation rules, business rules, database behavior, notifications, thresholds, performance SLAs, security requirements, localization behavior, or analytics events.
- When a required detail is missing or ambiguous, use ${CLARIFICATION_MARKER} or place a specific question in the enabled Open Questions section. Do not silently fill the gap.
- Preserve explicitly supplied business rules exactly while improving only their structure or readability. Any new rule belongs under Recommendations or Open Questions and must be labeled [Suggested].`;
  }

  private buildStoryInput(input: StoryPromptInput): string {
    return `# Supplied Story Information
${this.formatField('Raw Story / Request', input.rawStory)}
${this.formatField('Proposed Title', input.title)}
${this.formatField('Actor / Persona', input.actor)}
${this.formatField('Business Goal', input.businessGoal)}
${this.formatField('Problem Statement', input.problemStatement)}
${this.formatField('Current Behavior', input.currentBehavior)}
${this.formatField('Desired Behavior', input.desiredBehavior)}
${this.formatField('In Scope', input.scope)}
${this.formatField('Out of Scope', input.outOfScope)}
${this.formatField('Business Rules', input.businessRules)}
${this.formatField('Dependencies', input.dependencies)}
${this.formatField('Assumptions', input.assumptions)}
${this.formatField('UX / Design References', input.uxReferences)}
${this.formatField('API / Data Considerations', input.apiDataConsiderations)}
${this.formatField('Security Considerations', input.securityConsiderations)}
${this.formatField('Performance Considerations', input.performanceConsiderations)}
${this.formatField('Accessibility / Localization Considerations', input.accessibilityLocalizationConsiderations)}
- **Acceptance Criteria Style:** ${input.acceptanceCriteriaStyle}
- **Detail level:** ${input.detailLevel}
- **Target Format:** ${input.targetFormat}
- **Include Open Questions:** ${input.includeOpenQuestions ? 'Yes' : 'No'}
- **Include QA Impact:** ${input.includeQaImpact ? 'Yes' : 'No'}
- **Include Definition of Ready:** ${input.includeDefinitionOfReady ? 'Yes' : 'No'}
- **Include Suggested Test Coverage:** ${input.includeSuggestedTestCoverage ? 'Yes' : 'No'}`;
  }

  private buildRefinementInstructions(input: StoryPromptInput): string {
    const detailInstruction = {
      Concise: `- Detail level: Concise. Produce a refined title, a story statement, business goal, core in-scope behavior, focused acceptance criteria, and only the specific open questions needed to proceed.`,
      Standard: `- Detail level: Standard. Add business rules, assumptions, dependencies, negative and boundary acceptance criteria, and validation/error behavior. Include QA impact when enabled.`,
      Deep: `- Detail level: Deep. Add relevant security, permissions, data integrity, performance, accessibility, localization, observability, advanced edge considerations, Definition of Ready, and suggested test categories when enabled. Surface unknowns instead of filling them.`
    }[input.detailLevel];

    return `# Refinement Instructions
1. Produce a clear refined title without changing the supplied intent.
2. Produce this user story statement when the evidence supports it:
   As a <actor>
   I want <capability>
   So that <business value>
   If the actor or business value is unavailable, use ${CLARIFICATION_MARKER} in that position. Never invent a persona or value.
3. Separate business context into the supplied goal, problem statement, current behavior, and desired behavior. Preserve conflicts as Open Questions rather than choosing silently.
4. Present In Scope and Out of Scope separately. If Out of Scope was not provided, mark it as not defined and ask a specific question; do not invent exclusions.
5. Keep supplied assumptions visibly labeled as Provided Assumption. Do not use them as confirmed acceptance criteria.
6. Preserve relevant references and dependencies. Do not infer an API, database, workflow, permission, notification, threshold, or integration that was not supplied.
7. Make each open question specific, actionable, and minimal. Avoid generic questions such as "Anything else?".
${detailInstruction}`;
  }

  private buildAcceptanceCriteriaInstructions(input: StoryPromptInput): string {
    const styleInstruction = {
      Checklist: `# Acceptance Criteria Format: Checklist
Write a focused checklist of atomic, observable, testable conditions. Use one behavior per item and avoid vague wording such as works correctly, behaves properly, handles normally, or functions as expected.`,
      'Given / When / Then': `# Acceptance Criteria Format: Given / When / Then
Write focused scenarios using:
Given = known precondition
When = one user or system action where possible
Then = one observable result
Do not invent data or environmental conditions; use ${CLARIFICATION_MARKER} when required information is unknown.`,
      Both: `# Acceptance Criteria Format: Both
Provide a concise checklist and Given / When / Then scenarios for the same prioritized behaviors without unnecessary duplication. Use Given for known preconditions, When for one action where possible, and Then for observable results.`
    }[input.acceptanceCriteriaStyle];

    return `${styleInstruction}

## Acceptance Criteria Quality
- Request useful, prioritized coverage rather than hundreds of criteria.
- Include happy-path, negative, boundary, validation/error, permissions, data-integrity, and state-transition behavior only when relevant to supplied requirements or the application context.
- Keep criteria observable, testable, atomic where possible, unambiguous, and implementation-neutral unless technical implementation is explicitly required.
- Do not turn assumptions or suggestions into confirmed criteria. Unsupported details belong in ${CLARIFICATION_MARKER} or an Open Question.`;
  }

  private buildNonFunctionalInstructions(input: StoryPromptInput): string {
    if (input.detailLevel === 'Concise') {
      return `# Relevant Quality Considerations
For Concise output, include only non-functional or validation details explicitly supplied or clearly relevant to the core request. Do not expand unknowns into requirements; identify them briefly for clarification.`;
    }

    const depth = input.detailLevel === 'Deep'
      ? `For Deep output, examine security and authorization, sensitive-data exposure, audit or logging, data integrity, duplicate operations, consistency, state transitions, transactional expectations, concurrent updates, idempotency, measurable performance expectations, accessibility, localization, observability, and advanced edge cases only when relevant. Include a Definition of Ready and suggested testing categories when enabled.`
      : `For Standard output, identify missing validation and error behavior, required-field handling, invalid input, unauthorized access, invalid state transitions, and external dependency failures when relevant. Add QA impact when enabled.`;

    return `# Validation and Non-Functional Guidance
${depth}
- Do not mechanically add security, performance, accessibility, localization, or observability requirements to every story.
- Do not invent error messages, roles, permissions, thresholds, response-time values, throughput values, SLAs, audit events, analytics events, language behavior, or storage implementation.
- When performance matters but no target is supplied, add the open question: "What response-time or throughput expectation applies?"
- Distinguish a confirmed requirement, a relevant consideration, a Provided Assumption, a Suggested Requirement, and an Open Question.`;
  }

  private buildOutputContract(input: StoryPromptInput): string {
    const sections = [
      'Refined Title',
      'User Story Statement',
      'Business Context',
      'Functional Scope',
      'Out of Scope',
      'Acceptance Criteria',
      input.detailLevel === 'Concise' ? '' : 'Business Rules',
      input.detailLevel === 'Concise' ? '' : 'Assumptions',
      input.detailLevel === 'Concise' ? '' : 'Dependencies',
      input.detailLevel === 'Concise' ? '' : 'Validation / Error Behavior',
      input.detailLevel === 'Deep' ? 'Permissions / Security' : '',
      input.detailLevel === 'Deep' ? 'Data Integrity' : '',
      input.detailLevel === 'Deep' ? 'Performance' : '',
      input.detailLevel === 'Deep' ? 'Accessibility / Localization' : '',
      input.detailLevel === 'Deep' ? 'Observability / Analytics' : '',
      input.includeOpenQuestions ? 'Open Questions' : '',
      input.includeQaImpact ? 'QA Impact' : '',
      input.includeSuggestedTestCoverage ? 'Suggested Test Coverage' : '',
      input.includeDefinitionOfReady ? 'Definition of Ready' : ''
    ].filter(Boolean);

    return `# Final Response Contract
Return only a structured, implementation-ready story in the requested target format. Use this stable section order:
${sections.map((section, index) => `${index + 1}. ${section}`).join('\n')}

Use ${CLARIFICATION_MARKER} for unresolved required values. Do not present suggestions, assumptions, or relevant considerations as confirmed requirements. Keep the scope bounded by the supplied request and avoid adding unsupported implementation detail.`;
  }

  private buildFormatInstructions(targetFormat: StoryPromptInput['targetFormat']): string {
    const common = `Use the stable section order and preserve the distinction between confirmed requirements, Provided Assumptions, Suggested Requirements, and Open Questions. This is formatting only; do not call Jira or Azure DevOps APIs.`;

    if (targetFormat === 'Jira') {
      return `# Target Format: Jira
${common}
Use Jira-friendly labels such as Summary, Description, User Story, Scope, Out of Scope, Business Rules, Assumptions, Dependencies, Acceptance Criteria, Open Questions, QA Impact, and Definition of Ready.`;
    }

    if (targetFormat === 'Azure DevOps') {
      return `# Target Format: Azure DevOps
${common}
Use Azure DevOps-friendly labels such as Title, Description, User Story, Area, Out of Scope, Business Rules, Assumptions, Dependencies, Acceptance Criteria, Open Questions, QA Impact, and Definition of Ready.`;
    }

    return `# Target Format: Generic Markdown
${common}
Use Markdown headings and lists for the story, scope, rules, acceptance criteria, questions, and enabled optional sections. Do not wrap the response in an unnecessary code block.`;
  }

  private buildOptionalSections(input: StoryPromptInput): string {
    const sections = [
      input.includeOpenQuestions
        ? `### Open Questions
List only specific, actionable unresolved questions about actors, scope, behavior, validation, permissions, dependencies, UX, data, performance, security, localization, or observability. Mark required unknowns ${CLARIFICATION_MARKER}.`
        : '',
      input.includeQaImpact
        ? `### QA Impact
List relevant suggested coverage categories only, such as Happy Path, Negative, Boundary, State / Navigation, Security, Data Integrity, Accessibility, Localization, Performance, and Recovery. Do not write full test cases.`
        : '',
      input.includeSuggestedTestCoverage
        ? `### Suggested Test Coverage
Suggest a concise set of relevant testing categories and risk areas. Keep suggestions separate from confirmed requirements and do not invent data, expected values, or test steps.`
        : '',
      input.includeDefinitionOfReady
        ? `### Definition of Ready
Generate a concise checklist. Use [x] only when the supplied information confirms the item and [ ] unresolved when it does not. Check business goal, actor, scope, rules, dependencies, designs, API/data contract, testable acceptance criteria, and resolution of critical open questions.`
        : ''
    ].filter(Boolean);

    return `# Optional Output Sections
${sections.length ? sections.join('\n\n') : '- Omit all optional output sections.'}`;
  }

  private formatField(label: string, value: string): string {
    const trimmed = value.trim();
    if (!trimmed) return `- **${label}:** ${CLARIFICATION_MARKER}`;

    const lines = trimmed.split(/\r?\n/);
    if (lines.length === 1) return `- **${label}:** ${trimmed}`;

    return `- **${label}:**\n${lines.map(line => `  ${line}`).join('\n')}`;
  }
}
