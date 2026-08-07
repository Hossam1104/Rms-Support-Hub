import { BugPromptInput } from '../models/bug-prompt.model';

export class BugPromptBuilder {
  build(input: BugPromptInput): string {
    return [
      this.buildRole(),
      this.buildSourceOfTruth(),
      this.buildInputData(input),
      this.buildInstructions(input),
      this.buildOutputContract(),
      this.buildFormatInstructions(input.targetFormat),
      this.buildOptionalSections(input)
    ].join('\n\n');
  }

  private buildRole(): string {
    return `# Role
Act as a Senior QA Engineer and technical writer. Transform rough defect information into a developer-ready bug report that is useful for implementation, diagnosis, and verification.`;
  }

  private buildSourceOfTruth(): string {
    return `# Source of Truth and Safety
- Every non-empty value in **Supplied Bug Information** is a CONFIRMED FACT and is authoritative. Preserve its meaning and exact evidence references.
- Do not invent or silently assume an environment, device, account, customer, build or version, log, screenshot, error code, database state, API behavior, test data, or root cause.
- Treat empty values as MISSING INFORMATION. Use [NEEDS INVESTIGATION] wherever the final report needs a value that cannot be safely established.
- Keep CONFIRMED FACT, INFERRED / SUGGESTED, and MISSING INFORMATION visibly distinct.
- Any reasoning must be labeled [Suggested] or Potential Cause to Validate. Never present an unsupported root-cause claim as a confirmed fact.`;
  }

  private buildInputData(input: BugPromptInput): string {
    return `# Supplied Bug Information
${this.formatField('Raw Bug Notes', input.rawNotes)}
${this.formatField('Existing Bug Title', input.title)}
${this.formatField('Module / Feature', input.module)}
${this.formatField('Environment', input.environment)}
${this.formatField('Application / Build Version', input.buildVersion)}
${this.formatField('Preconditions', input.preconditions)}
${this.formatField('Test Data', input.testData)}
${this.formatField('Steps to Reproduce', input.steps)}
${this.formatField('Expected Result', input.expectedResult)}
${this.formatField('Actual Result', input.actualResult)}
${this.formatField('Frequency / Reproducibility', input.frequency)}
${this.formatField('Severity', input.severity)}
${this.formatField('Priority', input.priority)}
${this.formatField('Business / User Impact', input.impact)}
${this.formatField('Error Message', input.errorMessage)}
${this.formatField('Logs / Technical Evidence', input.logs)}
${this.formatField('Attachments', input.attachments)}
${this.formatField('Related Ticket / Reference', input.relatedReference)}
${this.formatField('Detail Level', input.detailLevel)}
${this.formatField('Target Format', input.targetFormat)}`;
  }

  private buildInstructions(input: BugPromptInput): string {
    const detailInstruction = {
      Concise: '- Detail level: Concise. Produce a compact report containing essential confirmed facts and only the most actionable missing information.',
      Standard: '- Detail level: Standard. Preserve facts, identify missing information, explain any needed severity or priority suggestions, and include clear evidence handling and fix guidance.',
      Deep: '- Detail level: Deep. Add contradiction checks, diagnostic gaps, regression considerations, and relevant data, security, and integration considerations without inventing details.'
    }[input.detailLevel];

    const suggestions = [
      input.severity.trim() ? '' : '- Severity was not supplied. If useful, provide a candidate labeled [Suggested] with a short rationale; never treat it as a confirmed fact.',
      input.priority.trim() ? '' : '- Priority was not supplied. If useful, provide a candidate labeled [Suggested] with a short rationale; never treat it as a confirmed fact.'
    ].filter(Boolean);

    return `# Processing Instructions
1. Preserve every supplied fact, value, reference, filename, URL, error message, and log reference. Do not overwrite confirmed Severity or Priority.
2. Use Raw Bug Notes as context, but resolve conflicts in favor of explicit structured fields and call out contradictions rather than choosing silently.
3. Normalize supplied reproduction steps into atomic actions in deterministic order with observable outcomes. Preserve ambiguous steps and identify the ambiguity as missing information; do not invent navigation, accounts, devices, test data, or actions.
4. Preserve Expected Result and Actual Result facts, make them observable where possible, and identify contradictions. Recommend clarification instead of inventing behavior. Avoid vague terms such as properly, correctly, normally, or as expected.
5. Preserve all evidence references exactly under Evidence, including screenshots, recordings, logs, URLs, filenames, error messages, and related ticket references. Request Missing Diagnostics when evidence is insufficient.
6. Discuss only Possible Investigation Areas or Potential Cause to Validate. Never state an unsupported root cause as confirmed.
${detailInstruction}
${suggestions.length ? suggestions.join('\n') : '- Severity and Priority were supplied. Preserve both values and do not replace them with suggestions.'}`;
  }

