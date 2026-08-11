import { ModuleCardComponent } from './module-card.component';
import { EnvironmentDto, ModuleDto } from '../../core/models';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_ASSETS } from '../../core/config/app-assets';

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
    expect(component.getLogoUrl('upc_ecommerce')).toBe(APP_ASSETS.modules.upc);
    expect(component.getLogoUrl('ghc_ecommerce')).toBe(APP_ASSETS.modules.ghc);
    expect(component.getLogoUrl('ghc_unicommerce')).toBe(APP_ASSETS.modules.ghc);
    expect(component.getLogoUrl('unknown_module')).toBe('');
    expect(component.getLogoAlt('upc_ecommerce')).toBe('UPC');
    expect(component.getLogoAlt('ghc_ecommerce')).toBe('GHC / Whites');
  });

  // The accent names are the hub tool card's, so one grid language covers both
  // dashboards and an unmapped module still gets a defined edge light.
  it('maps every module onto a shared tool-card accent', () => {
    const component = new ModuleCardComponent();

    expect(component.getAccentClass('upc_ecommerce')).toBe('module-card--amber');
    expect(component.getAccentClass('ghc_ecommerce')).toBe('module-card--info');
    expect(component.getAccentClass('ghc_unicommerce')).toBe('module-card--brand');
    expect(component.getAccentClass('oms')).toBe('module-card--teal');
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
    // An unavailable module has no route and no environments, so the card says
    // why it is empty instead of rendering a dead action row.
    expect(fixture.nativeElement.querySelector('.module-card__action')).toBeNull();
    expect(fixture.nativeElement.querySelector('.module-card__availability')?.textContent)
      .toContain('Planned module.');
  });

  it('renders capability chips and a pinned action for an available module', async () => {
    await TestBed.configureTestingModule({
      imports: [ModuleCardComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(ModuleCardComponent);
    fixture.componentInstance.module = {
      ...module,
      capabilities: { itemLookup: true, consumerLookup: false, orderRequests: true, cancel: false, resend: true }
    } as unknown as ModuleDto;
    fixture.detectChanges();

    const chips = Array.from(fixture.nativeElement.querySelectorAll('.module-card__capability')) as HTMLElement[];
    expect(chips.map(chip => chip.textContent?.trim())).toEqual(['Item lookup', 'Order Requests', 'Resend']);
    const icons = chips.map(chip => chip.querySelector('i') as HTMLElement);
    expect(icons.every(icon => icon.classList.contains('bi'))).toBe(true);
    expect(icons.map(icon => icon.classList.contains('bi-search') ? 'search' : icon.classList.contains('bi-inboxes') ? 'inboxes' : 'other'))
      .toEqual(['search', 'inboxes', 'other']);

    const action = fixture.nativeElement.querySelector('.module-card__action') as HTMLAnchorElement;
    expect(action.getAttribute('href')).toBe('/tools/online-orders/modules/upc_ecommerce/order');
    expect(action.textContent).toContain('Open Module');
    expect(fixture.nativeElement.querySelector('.module-card__availability')).toBeNull();
  });
});
