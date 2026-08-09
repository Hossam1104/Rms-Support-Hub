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

    // Deep enough that root(0)/level1(1)/level2(2) auto-expand but the
    // `items` array (3) and everything below it start collapsed.
    const deepData = {
        level1: {
            level2: {
                items: [
                    { level3: { secretValue: 'hidden-gem' } }
                ]
            }
        }
    };

    function findButtonByText(root: HTMLElement, selector: string, text: string): HTMLButtonElement {
        const match = Array.from(root.querySelectorAll<HTMLButtonElement>(selector))
            .find(el => el.textContent?.trim().includes(text));
        if (!match) throw new Error(`No ${selector} found containing "${text}"`);
        return match;
    }

    function search(fixture: ReturnType<typeof TestBed.createComponent<JsonTreeComponent>>, term: string) {
        const input = fixture.nativeElement.querySelector('.search-input') as HTMLInputElement;
        input.value = term;
        input.dispatchEvent(new Event('input'));
        fixture.detectChanges();
    }

    it('auto-expands the root and the first two useful levels, leaving deeper containers collapsed', () => {
        const fixture = TestBed.createComponent(JsonTreeComponent);
        fixture.componentRef.setInput('title', 'Deep');
        fixture.componentRef.setInput('data', deepData);
        fixture.detectChanges();

        const text = fixture.nativeElement.textContent as string;
        expect(text).toContain('level2');
        expect(text).toContain('items');
        expect(text).not.toContain('secretValue');
        expect(text).not.toContain('hidden-gem');
    });

    it('opens a nested collapsed array via its own toggle', () => {
        const fixture = TestBed.createComponent(JsonTreeComponent);
        fixture.componentRef.setInput('title', 'Deep');
        fixture.componentRef.setInput('data', deepData);
        fixture.detectChanges();

        const itemsToggle = findButtonByText(fixture.nativeElement, '.node-key', 'items')
            .closest('.node-row')!.querySelector('.toggle-button') as HTMLButtonElement;
        itemsToggle.click();
        fixture.detectChanges();

        // items (depth 3) is now open, revealing its array index "0" -- that
        // child object is itself default-collapsed (depth 4), so a second
        // toggle is needed to reach level3/secretValue, matching how a real
        // user would drill down past the auto-expand boundary.
        expect((fixture.nativeElement.textContent as string)).not.toContain('secretValue');
        const indexToggle = findButtonByText(fixture.nativeElement, '.node-key', '0')
            .closest('.node-row')!.querySelector('.toggle-button') as HTMLButtonElement;
        indexToggle.click();
        fixture.detectChanges();

        expect((fixture.nativeElement.textContent as string)).toContain('level3');
    });

    it('Expand all reveals every currently rendered container, Collapse all hides even the default-open ones', () => {
        const fixture = TestBed.createComponent(JsonTreeComponent);
        const component = fixture.componentInstance;
        fixture.componentRef.setInput('title', 'Deep');
        fixture.componentRef.setInput('data', deepData);
        fixture.detectChanges();

        component.expandAll();
        fixture.detectChanges();
        expect((fixture.nativeElement.textContent as string)).toContain('hidden-gem');

        component.collapseAll();
        fixture.detectChanges();
        const collapsedText = fixture.nativeElement.textContent as string;
        expect(collapsedText).not.toContain('level2');
        expect(collapsedText).not.toContain('hidden-gem');
    });

    it('search reveals a matching branch even after the user manually collapsed it', () => {
        const fixture = TestBed.createComponent(JsonTreeComponent);
        fixture.componentRef.setInput('title', 'Deep');
        fixture.componentRef.setInput('data', deepData);
        fixture.detectChanges();

        const itemsRow = () => findButtonByText(fixture.nativeElement, '.node-key', 'items').closest('.node-row')!;
        const itemsToggle = () => itemsRow().querySelector('.toggle-button') as HTMLButtonElement;

        // Open, then explicitly re-collapse `items` so its expandedState is
        // manually pinned to false before searching.
        itemsToggle().click();
        fixture.detectChanges();
        itemsToggle().click();
        fixture.detectChanges();
        expect((fixture.nativeElement.textContent as string)).not.toContain('level3');

        search(fixture, 'hidden-gem');

        expect((fixture.nativeElement.textContent as string)).toContain('hidden-gem');
    });

    it('keeps the raw/tree view switch functional', () => {
        const fixture = TestBed.createComponent(JsonTreeComponent);
        fixture.componentRef.setInput('title', 'Deep');
        fixture.componentRef.setInput('data', { status: 'ready' });
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.tree-view')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('.raw-view')).toBeNull();

        const rawToggle = findButtonByText(fixture.nativeElement, '.toolbar-btn', 'Raw view');
        rawToggle.click();
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.raw-view')).toBeTruthy();
        expect(fixture.nativeElement.querySelector('.tree-view')).toBeNull();
        expect(fixture.nativeElement.textContent).toContain('"status": "ready"');

        const treeToggle = findButtonByText(fixture.nativeElement, '.toolbar-btn', 'Tree view');
        treeToggle.click();
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.tree-view')).toBeTruthy();
    });
});
