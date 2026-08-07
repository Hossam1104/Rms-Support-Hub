import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SidebarStateService } from '../../core/services/sidebar-state.service';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
    beforeEach(async () => {
        localStorage.removeItem('order-tool.sidebar-collapsed');
        await TestBed.configureTestingModule({
            imports: [SidebarComponent],
            providers: [provideRouter([])]
        }).compileComponents();
    });

    it('keeps module navigation and exposes routes back to modules and the Hub', () => {
        const fixture = TestBed.createComponent(SidebarComponent);
        const component = fixture.componentInstance;
        component.moduleKey = 'upc_ecommerce';
        component.moduleLabel = 'UPC E-commerce';
        component.clientName = 'UPC';
        TestBed.inject(SidebarStateService).setCollapsed(false);
        fixture.detectChanges();

        const element = fixture.nativeElement as HTMLElement;
        expect(element.querySelector('nav')?.getAttribute('aria-label')).toBe('Online Order module navigation');
        expect(Array.from(element.querySelectorAll('.nav-label')).map(label => label.textContent?.trim())).toEqual([
            'Order Builder',
            'Order Requests'
        ]);

        const footerLinks = Array.from(element.querySelectorAll('.sidebar-footer a')) as HTMLAnchorElement[];
        expect(footerLinks.map(link => link.textContent?.trim())).toEqual(['All Modules', 'QA Support Hub']);
        expect(footerLinks.map(link => link.getAttribute('href'))).toEqual(['/tools/online-orders', '/']);
        expect(footerLinks[1].getAttribute('aria-label')).toBe('Back to QA Support Hub');
    });
});
