import { Injectable } from '@angular/core';
import { BugPromptInput } from '../models/bug-prompt.model';
import { StoryPromptInput } from '../models/story-prompt.model';
import { TestCasePromptInput } from '../models/test-case-prompt.model';

export type PromptQualityType = 'bug' | 'story' | 'testCase';
export type PromptQualitySeverity = 'warning' | 'info';
export type PromptQualityInput = BugPromptInput | StoryPromptInput | TestCasePromptInput;

export interface PromptQualityFinding {
    severity: PromptQualitySeverity;
    field: string;
    message: string;
    recommendation: string;
}

export interface PromptQualityResult {
    score: number;
    findings: readonly PromptQualityFinding[];
    missingFields: readonly string[];
    facts: readonly string[];
    assumptions: readonly string[];
    allowedInference: readonly string[];
    sensitiveContent: boolean;
}

interface QualityField {
    label: string;
    value: string;
}

const VAGUE_LANGUAGE = /\b(?:properly|correctly|appropriately|normally|as expected|something|some data|valid data|invalid data|the thing|do the action|perform action|check it|verify it|works|handled)\b/i;
const OBSERVABLE_LANGUAGE = /\b(?:display|displayed|show|shown|visible|contains|equals|matches|remains|stays|redirect|navigat|enabled|disabled|created|updated|deleted|saved|returned|listed|recorded|logged|status|message|error|warning|receipt|download|focus|selected|cleared|blocked|rejected|approved|synchronized|printed|appears|loads)\w*\b/i;
const SENSITIVE_LANGUAGE = /\b(?:password|passcode|secret|api[_ -]?key|access[_ -]?token|bearer\s+token|private\s+key|connection\s+string|client\s+secret)\b/i;
const EVIDENCE_LANGUAGE = /\b(?:screenshot|attachment|recording|log(?:s)?|evidence|reference)\b/i;
const WEAK_TITLE = /^(?:bug|issue|problem|test|test case|scenario|story|new feature|enhancement|filter test|something|status issue)$/i;
const OPPOSITE_PAIRS = [
    ['visible', 'not visible'],
    ['displayed', 'not displayed'],
    ['saved', 'not saved'],
    ['enabled', 'disabled'],
    ['selected', 'cleared'],
    ['approved', 'rejected'],
    ['loaded', 'failed'],
    ['present', 'missing']
] as const;

@Injectable({ providedIn: 'root' })
export class PromptQualityService {
    analyze(type: 'bug', input: BugPromptInput): PromptQualityResult;
    analyze(type: 'story', input: StoryPromptInput): PromptQualityResult;
    analyze(type: 'testCase', input: TestCasePromptInput): PromptQualityResult;
    analyze(type: PromptQualityType, input: PromptQualityInput): PromptQualityResult {
        if (type === 'bug') return this.analyzeBug(input as BugPromptInput);
        if (type === 'story') return this.analyzeStory(input as StoryPromptInput);
        return this.analyzeTestCase(input as TestCasePromptInput);
    }

    private analyzeBug(input: BugPromptInput): PromptQualityResult {
        const fields: QualityField[] = [
            { label: 'Raw Bug Notes', value: input.rawNotes },
            { label: 'Bug Title', value: input.title },
            { label: 'Module / Feature', value: input.module },
            { label: 'Environment', value: input.environment },
            { label: 'Application / Build Version', value: input.buildVersion },
            { label: 'Preconditions', value: input.preconditions },
            { label: 'Steps to Reproduce', value: input.steps },
            { label: 'Expected Result', value: input.expectedResult },
            { label: 'Actual Result', value: input.actualResult },
            { label: 'Severity', value: input.severity },
            { label: 'Priority', value: input.priority },
            { label: 'Evidence/Attachments', value: input.attachments }
        ];
        const findings: PromptQualityFinding[] = [];

        this.addMissingFindings(fields, findings);
        this.addWeakTitle(input.title, findings, 'Bug Title', 'Make the title specific to the affected feature, behavior, and trigger.');
        this.addVagueFinding(input.steps, findings, 'Steps to Reproduce', 'Replace vague wording with one atomic action and its target control or state.');
        this.addVagueFinding(input.expectedResult, findings, 'Expected Result', 'State the visible, measurable, or recorded outcome.');
        this.addVagueFinding(input.actualResult, findings, 'Actual Result', 'Describe the observed failure in specific, observable terms.');
        this.addDuplicateStepFinding(input.steps, findings, 'Steps to Reproduce');
        this.addEqualityFinding(input.expectedResult, input.actualResult, findings);
        this.addContradictionFinding(input.expectedResult, input.actualResult, findings, 'Expected Result / Actual Result');
        this.addSeverityPriorityFinding(input.severity, input.priority, findings);
        this.addEvidenceFinding([input.rawNotes, input.steps, input.expectedResult, input.actualResult], input.attachments, findings);
        this.addSensitiveFinding(fields, findings);

        return this.createResult(fields, findings, [
            'Refine wording and step order',
            'Suggest simple severity or priority values only when they are absent'
        ]);
    }