  private buildOutputContract(): string {
    return `# Final Response Contract
Return only a structured, developer-ready bug report in the requested target format. Include these core sections in this stable order:
1. Bug Title
2. Module / Feature
3. Environment
4. Application / Build Version
5. Preconditions
6. Test Data
7. Steps to Reproduce
8. Expected Result
9. Actual Result
10. Frequency / Reproducibility
11. Severity
12. Priority
13. Business / User Impact
14. Evidence

Use [NEEDS INVESTIGATION] for unresolved required values. Keep suggested classifications separate from confirmed facts, and do not add a root-cause claim unless explicit evidence supports it.`;
  }

  private buildFormatInstructions(targetFormat: BugPromptInput['targetFormat']): string {
    if (targetFormat === 'Jira') {
      return `# Target Format: Jira
Format the final report for a Jira issue description without calling Jira APIs. Use clear Jira-friendly labels such as Summary, Description, Environment, Steps to Reproduce, Expected Result, Actual Result, Frequency, Severity, Priority, Impact, Evidence, and the enabled optional sections. Keep the output easy to paste into Jira.`;
    }

    if (targetFormat === 'Azure DevOps') {
      return `# Target Format: Azure DevOps
Format the final report for an Azure DevOps work item without calling Azure DevOps APIs. Use clear labels such as Title, Description, Environment, Repro Steps, Expected Result, Actual Result, Reproducibility, Severity, Priority, Impact, Evidence, and the enabled optional sections. Keep the output easy to paste into a work item.`;
    }

    return `# Target Format: Generic Markdown
Use Markdown with the following core headings and preserve this order:
🐛 Bug Title
📦 Module / Feature
🌐 Environment
⚙️ Application / Build Version
🔧 Preconditions
🧪 Test Data
👣 Steps to Reproduce
✅ Expected Result
❌ Actual Result
🔁 Frequency / Reproducibility
🚨 Severity
📌 Priority
💥 Business / User Impact
📎 Evidence`;
  }

  private buildOptionalSections(input: BugPromptInput): string {
    const sections = [
      input.includeMissingInformation
        ? `### Missing Information
List unresolved values, ambiguous steps, contradictions, and Missing Diagnostics. Mark each unresolved item [NEEDS INVESTIGATION].`
        : '',
      input.includeAcceptanceCriteria
        ? `### Fix Acceptance Criteria
List observable conditions that prove the fix works. Base each condition on supplied facts and label any proposed clarification [Suggested].`
        : '',
      input.includeRetestChecklist
        ? `### Retest Checklist
Provide a concise checklist covering the original reproduction, expected and actual behavior, evidence, and relevant verification paths without inventing test data.`
        : '',
      input.includeRegressionScope
        ? `### Regression Scope
Identify affected areas to retest and explain why they are relevant. Use Potential Cause to Validate or Possible Investigation Areas rather than asserting a root cause.`
        : ''
    ].filter(Boolean);

    return `# Optional Output Sections
${sections.length ? sections.join('\n\n') : '- Omit all optional output sections.'}`;
  }

  private formatField(label: string, value: string): string {
    const trimmed = value.trim();
    if (!trimmed) return `- **${label}:** [NEEDS INVESTIGATION]`;

    const lines = trimmed.split(/\r?\n/);
    if (lines.length === 1) return `- **${label}:** ${trimmed}`;

    return `- **${label}:**\n${lines.map(line => `  ${line}`).join('\n')}`;
  }
}
