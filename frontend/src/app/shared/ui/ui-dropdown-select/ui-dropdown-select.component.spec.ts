import { OverlayContainer } from '@angular/cdk/overlay';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UiDropdownOption, UiDropdownSelectComponent } from './ui-dropdown-select.component';

const OPTIONS: UiDropdownOption[] = [
  { value: 'not_payment', label: 'Not paid', tone: 'neutral' },
  { value: 'done_payment', label: 'Paid', tone: 'success' },
  { value: 'failed_payment', label: 'Failed', tone: 'danger' }
];

describe('UiDropdownSelectComponent', () => {
  let fixture: ComponentFixture<UiDropdownSelectComponent>;
  let overlayContainer: OverlayContainer;
  let overlay: HTMLElement;
  let emitted: string[];

  function trigger(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('.dd-trigger') as HTMLButtonElement;
  }

  function optionButtons(): HTMLButtonElement[] {
    return Array.from(overlay.querySelectorAll('.dd-option'));
  }

  function open() {
    trigger().click();
    fixture.detectChanges();
  }

  function press(key: string) {
    trigger().dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [UiDropdownSelectComponent] });
    fixture = TestBed.createComponent(UiDropdownSelectComponent);
    fixture.componentRef.setInput('options', OPTIONS);
    fixture.componentRef.setInput('value', 'done_payment');
    fixture.componentRef.setInput('ariaLabel', 'Payment status');
    overlayContainer = TestBed.inject(OverlayContainer);
    overlay = overlayContainer.getContainerElement();
    emitted = [];
    fixture.componentInstance.valueChange.subscribe(value => emitted.push(value));
    fixture.detectChanges();
  });

  afterEach(() => overlayContainer.ngOnDestroy());

  it('shows the option label on the trigger, never the raw payload value', () => {
    expect(trigger().textContent).toContain('Paid');
    expect(trigger().textContent).not.toContain('done_payment');
  });

  it('opens its list into the overlay container instead of a native popup', () => {
    expect(fixture.nativeElement.querySelector('select')).toBeNull();
    expect(overlay.querySelector('.dd-panel')).toBeNull();

    open();

    expect(overlay.querySelector('.dd-panel')).not.toBeNull();
    expect(optionButtons().map(button => button.textContent!.trim())).toEqual(['Not paid', 'Paid', 'Failed']);
    expect(trigger().getAttribute('aria-expanded')).toBe('true');
  });

  it('marks the current option as selected for assistive technology', () => {
    open();

    const selected = optionButtons().filter(button => button.getAttribute('aria-selected') === 'true');
    expect(selected).toHaveLength(1);
    expect(selected[0].textContent).toContain('Paid');
  });

  it('emits the raw value of a clicked option and closes', () => {
    open();

    optionButtons()[2].click();
    fixture.detectChanges();

    expect(emitted).toEqual(['failed_payment']);
    expect(fixture.componentInstance.open()).toBe(false);
    expect(overlay.querySelector('.dd-panel')).toBeNull();
  });

  it('opens and selects with the keyboard alone', () => {
    press('ArrowDown');
    expect(fixture.componentInstance.open()).toBe(true);
    expect(fixture.componentInstance.activeIndex()).toBe(1); // starts on the selected option

    press('ArrowDown');
    press('Enter');

    expect(emitted).toEqual(['failed_payment']);
    expect(fixture.componentInstance.open()).toBe(false);
  });

  it('moves to the first and last option with Home and End', () => {
    press(' ');
    press('End');
    expect(fixture.componentInstance.activeIndex()).toBe(2);

    press('Home');
    expect(fixture.componentInstance.activeIndex()).toBe(0);
  });

  it('closes on Escape without emitting', () => {
    open();

    press('Escape');

    expect(emitted).toEqual([]);
    expect(fixture.componentInstance.open()).toBe(false);
    expect(overlay.querySelector('.dd-panel')).toBeNull();
  });

  it('closes when the backdrop is clicked', () => {
    open();

    (overlay.querySelector('.ui-dropdown-backdrop') as HTMLElement).click();
    fixture.detectChanges();

    expect(emitted).toEqual([]);
    expect(fixture.componentInstance.open()).toBe(false);
  });

  it('cannot be opened while disabled', () => {
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    press('ArrowDown');
    fixture.componentInstance.toggle();
    fixture.detectChanges();

    expect(fixture.componentInstance.open()).toBe(false);
    expect(trigger().disabled).toBe(true);
  });
});