    private analyzeStory(input: StoryPromptInput): PromptQualityResult {
        const fields: QualityField[] = [
            { label: 'Raw Story / Requirement', value: input.rawStory },
            { label: 'Story Title', value: input.title },
            { label: 'User / Role', value: input.actor },
            { label: 'Business Goal', value: input.businessGoal },
            { label: 'Requirement / Description', value: input.requirement },
            { label: 'Business Rules', value: input.businessRules },
            { label: 'Evidence / References', value: input.evidenceReferences }
        ];
        const findings: PromptQualityFinding[] = [];

        this.addMissingFindings(fields, findings);
        this.addWeakTitle(input.title, findings, 'Story Title', 'Describe the capability rather than a generic enhancement.');
        if (input.requirement.trim() && (this.hasVagueLanguage(input.requirement) || !OBSERVABLE_LANGUAGE.test(input.requirement))) {
            findings.push(this.finding('warning', 'Requirement / Description', 'The requirement is difficult to test as written.', 'Name the actor action and observable outcome when those facts are known.'));
        }
        if (!input.businessRules.trim()) {
            findings.push(this.finding('info', 'Business Rules', 'No confirmed business rule was supplied.', 'Keep this field as [NEEDS CLARIFICATION] until a rule is confirmed.'));
        }
        this.addAmbiguousActorFinding(input.actor, findings);
        this.addEvidenceFinding([input.rawStory, input.requirement], input.evidenceReferences, findings);
        this.addContradictionFinding(input.rawStory, input.requirement, findings, 'Raw Story / Requirement');
        this.addSensitiveFinding(fields, findings);

        return this.createResult(fields, findings, [
            'Refine wording and acceptance criteria structure',
            'Use [NEEDS CLARIFICATION] for unsupported acceptance outcomes'
        ]);
    }

    private analyzeTestCase(input: TestCasePromptInput): PromptQualityResult {
        const fields: QualityField[] = [
            { label: 'Test Case Title', value: input.title },
            { label: 'Module / Feature', value: input.module },
            { label: 'Scenario / Objective', value: input.scenario },
            { label: 'Preconditions', value: input.preconditions },
            { label: 'Test Data', value: input.testData },
            { label: 'Steps', value: input.steps },
            { label: 'Expected Result', value: input.expectedResult },
            { label: 'Priority', value: input.priority },
            { label: 'Evidence / Attachments', value: input.attachments }
        ];
        const findings: PromptQualityFinding[] = [];

        this.addMissingFindings(fields, findings);
        this.addWeakTitle(input.title, findings, 'Test Case Title', 'State the action or condition and the expected behavior.');
        this.addVagueFinding(input.steps, findings, 'Steps', 'Replace vague wording with one atomic action and its target control or state.');
        this.addVagueFinding(input.expectedResult, findings, 'Expected Result', 'State the visible, measurable, or recorded outcome.');
        this.addDuplicateStepFinding(input.steps, findings, 'Steps');
        this.addNoObservableFinding(input.expectedResult, findings);
        this.addEvidenceFinding([input.scenario, input.steps, input.expectedResult], input.attachments, findings);
        this.addSensitiveFinding(fields, findings);

        return this.createResult(fields, findings, [
            'Normalize step order and atomic actions',
            'Mark evidence-supported details [Inferred]'
        ]);
    }

    private createResult(fields: readonly QualityField[], findings: PromptQualityFinding[], allowedInference: string[]): PromptQualityResult {
        const missingFields = fields.filter(field => !field.value.trim()).map(field => field.label);
        const facts = fields.filter(field => field.value.trim()).map(field => field.label);
        const assumptions = fields
            .filter(field => /\b(?:assume|assumption|maybe|perhaps)\b/i.test(field.value))
            .map(field => field.label);
        const contentFields = fields.filter(field => !['Severity', 'Priority'].includes(field.label));
        const completeness = contentFields.length === 0
            ? 0
            : contentFields.filter(field => field.value.trim()).length / contentFields.length;
        const vagueFindings = findings.filter(finding => /vague|difficult to test|weak/i.test(finding.message)).length;
        const consistencyFindings = findings.filter(finding => /same|contradict|duplicate/i.test(finding.message)).length;
        const evidenceProvided = fields.some(field => /Evidence/.test(field.label) && field.value.trim());
        const score = facts.length === 0
            ? 0
            : Math.max(0, Math.min(100, Math.round(
                completeness * 55
                + Math.max(0, 20 - vagueFindings * 5)
                + Math.max(0, 15 - consistencyFindings * 7)
                + (evidenceProvided ? 10 : 0)
            )));

        return {
            score,
            findings,
            missingFields,
            facts,
            assumptions,
            allowedInference,
            sensitiveContent: findings.some(finding => finding.field === 'Source information')
        };
    }

    private addMissingFindings(fields: readonly QualityField[], findings: PromptQualityFinding[]): void {
        for (const field of fields) {
            if (!field.value.trim()) {
                findings.push(this.finding('warning', field.label, `${field.label} is missing.`, `Supply the confirmed value or keep [NEEDS INVESTIGATION] in this field.`));
            }
        }
    }

