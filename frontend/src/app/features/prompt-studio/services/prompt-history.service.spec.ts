import { TestBed } from '@angular/core/testing';
import {
    PROMPT_STUDIO_HISTORY_KEY,
    PROMPT_STUDIO_HISTORY_LIMIT,
    PromptHistoryService
} from './prompt-history.service';

describe('PromptHistoryService', () => {
    beforeEach(() => {
        localStorage.clear();
        TestBed.configureTestingModule({});
    });

    it('stores newest records first and removes records beyond the limit', () => {
        const service = TestBed.inject(PromptHistoryService);

        for (let index = 0; index < PROMPT_STUDIO_HISTORY_LIMIT + 2; index += 1) {
            service.add('Bug', `Prompt ${index}`, `Generated prompt ${index}`);
        }

        expect(service.records()).toHaveLength(PROMPT_STUDIO_HISTORY_LIMIT);
        expect(service.records()[0].title).toBe('Prompt 11');
        expect(service.records().at(-1)?.title).toBe('Prompt 2');
        expect(JSON.parse(localStorage.getItem(PROMPT_STUDIO_HISTORY_KEY) as string)).toEqual(service.records());
        expect(Object.keys(JSON.parse(localStorage.getItem(PROMPT_STUDIO_HISTORY_KEY) as string)[0]).sort()).toEqual([
            'prompt',
            'timestamp',
            'title',
            'type'
        ]);
    });

    it('deletes one record and clears all records', () => {
        const service = TestBed.inject(PromptHistoryService);
        service.add('Story', 'First', 'Prompt one');
        service.add('Test Case', 'Second', 'Prompt two');

        const first = service.records()[0];
        service.delete(first);

        expect(service.records()).toHaveLength(1);
        expect(service.records()[0].title).toBe('First');

        service.clear();

        expect(service.records()).toEqual([]);
        expect(JSON.parse(localStorage.getItem(PROMPT_STUDIO_HISTORY_KEY) as string)).toEqual([]);
    });

    it('restores only valid records and ignores malformed or extra data', () => {
        localStorage.setItem(PROMPT_STUDIO_HISTORY_KEY, JSON.stringify([
            { type: 'Story', title: 'Valid', timestamp: '2026-08-07T00:00:00.000Z', prompt: 'Prompt', attachments: 'private.png' },
            { type: 'Unknown', title: 'Invalid', timestamp: 'now', prompt: 'Prompt' },
            { type: 'Bug', title: 'Missing prompt', timestamp: 'now' }
        ]));

        const service = TestBed.inject(PromptHistoryService);

        expect(service.records()).toEqual([{
            type: 'Story',
            title: 'Valid',
            timestamp: '2026-08-07T00:00:00.000Z',
            prompt: 'Prompt'
        }]);
    });

    it('handles invalid JSON and unavailable storage without throwing', () => {
        localStorage.setItem(PROMPT_STUDIO_HISTORY_KEY, '{invalid');
        expect(TestBed.inject(PromptHistoryService).records()).toEqual([]);

        const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('Blocked'); });
        const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Blocked'); });
        try {
            TestBed.resetTestingModule();
            TestBed.configureTestingModule({});
            const service = TestBed.inject(PromptHistoryService);

            expect(() => service.add('Bug', 'Unavailable', 'Prompt')).not.toThrow();
            expect(service.records()).toHaveLength(1);
            expect(() => service.clear()).not.toThrow();
            expect(service.records()).toEqual([]);
        } finally {
            getItem.mockRestore();
            setItem.mockRestore();
        }
    });
});