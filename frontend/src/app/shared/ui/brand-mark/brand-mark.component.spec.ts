import { TestBed } from '@angular/core/testing';
import { APP_ASSETS } from '../../../core/config/app-assets';
import { BrandMarkComponent } from './brand-mark.component';

describe('BrandMarkComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BrandMarkComponent]
    }).compileComponents();
  });

  it('renders a contained meaningful mark with the requested size', () => {
    const fixture = TestBed.createComponent(BrandMarkComponent);
    fixture.componentRef.setInput('src', APP_ASSETS.brand.rms.dark);
    fixture.componentRef.setInput('alt', 'RMS+');
    fixture.componentRef.setInput('size', '4rem');
    fixture.detectChanges();

    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
    const mark = fixture.nativeElement.querySelector('.brand-mark') as HTMLElement;
    expect(image.getAttribute('src')).toBe(APP_ASSETS.brand.rms.dark);
    expect(image.getAttribute('alt')).toBe('RMS+');
    expect(image.getAttribute('aria-hidden')).toBeNull();
    expect(mark.style.getPropertyValue('--brand-mark-size')).toBe('4rem');
    // Width defaults to the size, keeping square module marks square.
    expect(mark.style.getPropertyValue('--brand-mark-width')).toBe('4rem');
    expect(fixture.nativeElement.querySelector('.brand-mark--framed')).toBeNull();
  });

  it('accepts an explicit width so wide wordmarks are not letterboxed', () => {
    const fixture = TestBed.createComponent(BrandMarkComponent);
    fixture.componentRef.setInput('src', APP_ASSETS.brand.dbs.light);
    fixture.componentRef.setInput('alt', 'DBS');
    fixture.componentRef.setInput('size', '2.5rem');
    fixture.componentRef.setInput('width', '7.4rem');
    fixture.detectChanges();

    const mark = fixture.nativeElement.querySelector('.brand-mark') as HTMLElement;
    expect(mark.style.getPropertyValue('--brand-mark-size')).toBe('2.5rem');
    expect(mark.style.getPropertyValue('--brand-mark-width')).toBe('7.4rem');
  });

  it('hides decorative duplicates from assistive technology and supports a framed mark', () => {
    const fixture = TestBed.createComponent(BrandMarkComponent);
    fixture.componentRef.setInput('src', APP_ASSETS.modules.upc);
    fixture.componentRef.setInput('alt', 'UPC');
    fixture.componentRef.setInput('decorative', true);
    fixture.componentRef.setInput('framed', true);
    fixture.detectChanges();

    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
    expect(image.getAttribute('alt')).toBe('');
    expect(image.getAttribute('aria-hidden')).toBe('true');
    expect(fixture.nativeElement.querySelector('.brand-mark--framed')).toBeTruthy();
  });
});