    private addWeakTitle(value: string, findings: PromptQualityFinding[], field: string, recommendation: string): void {
        const normalized = value.trim();
        if (normalized && (normalized.length < 8 || WEAK_TITLE.test(normalized))) {
            findings.push(this.finding('warning', field, 'The title is too vague to identify the behavior.', recommendation));
        }
    }

    private addVagueFinding(value: string, findings: PromptQualityFinding[], field: string, recommendation: string): void {
        if (value.trim() && this.hasVagueLanguage(value)) {
            findings.push(this.finding('warning', field, 'Vague wording was detected.', recommendation));
        }
    }

    private addNoObservableFinding(value: string, findings: PromptQualityFinding[]): void {
        if (value.trim() && !OBSERVABLE_LANGUAGE.test(value)) {
            findings.push(this.finding('warning', 'Expected Result', 'The expected result has no obvious observable outcome.', 'Name a visible value, message, state, record, or navigation result.'));
        }
    }

    private addDuplicateStepFinding(value: string, findings: PromptQualityFinding[], field: string): void {
        const steps = this.splitSteps(value).map(step => this.normalizeText(step));
        const duplicates = steps.filter((step, index) => step && steps.indexOf(step) !== index);
        if (duplicates.length) {
            findings.push(this.finding('warning', field, 'Duplicate execution steps were detected.', 'Keep each step atomic and remove repeated actions.'));
        }
    }

    private addSeverityPriorityFinding(severity: string, priority: string, findings: PromptQualityFinding[]): void {
        const normalizedSeverity = severity.trim().toLowerCase();
        const normalizedPriority = priority.trim().toUpperCase();
        const severe = /^(?:blocker|critical|high|urgent)$/.test(normalizedSeverity);
        const minor = /^(?:low|minor|trivial)$/.test(normalizedSeverity);
        const lowPriority = /^(?:P2|P3)$/.test(normalizedPriority);
        const highPriority = /^(?:P0|P1)$/.test(normalizedPriority);

        if ((severe && lowPriority) || (minor && highPriority)) {
            findings.push(this.finding('info', 'Severity / Priority', 'Severity and priority may describe different urgency levels.', 'Confirm the relationship using the supplied triage policy; do not overwrite either value.'));
        }
    }

    private addAmbiguousActorFinding(actor: string, findings: PromptQualityFinding[]): void {
        if (/^(?:user|someone|they|team|customer|end user)$/i.test(actor.trim())) {
            findings.push(this.finding('warning', 'User / Role', 'The actor is too broad to define ownership or permissions.', 'Name the role, persona, or permission level supported by the source.'));
        }
    }

    private addEqualityFinding(expected: string, actual: string, findings: PromptQualityFinding[]): void {
        if (expected.trim() && actual.trim() && this.normalizeText(expected) === this.normalizeText(actual)) {
            findings.push(this.finding('warning', 'Expected Result / Actual Result', 'Expected and Actual results are identical.', 'Describe the observed failure separately from the intended outcome.'));
        }
    }

    private addContradictionFinding(left: string, right: string, findings: PromptQualityFinding[], field: string): void {
        const normalizedLeft = this.normalizeText(left);
        const normalizedRight = this.normalizeText(right);
        const contradictory = OPPOSITE_PAIRS.some(([positive, negative]) =>
            (normalizedLeft.includes(positive) && normalizedRight.includes(negative))
            || (normalizedRight.includes(positive) && normalizedLeft.includes(negative))
        );
        if (contradictory) {
            findings.push(this.finding('warning', field, 'Potentially contradictory outcomes were detected.', 'Confirm the intended behavior using only supplied facts.'));
        }
    }

    private addEvidenceFinding(sourceValues: readonly string[], evidence: string, findings: PromptQualityFinding[]): void {
        if (!evidence.trim() && sourceValues.some(value => EVIDENCE_LANGUAGE.test(value))) {
            findings.push(this.finding('warning', 'Evidence / Attachments', 'The source mentions evidence but no reference is supplied.', 'Add a filename, link, or concise reference without storing file contents.'));
        }
    }

    private addSensitiveFinding(fields: readonly QualityField[], findings: PromptQualityFinding[]): void {
        if (fields.some(field => SENSITIVE_LANGUAGE.test(field.value))) {
            findings.push(this.finding('warning', 'Source information', 'Potentially sensitive content was detected.', 'Remove credentials, tokens, secrets, and private file contents before sharing the prompt.'));
        }
    }

    private finding(severity: PromptQualitySeverity, field: string, message: string, recommendation: string): PromptQualityFinding {
        return { severity, field, message, recommendation };
    }

    private hasVagueLanguage(value: string): boolean {
        return VAGUE_LANGUAGE.test(value);
    }

    private splitSteps(value: string): string[] {
        return value
            .split(/\r?\n/)
            .map(line => line.replace(/^\s*(?:\d+[.)]|[-*])\s*/, '').trim())
            .filter(Boolean);
    }

    private normalizeText(value: string): string {
        return value.toLowerCase().replace(/[^a-z0-9\s]/g, '').replace(/\s+/g, ' ').trim();
    }
}
