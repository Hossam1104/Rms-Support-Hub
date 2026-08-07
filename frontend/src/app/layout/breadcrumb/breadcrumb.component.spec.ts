import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BreadcrumbComponent } from './breadcrumb.component';

describe('BreadcrumbComponent', () => {
    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [BreadcrumbComponent],
            providers: [provideRouter([])]
        }).compileComponents();
    });

    it('renders the Online Orders landing hierarchy with a current page crumb', () => {
        const fixture = TestBed.createComponent(BreadcrumbComponent);
        fixture.detectChanges();

        const nav = fixture.nativeElement as HTMLElement;
        expect(Array.from(nav.querySelectorAll('a')).map(link => link.textContent?.trim())).toEqual(['Dashboard']);
        expect(nav.querySelector('[aria-current="page"]')?.textContent?.trim()).toBe('Online Orders');
        expect(nav.querySelector('ol')).toBeTruthy();
    });

    it('links the module crumb to its default workspace and keeps the active tab non-clickable', () => {
        const fixture = TestBed.createComponent(BreadcrumbComponent);
        fixture.componentInstance.moduleLabel = 'UPC E-commerce';
        fixture.componentInstance.moduleRoute = ['/tools/online-orders/modules', 'upc_ecommerce', 'order'];
        fixture.componentInstance.currentTab = 'Order Requests';
        fixture.detectChanges();

        const nav = fixture.nativeElement as HTMLElement;
        const links = Array.from(nav.querySelectorAll('a')) as HTMLAnchorElement[];
        expect(links.map(link => link.textContent?.trim())).toEqual(['Dashboard', 'Online Orders', 'UPC E-commerce']);
        expect(links[1].getAttribute('href')).toBe('/tools/online-orders');
        expect(links[2].getAttribute('href')).toBe('/tools/online-orders/modules/upc_ecommerce/order');
        expect(nav.querySelector('[aria-current="page"]')?.textContent?.trim()).toBe('Order Requests');
        expect(nav.querySelector('[aria-current="page"]')?.closest('a')).toBeNull();
    });
});
