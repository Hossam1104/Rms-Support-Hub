import { StoryPromptInput } from '../models/story-prompt.model';

const CLARIFICATION_MARKER = '[NEEDS CLARIFICATION]';

export class StoryPromptBuilder {
    build(input: StoryPromptInput): string {
        return [
            `# Role\nAct as a senior product analyst, requirements engineer, and QA-minded technical writer. Refine the supplied request into a concise implementation-ready story prompt.`,
            `# Source of Truth\n- Preserve every supplied fact, rule, qualification, and evidence reference without changing its meaning.\n- Improve wording and structure only where the source supports it.\n- Never invent actors, permissions, workflows, API behavior, database behavior, validation messages, thresholds, or business rules.\n- Use ${CLARIFICATION_MARKER} inside the relevant field when a required detail is absent.\n- Keep assumptions, suggestions, and inferred details distinct from confirmed source information.`,
            this.buildInputData(input),
            `# Refinement Instructions\n1. Refine the title so it describes the capability, not a generic enhancement.\n2. Preserve the supplied actor and business goal. If either is missing, keep ${CLARIFICATION_MARKER} in that field.\n3. Turn the Requirement / Description into a clear desired behavior without adding unsupported scope.\n4. Write Acceptance Criteria that are observable, testable, specific, and atomic where practical. Use Given / When / Then only when it makes a criterion clearer. Put unknown outcomes in a criterion as ${CLARIFICATION_MARKER}.\n5. Keep Business Rules limited to confirmed rules. If none are supplied, use ${CLARIFICATION_MARKER}.\n6. Preserve references in Evidence / References and label any evidence-supported detail [Inferred].\n7. Return only the fixed final response contract. Do not add analysis, open questions, readiness checklists, or other headings.`,
            `# Final Response Contract\nReturn exactly these seven headings in this order, with no additional story headings:\n📖 Story Title\n👤 User / Role\n🎯 Business Goal\n📝 Requirement / Description\n📏 Business Rules\n✅ Acceptance Criteria\n📎 Evidence / References`
        ].join('\n\n');
    }

    private buildInputData(input: StoryPromptInput): string {
        return `# Supplied Story Information\n${this.formatField('Raw Story / Requirement', input.rawStory)}\n${this.formatField('Story Title', input.title)}\n${this.formatField('User / Role', input.actor)}\n${this.formatField('Business Goal', input.businessGoal)}\n${this.formatField('Requirement / Description', input.requirement)}\n${this.formatField('Business Rules', input.businessRules)}\n${this.formatField('Evidence / References', input.evidenceReferences)}`;
    }

    private formatField(label: string, value: string): string {
        const trimmed = value.trim();
        if (!trimmed) return `- **${label}:** ${CLARIFICATION_MARKER}`;

        const lines = trimmed.split(/\r?\n/);
        if (lines.length === 1) return `- **${label}:** ${trimmed}`;

        return `- **${label}:**\n${lines.map(line => `  ${line}`).join('\n')}`;
    }
}
