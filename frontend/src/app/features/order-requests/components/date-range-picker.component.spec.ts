import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DateRangePickerComponent, DateRangeSelection } from './date-range-picker.component';

/** Guards the calendar contract the Order Requests filter depends on: a full
 * six-week grid, an explicit Apply, and an unchanged preset set. The popover's
 * clipping and alignment are CSS concerns and are deliberately not asserted. */
describe('DateRangePickerComponent', () => {
  let fixture: ComponentFixture<DateRangePickerComponent>;
  let component: DateRangePickerComponent;
  let emitted: DateRangeSelection[];

  function openOn(dateFrom: string | null, dateTo: string | null) {
    component.dateFrom = dateFrom;
    component.dateTo = dateTo;
    (fixture.nativeElement.querySelector('.date-trigger') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function days(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.calendar-day'));
  }

  function popover(): HTMLElement {
    return fixture.nativeElement.querySelector('.date-popover') as HTMLElement;
  }

  /** Places the closed trigger at a chosen viewport offset so placement can be
   * exercised; jsdom otherwise reports every rect as zero. */
  function placeTriggerAt(top: number) {
    const trigger = fixture.nativeElement.querySelector('.date-trigger') as HTMLButtonElement;
    trigger.getBoundingClientRect = () => ({ top, bottom: top + 42, height: 42 }) as DOMRect;
  }

  beforeEach(() => {
    Object.defineProperty(window, 'innerHeight', { value: 768, configurable: true });
    Object.defineProperty(window, 'innerWidth', { value: 1440, configurable: true });
    TestBed.configureTestingModule({ imports: [DateRangePickerComponent] });
    fixture = TestBed.createComponent(DateRangePickerComponent);
    component = fixture.componentInstance;
    emitted = [];
    component.rangeChange.subscribe(value => emitted.push(value));
    fixture.detectChanges();
  });

  it('renders a full six-week grid of 42 day cells', () => {
    openOn('2026-08-01', '2026-08-10');

    expect(days()).toHaveLength(42);
  });

  it('shows every August 2026 day including the 31st', () => {
    openOn('2026-08-01', '2026-08-10');

    const inMonth = days().filter(day => !day.classList.contains('is-outside'));
    expect(fixture.nativeElement.querySelector('.calendar-toolbar strong')!.textContent).toContain('August 2026');
    expect(inMonth).toHaveLength(31);
    expect(inMonth[inMonth.length - 1].textContent!.trim()).toBe('31');
  });

  it('keeps the four documented presets', () => {
    openOn(null, null);

    const labels = Array.from(fixture.nativeElement.querySelectorAll('.date-preset span'))
      .map(node => (node as HTMLElement).textContent!.trim());
    expect(labels).toEqual(['Today', 'This month', 'Last 7 days', 'Last 30 days']);
  });

  it('requires an explicit Apply and then emits both range ends', () => {
    openOn('2026-08-01', '2026-08-10');

    days()[6].click(); // Aug 1 restarts the range
    fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('.date-apply-button') as HTMLButtonElement).disabled).toBe(true);
    expect(emitted).toHaveLength(0);

    days()[15].click(); // Aug 10 completes it
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.date-apply-button') as HTMLButtonElement).click();

    expect(emitted).toEqual([{ dateFrom: '2026-08-01', dateTo: '2026-08-10' }]);
    expect(component.open()).toBe(false);
  });

  it('caps its height to the room left below the trigger', () => {
    placeTriggerAt(400); // 768 - 442 - 10 - 12 = 304px of usable room
    openOn('2026-08-01', '2026-08-10');

    expect(popover().style.maxHeight).toBe('304px');
    expect(popover().classList.contains('is-above')).toBe(false);
  });

  it('opens above the trigger when the room below is too tight', () => {
    placeTriggerAt(700); // only 16px below, 678px above

    openOn('2026-08-01', '2026-08-10');

    expect(popover().classList.contains('is-above')).toBe(true);
    expect(popover().style.maxHeight).toBe('560px');
  });

  it('re-measures when the viewport moves under an open popover', () => {
    placeTriggerAt(400);
    openOn('2026-08-01', '2026-08-10');
    expect(popover().style.maxHeight).toBe('304px');

    placeTriggerAt(200);
    window.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    expect(popover().style.maxHeight).toBe('504px');
  });

  it('clears both range ends and closes', () => {
    openOn('2026-08-01', '2026-08-10');

    (fixture.nativeElement.querySelector('.date-clear-button') as HTMLButtonElement).click();

    expect(emitted).toEqual([{ dateFrom: null, dateTo: null }]);
    expect(component.open()).toBe(false);
  });
});
