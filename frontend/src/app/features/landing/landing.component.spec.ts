import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EnvironmentDto, ModuleDto } from '../../core/models';
import { ModuleService } from '../../core/services/module.service';
import { EmptyStateComponent } from '../../shared/ui';
import { LandingComponent } from './landing.component';

@Component({
    standalone: true,
    selector: 'app-navbar',
    template: ''
})
class StubNavbarComponent { }

@Component({
    standalone: true,
    selector: 'app-breadcrumb',
    template: '<nav aria-label="Breadcrumb"></nav>'
})
class StubBreadcrumbComponent { }

@Component({
    standalone: true,
    selector: 'app-page-header',
    template: '<header><h1>{{ title }}</h1><p>{{ subtitle }}</p></header>'
})
class StubPageHeaderComponent {
    @Input() title = '';
    @Input() subtitle = '';
}

@Component({
    standalone: true,
    selector: 'app-module-card',
    template: '<article>{{ module.label }}</article>'
})
class StubModuleCardComponent {
    @Input() module!: ModuleDto;
    @Output() selectEnv = new EventEmitter<EnvironmentDto>();
}

class StubModuleService {
    modules = signal<ModuleDto[]>([
        { key: 'upc_ecommerce', label: 'UPC E-commerce' } as unknown as ModuleDto,
        { key: 'ghc_ecommerce', label: 'GHC E-commerce' } as unknown as ModuleDto
    ]);
}

describe('LandingComponent', () => {
    let moduleService: StubModuleService;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [LandingComponent],
            providers: [provideRouter([]), { provide: ModuleService, useClass: StubModuleService }]
        }).overrideComponent(LandingComponent, {
            set: {
                imports: [StubNavbarComponent, StubBreadcrumbComponent, StubPageHeaderComponent, StubModuleCardComponent, EmptyStateComponent]
            }
        }).compileComponents();

        moduleService = TestBed.inject(ModuleService) as unknown as StubModuleService;
    });

    it('renders the Online Order identity inside the shared landing structure', () => {
        const fixture = TestBed.createComponent(LandingComponent);
        fixture.detectChanges();

        const main = fixture.nativeElement.querySelector('main') as HTMLElement;
        expect(main.querySelector('app-breadcrumb')).toBeTruthy();
        expect(main.querySelector('h1')?.textContent?.trim()).toBe('Online Order Tool');
        expect(main.querySelector('[aria-label="Online Order modules"]')).toBeTruthy();
        expect(main.querySelectorAll('app-module-card')).toHaveLength(2);
        expect(main.querySelector('app-empty-state')).toBeNull();
    });

    // ModuleService.initialize() sets an empty list for both an empty response
    // and a failed load, so the landing must never show a bare header over
    // empty space.
    it('renders a neutral empty state when no modules are available', () => {
        moduleService.modules.set([]);

        const fixture = TestBed.createComponent(LandingComponent);
        fixture.detectChanges();

        const main = fixture.nativeElement.querySelector('main') as HTMLElement;
        expect(main.querySelectorAll('app-module-card')).toHaveLength(0);

        const empty = main.querySelector('app-empty-state') as HTMLElement;
        expect(empty).toBeTruthy();
        expect(empty.textContent).toContain('No Online Order modules are currently available.');
        expect(main.querySelector('[aria-label="Online Order modules"]')).toBeTruthy();
    });
});
