import { TestBed } from '@angular/core/testing';
import { PromptPreviewComponent } from './prompt-preview.component';

describe('PromptPreviewComponent', () => {
    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [PromptPreviewComponent]
        }).compileComponents();
    });

    it('shows copy, Markdown, and plain-text actions for generated output', () => {
        const fixture = TestBed.createComponent(PromptPreviewComponent);
        fixture.componentRef.setInput('prompt', '# Generated prompt');
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Copy generated prompt"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as Markdown"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('ui-button[ariaLabel="Download generated prompt as plain text"]')).toBeTruthy();
    });

    it('downloads the selected extension with the matching MIME type', () => {
        const fixture = TestBed.createComponent(PromptPreviewComponent);
        fixture.componentRef.setInput('prompt', 'Plain text output');
        fixture.componentRef.setInput('filename', 'bug-prompt');
        fixture.detectChanges();

        const createObjectUrl = vi.fn((blob: Blob) => {
            void blob;
            return 'blob:prompt';
        });
        const revokeObjectUrl = vi.fn();
        const originalCreateObjectUrl = URL.createObjectURL;
        const originalRevokeObjectUrl = URL.revokeObjectURL;
        const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
        Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createObjectUrl });
        Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revokeObjectUrl });

        try {
            fixture.componentInstance.download('txt');

            expect(createObjectUrl).toHaveBeenCalledOnce();
            expect((createObjectUrl.mock.calls[0][0] as Blob).type).toBe('text/plain;charset=utf-8');
            expect(revokeObjectUrl).toHaveBeenCalledWith('blob:prompt');
            expect(click).toHaveBeenCalledOnce();
        } finally {
            Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: originalCreateObjectUrl });
            Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: originalRevokeObjectUrl });
            click.mockRestore();
        }
    });
});
