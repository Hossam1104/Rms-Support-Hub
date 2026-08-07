import { TestCasePromptInput } from '../models/test-case-prompt.model';

function formatTestCasePromptBlock(label: string, value: string): string {
  const trimmed = value.trim();
  if (!trimmed) return `- **${label}:** NULL [Generate & Suggest based on screenshots/evidence]`;
  if (!trimmed.includes('\n')) return `- **${label}:** ${trimmed}`;

  const formattedLines = trimmed
    .split('\n')
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => `  ${line}`)
    .join('\n');

  return `- **${label}:**\n${formattedLines}`;
}

export class TestCasePromptBuilder {
  build(input: TestCasePromptInput): string {
    const attachments = input.attachments.trim() || 'None specified (Refer to standard visual upload)';

    return `# Role
Act as a Principal Software Quality Assurance Engineer. Analyze the attached user interface screenshot(s) and any provided metadata to generate or refine a production-ready manual QA test case.

# Source of Truth & Context
- **Primary Source:** The attached screenshots/evidence represent the actual user interface and system state.
- **Secondary Source:** The explicit metadata inputs listed below represent user constraints. Any field listed as "NULL" represents missing/unprovided data that you MUST infer and generate.

# Metadata Input Configuration
${formatTestCasePromptBlock('Test Case ID', input.testCaseId)}
${formatTestCasePromptBlock('Scenario Type', input.scenarioType)}
${formatTestCasePromptBlock('Test Case Name', input.name)}
${formatTestCasePromptBlock('Targeted Table / Sheet Name', input.targetSection)}
${formatTestCasePromptBlock('Priority', input.priority)}
${formatTestCasePromptBlock('Preconditions', input.preconditions)}
${formatTestCasePromptBlock('Steps to Execute', input.steps)}
${formatTestCasePromptBlock('Expected Result', input.expectedResult)}
- **Attached Screenshot references:** ${attachments}

# Instructions for Processing NULL Fields
If any input field above is marked as "NULL [Generate & Suggest based on screenshots/evidence]", you MUST analyze the screen structure, state indicators, buttons, data fields, and headers to:
1. **Deduce missing fields:** Formulate professional, precise QA values for them.
2. **Suggest Test Case ID:** If missing, recommend a structured ID with standard prefixes matching the Scenario Type (e.g., \`TC-HP-###\` for Happy Path, \`TC-NEG-###\` for Negative, \`TC-EDGE-###\` for Edge/Boundary, \`TC-SEC-###\` for Security, \`TC-PERF-###\` for Performance, \`TC-ACC-###\` for Accessibility, \`TC-I18N-###\` for Localization, \`TC-DB-###\` for Data Integrity).
3. **Draft Steps & Expected:** If steps or expected results are missing, translate the visual flows, warning toasts, input validations, or dashboard grids shown in the screenshots into atomic, actionable numbered steps and specific expected states.
4. **Targeted Table / Sheet:** Deduce the logical module or spreadsheet catalog this test case belongs to (e.g., Auth, Cart, Payments, RecyclingRequests) based on the screen context.
5. **Differentiate Inferred Content:** Highlight all generated/suggested elements in your final output by prefixing them with "✨ [Suggested]".

# Output Constraints
- Output ONLY the formatted test case block inside a clean code box or markdown template.
- Do not include conversational remarks, metadata explanations, or pleasantries outside the requested template structure.
- Include a "💡 Suggestion Rationale" section at the very end explaining the reasoning behind your inferred values.

# Mandatory Output Format
Please format the final test case report using the exact layout below:

📋 **Test Case Details**
- 🆔 **ID:** [Value or ✨ [Suggested] Value]
- 🏷️ **Test Case Name:** [Value or ✨ [Suggested] Value]
- 🎯 **Scenario Type:** [Value or ✨ [Suggested] Value]
- ⚙️ **Preconditions:** [Value or ✨ [Suggested] Value]
- 📶 **Priority:** [Value or ✨ [Suggested] Value]
- 📊 **Targeted Table:** [Value or ✨ [Suggested] Value]

👣 **Steps to Execute:**
[Numbered list of execution steps, e.g.]
1. Action
2. Action

✅ **Expected Result:**
[Numbered expected outcomes, e.g.]
1. Expected result
2. Expected result

📎 **Attachments & References:**
- [List attached file/screenshot references]

---
💡 **Suggestion Rationale**
- [Briefly explain the rationale behind any suggested fields]`;
  }
}
