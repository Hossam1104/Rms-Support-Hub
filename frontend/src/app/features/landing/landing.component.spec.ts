import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EnvironmentDto, EnvironmentHealthMap, ModuleDto, environmentHealthKey } from '../../core/models';
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
    template: '<header><h1>{{ title }}</h1><p>{{ subtitle }}</p><ng-content></ng-content></header>'
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
    @Input() health: EnvironmentHealthMap = new Map();
    @Input() healthPending = false;
    @Output() selectEnv = new EventEmitter<EnvironmentDto>();
}

class StubModuleService {
    modules = signal<ModuleDto[]>([
        { key: 'oms', label: 'OMS', available: false, environments: [] } as unknown as ModuleDto,
        { key: 'upc_ecommerce', label: 'UPC E-commerce', available: true, environments: [{}, {}] } as unknown as ModuleDto,
        { key: 'ghc_ecommerce', label: 'GHC E-commerce', available: true, environments: [{}] } as unknown as ModuleDto
    ]);
    health = signal<EnvironmentHealthMap>(new Map());
    healthPending = signal(false);
    loadHealthCalls = 0;
    // Mirrors the real service: pending flips synchronously, before the await.
    loadHealth = async () => { this.loadHealthCalls++; this.healthPending.set(true); };
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
        expect(main.querySelectorAll('app-module-card')).toHaveLength(3);
        expect(main.querySelector('app-empty-state')).toBeNull();
        // The grid is introduced the same way the Hub introduces its tools.
        expect(main.querySelector('.landing-eyebrow')?.textContent).toContain('Module directory');
        expect(main.querySelector('.landing-heading__title')?.textContent?.trim()).toBe('Choose a module');
    });

    // The Coming Soon module carries no environments, so it must not lead a
    // grid whose first row is otherwise full of selectable routes.
    it('leads with available modules and summarizes the workspace in the hero', () => {
        const fixture = TestBed.createComponent(LandingComponent);
        fixture.detectChanges();

        const main = fixture.nativeElement.querySelector('main') as HTMLElement;
        const cards = Array.from(main.querySelectorAll('app-module-card')) as HTMLElement[];
        expect(cards.map(card => card.textContent?.trim())).toEqual(['UPC E-commerce', 'GHC E-commerce', 'OMS']);

        const stats = Array.from(main.querySelectorAll('.landing-stat strong')) as HTMLElement[];
        expect(stats.map(stat => stat.textContent?.trim())).toEqual(['2 of 3', '3 routes', 'Checking…']);
    });

    // The probe sweep waits on internal hosts, so it must never gate the grid.
    it('starts the reachability sweep and rolls the result up in the hero', () => {
        const fixture = TestBed.createComponent(LandingComponent);
        fixture.detectChanges();

        expect(moduleService.loadHealthCalls).toBe(1);
        expect(fixture.nativeElement.querySelectorAll('app-module-card')).toHaveLength(3);

        const health: EnvironmentHealthMap = new Map([
            [environmentHealthKey('upc_ecommerce', 'UPC Production'), 'reachable'],
            [environmentHealthKey('upc_ecommerce', 'UPC Testing'), 'unreachable'],
            [environmentHealthKey('ghc_ecommerce', 'GHC Production'), 'reachable'],
            [environmentHealthKey('oms', 'OMS Testing'), 'unconfigured']
        ]);
        moduleService.health.set(health);
        moduleService.healthPending.set(false);
        fixture.detectChanges();

        const stats = Array.from(fixture.nativeElement.querySelectorAll('.landing-stat strong')) as HTMLElement[];
        // Unconfigured lanes are excluded: nothing could be probed there.
        expect(stats[2].textContent?.trim()).toBe('2 of 3');
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
