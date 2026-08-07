import { TestCasePromptInput } from '../models/test-case-prompt.model';

export class TestCasePromptBuilder {
    build(input: TestCasePromptInput): string {
        return [
            `# Role\nAct as a principal QA engineer. Refine the supplied scenario into a concise, reproducible manual test-case prompt for another AI.`,
            `# Source of Truth\n- Preserve every supplied fact, value, priority, and evidence reference without changing its meaning.\n- Improve wording and structure only where the source supports it.\n- Never invent environment, permissions, API behavior, database behavior, credentials, validation messages, thresholds, or system state.\n- Use [NEEDS INVESTIGATION] inside the relevant field when a required fact is absent.\n- If supplied evidence supports an additional detail, include it in the relevant field and mark it [Inferred].`,
            this.buildInputData(input),
            `# Refinement Instructions\n1. Refine the title into an action or condition plus the expected behavior.\n2. Keep the Scenario / Objective directly tied to the supplied requirement or evidence.\n3. Normalize Steps into ordered, atomic, reproducible actions and remove duplicated actions without adding unsupported steps.\n4. Make Expected Result observable, specific, and directly linked to the scenario. Replace vague wording with a visible, measurable, recorded, or state-based outcome when the source supports it.\n5. Preserve Priority exactly as supplied. If it is blank, suggest only a simple P0, P1, P2, or P3 value and mark it [Suggested].\n6. Keep screenshot and evidence interpretation inside Module / Feature, Scenario / Objective, Steps, Expected Result, or Evidence / Attachments. Never create a separate inference or warning section.\n7. Return only the fixed final response contract. Do not add analysis, recommendations, postconditions, cleanup, or other headings.`,
            `# Final Response Contract\nReturn exactly these nine headings in this order, with no additional test-case headings:\n🧪 Test Case Title\n📦 Module / Feature\n🎯 Scenario / Objective\n🔧 Preconditions\n🧾 Test Data\n👣 Steps\n✅ Expected Result\n🚨 Priority\n📎 Evidence / Attachments`
        ].join('\n\n');
    }

    private buildInputData(input: TestCasePromptInput): string {
        return `# Supplied Test Case Information\n${this.formatField('Requirement / Story Reference', input.requirementReference)}\n${this.formatField('Test Case Title', input.title)}\n${this.formatField('Module / Feature', input.module)}\n${this.formatField('Scenario / Objective', input.scenario)}\n${this.formatField('Preconditions', input.preconditions)}\n${this.formatField('Test Data', input.testData)}\n${this.formatField('Steps', input.steps)}\n${this.formatField('Expected Result', input.expectedResult)}\n${this.formatField('Priority', input.priority)}\n${this.formatField('Evidence / Attachments', input.attachments)}`;
    }

    private formatField(label: string, value: string): string {
        const trimmed = value.trim();
        if (!trimmed) return `- **${label}:** [NEEDS INVESTIGATION]`;

        const lines = trimmed.split(/\r?\n/);
        if (lines.length === 1) return `- **${label}:** ${trimmed}`;

        return `- **${label}:**\n${lines.map(line => `  ${line}`).join('\n')}`;
    }
}
