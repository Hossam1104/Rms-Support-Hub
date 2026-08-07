import { sanitizeDownloadFilename } from './download-filename.util';

describe('sanitizeDownloadFilename', () => {
    it('flattens path-like and illegal filename input', () => {
        const filename = sanitizeDownloadFilename('../reports/bug: report?', 'prompt');

        expect(filename).toBe('reports-bug-report');
        expect(filename).not.toMatch(/[<>:"/\\|?*\u0000-\u001F\u007F]/);
    });

    it('handles empty and reserved names with safe fallbacks', () => {
        expect(sanitizeDownloadFilename('..', 'prompt')).toBe('prompt');
        expect(sanitizeDownloadFilename('CON', 'prompt')).toBe('download-CON');
    });

    it('bounds the generated base name', () => {
        expect(sanitizeDownloadFilename('a'.repeat(200), 'prompt')).toHaveLength(80);
    });
});