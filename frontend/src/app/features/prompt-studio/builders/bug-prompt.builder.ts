import { BugPromptInput } from '../models/bug-prompt.model';

function formatBugPromptBlock(label: string, value: string, fallback: string): string {
  const trimmed = value.trim();
  if (!trimmed) return `- **${label}:** ${fallback}`;
  if (!trimmed.includes('\n')) return `- **${label}:** ${trimmed}`;

  const formattedLines = trimmed
    .split('\n')
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => `  ${line}`)
    .join('\n');

  return `- **${label}:**\n${formattedLines}`;
}

export class BugPromptBuilder {
  build(input: BugPromptInput): string {
    const attachments = input.attachments.trim() || 'None provided';

    return `# Role
Act as a Senior QA Engineer. Transform the supplied raw bug notes into a concise, developer-ready bug report while preserving every confirmed fact the reporter already provided.

# Source of Truth
The user inputs below are the source of truth. Do not overwrite or contradict any provided title, preconditions, steps, expected result, actual result, or attachment references. Only clarify wording, ordering, and missing structure.

# Input Data
${formatBugPromptBlock('Bug Title', input.title, '{{INSERT_BUG_TITLE}}')}
${formatBugPromptBlock('Preconditions', input.preconditions, '{{INSERT_PRECONDITIONS_OR_LEAVE_BLANK}}')}
${formatBugPromptBlock('Steps to Reproduce', input.steps, '{{INSERT_STEPS_TO_REPRODUCE_OR_LEAVE_BLANK}}')}
${formatBugPromptBlock('Expected Result', input.expectedResult, '{{INSERT_EXPECTED_RESULT_OR_LEAVE_BLANK}}')}
${formatBugPromptBlock('Actual Result', input.actualResult, '{{INSERT_ACTUAL_RESULT_OR_LEAVE_BLANK}}')}
- **Attachments:** ${attachments}

# Instructions
1. Preserve every provided fact and field meaning exactly as supplied.
2. Rewrite the content into cleaner QA language only where clarity improves.
3. Normalize the steps into an atomic numbered list.
4. Preserve any referenced evidence such as screenshots, videos, logs, links, or file names under a dedicated Attachments section.
5. If information is missing and cannot be derived safely, write "[NEEDS INVESTIGATION]".
6. Make the final report short, precise, and actionable for developers and testers.

# Output Constraints
- Output ONLY the six sections below.
- Do not include commentary, rationale, markdown tables, or code fences.
- Do not invent environments, accounts, data, or attachments unless they are explicitly mentioned.

# Required Format
IMPORTANT: Please use the exact emoji/icon headings below so the final bug report is visually clear and easy to copy and paste.
🐛 Bug Title: [Concise summary of the issue]
⚙️ Preconditions: [List of setup requirements]
👣 Steps to Reproduce: [Numbered list]
✅ Expected Result: [Clear statement]
❌ Actual Result: [Clear statement]
📎 Attachments: [List screenshots, videos, logs, URLs, file names, or "None provided"]`;
  }
}
