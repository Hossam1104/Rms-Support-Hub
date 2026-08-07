import { ModuleCardComponent } from './module-card.component';
import { EnvironmentDto, ModuleDto } from '../../core/models';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

describe('ModuleCardComponent', () => {
  const module = {
    key: 'upc_ecommerce',
    label: 'UPC',
    client: 'Client',
    available: true,
    environments: []
  } as unknown as ModuleDto;

  it('keeps module-specific routes and logo selection', () => {
    const component = new ModuleCardComponent();

    expect(component.getModuleRoute('ghc_unicommerce')).toEqual(['/tools/online-orders/modules', 'ghc_unicommerce', 'unicommerce']);
    expect(component.getModuleRoute('upc_ecommerce')).toEqual(['/tools/online-orders/modules', 'upc_ecommerce', 'order']);
    expect(component.getLogoUrl('upc_ecommerce')).toBe('assets/upc_logo.svg');
    expect(component.getLogoUrl('ghc_ecommerce')).toBe('assets/whites_logo.svg');
    expect(component.getLogoUrl('ghc_unicommerce')).toBe('assets/whites_logo.svg');
  });

  it('emits the selected environment from a card action', () => {
    const component = new ModuleCardComponent();
    component.module = module;
    const environment = { key: 'UPC Testing', environment: 'Testing' } as EnvironmentDto;
    const emitted: EnvironmentDto[] = [];
    component.selectEnv.subscribe(value => emitted.push(value));

    component.onSelectEnv(environment);

    expect(emitted).toEqual([environment]);
  });

  it('uses the shared Coming Soon status label for unavailable modules', async () => {
    await TestBed.configureTestingModule({
      imports: [ModuleCardComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(ModuleCardComponent);
    fixture.componentInstance.module = {
      ...module,
      available: false,
      environments: []
    } as ModuleDto;
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('.status-badge');
    expect(status?.textContent?.trim()).toBe('Coming Soon');
    expect(fixture.nativeElement.querySelector('[role="status"]')).toBeTruthy();
  });
});
