import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EmptyStateComponent, ToolCardComponent } from '../../shared/ui';
import { HubComponent } from './hub.component';
import { QA_TOOL_REGISTRY } from './tool-registry';

@Component({
    standalone: true,
    selector: 'app-navbar',
    template: ''
})
class StubNavbarComponent { }

/** The real scene lazy-loads Three.js; the Hub contract is that it is purely
 * decorative, so the tests below run without it entirely. */
@Component({
    standalone: true,
    selector: 'app-hub-scene',
    template: ''
})
class StubHubSceneComponent { }

describe('HubComponent', () => {
    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [HubComponent],
            providers: [provideRouter([])]
        }).overrideComponent(HubComponent, {
            set: {
                imports: [StubNavbarComponent, ToolCardComponent, EmptyStateComponent, StubHubSceneComponent]
            }
        }).compileComponents();
    });

    it('renders exactly the three registry tools with authoritative status and routes', () => {
        const fixture = TestBed.createComponent(HubComponent);
        fixture.detectChanges();

        const cards = Array.from(fixture.nativeElement.querySelectorAll('app-tool-card')) as HTMLElement[];
        expect(cards).toHaveLength(3);
        expect(cards.map(card => card.querySelector('.tool-card__title')?.textContent?.trim())).toEqual([
            'QA Prompt Studio',
            'Online Order Tool',
            'POS Maintenance Tool'
        ]);
        expect(cards.map(card => card.querySelector('.tool-card__status')?.textContent?.trim())).toEqual([
            'Available',
            'Available',
            'Coming Soon'
        ]);
        expect(cards.map(card => card.querySelector('a')?.getAttribute('href'))).toEqual([
            '/tools/prompt-studio',
            '/tools/online-orders',
            '/tools/pos-maintenance'
        ]);
    });

    it('uses native links for keyboard-reachable card navigation', () => {
        const fixture = TestBed.createComponent(HubComponent);
        fixture.detectChanges();

        const links = Array.from(fixture.nativeElement.querySelectorAll('.tool-card__link')) as HTMLAnchorElement[];
        expect(links).toHaveLength(3);
        expect(links.every(link => link.getAttribute('role') === null && link.tabIndex === 0)).toBe(true);
        expect(links[0].getAttribute('aria-label')).toContain('Open Prompt Studio: QA Prompt Studio');
        expect(links[0].getAttribute('aria-label')).toContain('Available');
    });

    it('keeps navigation available when reduced motion is selected', () => {
        document.documentElement.setAttribute('data-motion', 'reduce');
        const fixture = TestBed.createComponent(HubComponent);
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.tool-card__link')?.getAttribute('href')).toBe('/tools/prompt-studio');
    });

    it('renders the shared empty state when the visible registry is empty', () => {
        const fixture = TestBed.createComponent(HubComponent);
        fixture.componentInstance.tools = [];
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelectorAll('app-tool-card')).toHaveLength(0);
        expect(fixture.nativeElement.querySelector('.empty-state h3')?.textContent).toContain('No QA tools are currently configured.');
    });

    it('renders from the registry collection instead of a fixed card list', () => {
        const fixture = TestBed.createComponent(HubComponent);
        fixture.componentInstance.tools = [QA_TOOL_REGISTRY[1]];
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelectorAll('app-tool-card')).toHaveLength(1);
        expect(fixture.nativeElement.querySelector('.tool-card__title')?.textContent).toContain('Online Order Tool');
    });

    it('keeps one H1 and reachable tool cards when the decorative scene is absent', () => {
        const fixture = TestBed.createComponent(HubComponent);
        fixture.detectChanges();

        const page = fixture.nativeElement as HTMLElement;
        expect(page.querySelectorAll('h1')).toHaveLength(1);
        expect(page.querySelector('h1')?.textContent).toContain('QA Support Hub');
        // The stub renders nothing, proving navigation never depends on WebGL.
        expect(page.querySelector('canvas')).toBeNull();
        expect(page.querySelectorAll('.tool-card__link')).toHaveLength(3);
    });

    it('summarizes registry availability in the hero without duplicating card status', () => {
        const fixture = TestBed.createComponent(HubComponent);
        fixture.detectChanges();

        const signals = Array.from(fixture.nativeElement.querySelectorAll('.hub-signal')) as HTMLElement[];
        expect(signals.map(signal => signal.querySelector('.hub-signal__state')?.textContent?.trim()))
            .toEqual(['Available', 'Available', 'Coming Soon']);
    });
});