import { TestBed } from '@angular/core/testing';
import { JsonTreeComponent } from './json-tree.component';
import { JsonTreeNodeComponent } from './json-tree-node.component';

describe('JSON tree controls', () => {
    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [JsonTreeComponent, JsonTreeNodeComponent]
        }).compileComponents();
    });

    it('renders user data as text and exposes keyboard controls', () => {
        const fixture = TestBed.createComponent(JsonTreeNodeComponent);
        fixture.componentRef.setInput('value', { '<script>': '<img onerror=x>' });
        fixture.componentRef.setInput('searchTerm', 'script');
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('img')).toBeNull();
        expect(fixture.nativeElement.textContent).toContain('<img onerror=x>');
        expect(fixture.nativeElement.querySelector('mark')?.textContent).toBe('script');

        const toggle = fixture.nativeElement.querySelector('.toggle-button') as HTMLButtonElement;
        const key = fixture.nativeElement.querySelector('.node-key') as HTMLButtonElement;
        expect(toggle.type).toBe('button');
        expect(toggle.getAttribute('aria-expanded')).toBe('true');
        expect(key.type).toBe('button');
    });

    it('names the JSON search and download controls', () => {
        const fixture = TestBed.createComponent(JsonTreeComponent);
        fixture.componentRef.setInput('title', 'Order Request');
        fixture.componentRef.setInput('data', { status: 'ready' });
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('input[aria-label="Search JSON keys and values"]')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('button[aria-label="Download JSON data"]')).toBeTruthy();
    });
});