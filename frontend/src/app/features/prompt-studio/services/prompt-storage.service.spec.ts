import { TestBed } from '@angular/core/testing';
import { PROMPT_STUDIO_DRAFT_KEYS, PromptStorageService } from './prompt-storage.service';

describe('PromptStorageService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('saves, restores, and clears drafts under namespaced keys', () => {
    const service = TestBed.inject(PromptStorageService);
    const draft = { title: 'Saved bug' };

    service.save('bug', draft);

    expect(localStorage.getItem(PROMPT_STUDIO_DRAFT_KEYS.bug)).toBe(JSON.stringify(draft));
    expect(service.load<typeof draft>('bug')).toEqual(draft);

    service.clear('bug');
    expect(service.load('bug')).toBeNull();
  });

  it('returns null instead of throwing for invalid stored JSON', () => {
    localStorage.setItem(PROMPT_STUDIO_DRAFT_KEYS.story, '{invalid');

    expect(TestBed.inject(PromptStorageService).load('story')).toBeNull();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('does not crash when storage access is unavailable', () => {
    const service = TestBed.inject(PromptStorageService);
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('Blocked'); });
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Blocked'); });
    const removeItem = vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => { throw new Error('Blocked'); });

    try {
      expect(() => {
        service.save('testCase', { name: 'Draft' });
        expect(service.load('testCase')).toBeNull();
        service.clear('testCase');
      }).not.toThrow();
    } finally {
      getItem.mockRestore();
      setItem.mockRestore();
      removeItem.mockRestore();
    }
  });
});
