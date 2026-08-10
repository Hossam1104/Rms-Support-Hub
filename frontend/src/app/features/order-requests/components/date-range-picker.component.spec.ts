import { OverlayContainer } from '@angular/cdk/overlay';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DateRangePickerComponent, DateRangeSelection } from './date-range-picker.component';

/** Guards the calendar contract the Order Requests filter depends on: a full
 * six-week grid, an explicit Apply, and an unchanged preset set. The panel now
 * renders into the CDK overlay container, so it is queried from there rather
 * than from the fixture. Pixel placement belongs to the CDK and is not asserted
 * here -- jsdom reports no layout. */
describe('DateRangePickerComponent', () => {
  let fixture: ComponentFixture<DateRangePickerComponent>;
  let component: DateRangePickerComponent;
  let overlayContainer: OverlayContainer;
  let overlay: HTMLElement;
  let emitted: DateRangeSelection[];

  function openOn(dateFrom: string | null, dateTo: string | null) {
    component.dateFrom = dateFrom;
    component.dateTo = dateTo;
    (fixture.nativeElement.querySelector('.date-trigger') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function days(): HTMLButtonElement[] {
    return Array.from(overlay.querySelectorAll('.calendar-day'));
  }

  function click(selector: string) {
    (overlay.querySelector(selector) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [DateRangePickerComponent] });
    fixture = TestBed.createComponent(DateRangePickerComponent);
    component = fixture.componentInstance;
    overlayContainer = TestBed.inject(OverlayContainer);
    overlay = overlayContainer.getContainerElement();
    emitted = [];
    component.rangeChange.subscribe(value => emitted.push(value));
    fixture.detectChanges();
  });

  afterEach(() => overlayContainer.ngOnDestroy());

  it('renders the panel into the overlay container, not inside the filter card', () => {
    expect(overlay.querySelector('.date-popover')).toBeNull();

    openOn('2026-08-01', '2026-08-10');

    expect(overlay.querySelector('.date-popover')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.date-popover')).toBeNull();
  });

  it('renders a full six-week grid of 42 day cells', () => {
    openOn('2026-08-01', '2026-08-10');

    expect(days()).toHaveLength(42);
  });

  it('shows every August 2026 day including the 31st', () => {
    openOn('2026-08-01', '2026-08-10');

    const inMonth = days().filter(day => !day.classList.contains('is-outside'));
    expect(overlay.querySelector('.calendar-toolbar strong')!.textContent).toContain('August 2026');
    expect(inMonth).toHaveLength(31);
    expect(inMonth[inMonth.length - 1].textContent!.trim()).toBe('31');
  });

  it('keeps the four documented presets', () => {
    openOn(null, null);

    const labels = Array.from(overlay.querySelectorAll('.date-preset span'))
      .map(node => (node as HTMLElement).textContent!.trim());
    expect(labels).toEqual(['Today', 'This month', 'Last 7 days', 'Last 30 days']);
  });

  it('requires an explicit Apply and then emits both range ends', () => {
    openOn('2026-08-01', '2026-08-10');

    days()[6].click(); // Aug 1 restarts the range
    fixture.detectChanges();
    expect((overlay.querySelector('.date-apply-button') as HTMLButtonElement).disabled).toBe(true);
    expect(emitted).toHaveLength(0);

    days()[15].click(); // Aug 10 completes it
    fixture.detectChanges();
    click('.date-apply-button');

    expect(emitted).toEqual([{ dateFrom: '2026-08-01', dateTo: '2026-08-10' }]);
    expect(component.open()).toBe(false);
    expect(overlay.querySelector('.date-popover')).toBeNull();
  });

  it('clears both range ends and closes', () => {
    openOn('2026-08-01', '2026-08-10');

    click('.date-clear-button');

    expect(emitted).toEqual([{ dateFrom: null, dateTo: null }]);
    expect(component.open()).toBe(false);
  });

  it('closes on Cancel without emitting a range', () => {
    openOn('2026-08-01', '2026-08-10');

    click('.date-cancel-button');

    expect(emitted).toEqual([]);
    expect(component.open()).toBe(false);
    expect(overlay.querySelector('.date-popover')).toBeNull();
  });

  it('closes when the overlay backdrop is clicked', () => {
    openOn('2026-08-01', '2026-08-10');

    (overlay.querySelector('.date-range-backdrop') as HTMLElement).click();
    fixture.detectChanges();

    expect(component.open()).toBe(false);
    expect(overlay.querySelector('.date-popover')).toBeNull();
  });

  it('closes on Escape through the overlay keyboard dispatcher', () => {
    openOn('2026-08-01', '2026-08-10');

    const escape = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true });
    Object.defineProperty(escape, 'keyCode', { get: () => 27 });
    (overlay.querySelector('.date-popover') as HTMLElement).dispatchEvent(escape);
    fixture.detectChanges();

    expect(component.open()).toBe(false);
    expect(overlay.querySelector('.date-popover')).toBeNull();
  });
});
