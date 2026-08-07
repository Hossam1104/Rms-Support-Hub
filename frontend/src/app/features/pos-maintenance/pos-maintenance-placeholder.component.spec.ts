import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';
import { PageHeaderComponent } from '../../shared/ui';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { routes } from '../../app.routes';
import { PosMaintenancePlaceholderComponent } from './pos-maintenance-placeholder.component';

@Component({
    standalone: true,
    selector: 'app-navbar',
    template: ''
})
class StubNavbarComponent { }

describe('PosMaintenancePlaceholderComponent', () => {
    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [PosMaintenancePlaceholderComponent],
            providers: [provideRouter([])]
        }).overrideComponent(PosMaintenancePlaceholderComponent, {
            set: {
                imports: [StubNavbarComponent, PageHeaderComponent, StatusBadgeComponent, RouterLink]
            }
        }).compileComponents();
    });

    afterEach(() => document.documentElement.removeAttribute('data-motion'));

    function render() {
        const fixture = TestBed.createComponent(PosMaintenancePlaceholderComponent);
        fixture.detectChanges();
        return fixture.nativeElement as HTMLElement;
    }

    it('keeps the POS route lazy and renders the Coming Soon identity', () => {
        const route = routes.find(candidate => candidate.path === 'tools/pos-maintenance');
        expect(route?.loadComponent).toBeTruthy();

        const page = render();
        expect(page.querySelectorAll('h1')).toHaveLength(1);
        expect(page.querySelector('h1')?.textContent?.trim()).toBe('POS Maintenance Tool');
        expect(page.querySelector('[role="status"]')?.textContent).toContain('Coming Soon');
        expect(page.querySelector('.status-panel [role="status"]')?.textContent).not.toContain('Available');
    });

    it('renders planned capabilities as non-interactive informational cards', () => {
        const page = render();
        const cards = Array.from(page.querySelectorAll('.capability-card')) as HTMLElement[];

        expect(cards).toHaveLength(5);
        expect(cards.map(card => card.querySelector('h3')?.textContent?.trim())).toEqual([
            'Diagnostics',
            'Backup & Restore',
            'Configuration',
            'Windows Services',
            'Environment / Connectivity'
        ]);
        expect(cards.every(card => card.querySelectorAll('button, a, input, select, textarea').length === 0)).toBe(true);
        expect(page.querySelectorAll('.capability-card app-status-badge')).toHaveLength(5);
        expect(page.textContent).toContain('Planned');
    });

    it('shows a simple informational status without presenting a blocker', () => {
        const page = render();
        const text = page.textContent || '';

        expect(text).toContain('Available now');
        expect(text).toContain('Information only');
        expect(text).toContain('Planned capability areas');
        expect(text).toContain('No POS operations or maintenance controls are available.');
        expect(text).not.toContain('Source required before migration');
        expect(text).not.toContain('Pending Source Review');
    });

    it('offers Hub navigation without operational or generic execution controls', () => {
        const page = render();
        const backLink = page.querySelector('.back-link') as HTMLAnchorElement | null;
        const text = page.textContent || '';

        expect(backLink?.getAttribute('href')).toBe('/');
        expect(backLink?.textContent).toContain('Back to QA Support Hub');
        expect(page.querySelectorAll('button')).toHaveLength(0);
        for (const forbiddenText of [
            'Run PowerShell',
            'Execute SQL',
            'Run Command',
            'Backup Now',
            'Restore Now',
            'Restart Service'
        ]) {
            expect(text).not.toContain(forbiddenText);
        }
    });

    it('keeps the page structurally accessible when reduced motion is selected', () => {
        document.documentElement.setAttribute('data-motion', 'reduce');
        const page = render();

        expect(page.querySelector('main[aria-label="POS Maintenance Tool"]')).toBeTruthy();
        expect(page.querySelector('main[aria-label="POS Maintenance Tool"] h1')).toBeTruthy();
        expect(page.querySelector('[role="status"]')).toBeTruthy();
        expect((page.querySelector('.back-link') as HTMLAnchorElement).getAttribute('href')).toBe('/');
    });
});
