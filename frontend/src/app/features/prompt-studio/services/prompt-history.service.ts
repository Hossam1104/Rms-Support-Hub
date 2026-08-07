import { Injectable, signal } from '@angular/core';

export const PROMPT_STUDIO_HISTORY_KEY = 'qa-support-hub.prompt-studio.history';
export const PROMPT_STUDIO_HISTORY_LIMIT = 10;

export type PromptHistoryType = 'Bug' | 'Story' | 'Test Case';

export interface PromptHistoryRecord {
    type: PromptHistoryType;
    title: string;
    timestamp: string;
    prompt: string;
}

type StoredRecord = Record<string, unknown>;

@Injectable({ providedIn: 'root' })
export class PromptHistoryService {
    private readonly recordState = signal<readonly PromptHistoryRecord[]>(this.read());
    readonly records = this.recordState.asReadonly();

    add(type: PromptHistoryType, title: string, prompt: string): void {
        const record: PromptHistoryRecord = {
            type,
            title: title.trim() || `Untitled ${type} Prompt`,
            timestamp: new Date().toISOString(),
            prompt
        };
        this.persist([record, ...this.records()].slice(0, PROMPT_STUDIO_HISTORY_LIMIT));
    }

    delete(record: PromptHistoryRecord): void {
        const index = this.records().findIndex(candidate => candidate === record || this.sameRecord(candidate, record));
        if (index < 0) return;
        this.persist([...this.records().slice(0, index), ...this.records().slice(index + 1)]);
    }

    clear(): void {
        this.persist([]);
    }

    private read(): readonly PromptHistoryRecord[] {
        try {
            if (typeof localStorage === 'undefined') return [];
            const raw = localStorage.getItem(PROMPT_STUDIO_HISTORY_KEY);
            if (!raw) return [];
            const parsed: unknown = JSON.parse(raw);
            if (!Array.isArray(parsed)) return [];
            return parsed
                .filter((value): value is StoredRecord => this.isValidRecord(value))
                .slice(0, PROMPT_STUDIO_HISTORY_LIMIT)
                .map(value => ({
                    type: value['type'] as PromptHistoryType,
                    title: value['title'] as string,
                    timestamp: value['timestamp'] as string,
                    prompt: value['prompt'] as string
                }));
        } catch {
            return [];
        }
    }

    private persist(records: readonly PromptHistoryRecord[]): void {
        const next = records.slice(0, PROMPT_STUDIO_HISTORY_LIMIT);
        this.recordState.set(next);
        try {
            if (typeof localStorage !== 'undefined') localStorage.setItem(PROMPT_STUDIO_HISTORY_KEY, JSON.stringify(next));
        } catch {
        }
    }

    private isValidRecord(value: unknown): value is StoredRecord {
        if (!this.isStoredRecord(value)) return false;
        return (value['type'] === 'Bug' || value['type'] === 'Story' || value['type'] === 'Test Case')
            && typeof value['title'] === 'string'
            && typeof value['timestamp'] === 'string'
            && typeof value['prompt'] === 'string';
    }

    private isStoredRecord(value: unknown): value is StoredRecord {
        return typeof value === 'object' && value !== null && !Array.isArray(value);
    }

    private sameRecord(left: PromptHistoryRecord, right: PromptHistoryRecord): boolean {
        return left.type === right.type
            && left.title === right.title
            && left.timestamp === right.timestamp
            && left.prompt === right.prompt;
    }
}
