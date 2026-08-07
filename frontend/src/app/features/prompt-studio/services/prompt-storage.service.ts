import { Injectable } from '@angular/core';

export const PROMPT_STUDIO_DRAFT_KEYS = {
  bug: 'qa-support-hub.prompt-studio.bug-draft',
  story: 'qa-support-hub.prompt-studio.story-draft',
  testCase: 'qa-support-hub.prompt-studio.test-case-draft'
} as const;

export type PromptStudioDraftKind = keyof typeof PROMPT_STUDIO_DRAFT_KEYS;

/** Feature-local, namespaced draft persistence. localStorage failures are
 * treated as non-fatal so private-mode or policy-blocked storage never breaks
 * prompt generation. */
@Injectable({ providedIn: 'root' })
export class PromptStorageService {
  load<T>(kind: PromptStudioDraftKind): Partial<T> | null {
    try {
      const raw = localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS[kind]);
      return raw ? JSON.parse(raw) as Partial<T> : null;
    } catch {
      return null;
    }
  }

  save<T>(kind: PromptStudioDraftKind, draft: T): void {
    try {
      localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS[kind], JSON.stringify(draft));
    } catch {
      // Draft persistence is progressive enhancement, never a blocking path.
    }
  }

  clear(kind: PromptStudioDraftKind): void {
    try {
      localStorage.removeItem(PROMPT_STUDIO_DRAFT_KEYS[kind]);
    } catch {
      // Same non-fatal behavior as save/load.
    }
  }
}
