import { BugPromptInput } from '../models/bug-prompt.model';

export class BugPromptBuilder {
  build(input: BugPromptInput): string {
    return [
      `# Role\nAct as a senior QA engineer and technical writer. Refine the supplied defect information into a concise, developer-ready bug report prompt.`,
      `# Source of Truth\n- Preserve every supplied fact and evidence reference without changing its meaning.\n- Improve wording and structure only where the source supports it.\n- Never invent environment, version, permissions, API behavior, database behavior, customer information, credentials, logs, screenshots, or root cause.\n- Use [NEEDS INVESTIGATION] inside the relevant field when a required fact is absent.\n- Keep suggested severity or priority values marked [Suggested].`,
      this.buildInputData(input),
      `# Refinement Instructions\n1. Make the title concise and specific: use the affected feature, incorrect behavior, and trigger or context when those facts are supplied.\n2. Normalize Steps to Reproduce into ordered, atomic, reproducible actions. Remove duplicated actions without adding unsupported steps.\n3. Make Expected Result and Actual Result observable and clearly different. Preserve uncertainty instead of resolving it silently.\n4. Keep Severity and Priority exactly as supplied. If either is blank, suggest only a simple value from the allowed scale and mark it [Suggested].\n5. Preserve attachment and evidence references in the Evidence/Attachments field. If the source mentions evidence without a reference, mark that field [NEEDS INVESTIGATION].\n6. Return only the fixed final response contract. Do not add analysis, rationale, or other headings.`,
      `# Final Response Contract\nReturn exactly these eleven headings in this order, with no additional report headings:\n🐛 Bug Title\n📦 Module / Feature\n🌐 Environment\n⚙️ Application / Build Version\n🔧 Preconditions\n👣 Steps to Reproduce\n✅ Expected Result\n❌ Actual Result\n🚨 Severity\n📌 Priority\n📎 Evidence/Attachments`
    ].join('\n\n');
  }

  private buildInputData(input: BugPromptInput): string {
    return `# Supplied Bug Information
${this.formatField('Raw Bug Notes', input.rawNotes)}
${this.formatField('Existing Bug Title', input.title)}
${this.formatField('Module / Feature', input.module)}
${this.formatField('Environment', input.environment)}
${this.formatField('Application / Build Version', input.buildVersion)}
${this.formatField('Preconditions', input.preconditions)}
${this.formatField('Steps to Reproduce', input.steps)}
${this.formatField('Expected Result', input.expectedResult)}
${this.formatField('Actual Result', input.actualResult)}
${this.formatField('Severity', input.severity)}
${this.formatField('Priority', input.priority)}
${this.formatField('Evidence/Attachments', input.attachments)}`;
  }

  private formatField(label: string, value: string): string {
    const trimmed = value.trim();
    if (!trimmed) return `- **${label}:** [NEEDS INVESTIGATION]`;

    const lines = trimmed.split(/\r?\n/);
    if (lines.length === 1) return `- **${label}:** ${trimmed}`;

    return `- **${label}:**\n${lines.map(line => `  ${line}`).join('\n')}`;
  }
}
