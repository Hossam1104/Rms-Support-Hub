import { TestBed } from '@angular/core/testing';
import { UiButtonComponent } from './ui-button/ui-button.component';
import { UiCardComponent } from './ui-card/ui-card.component';
import { UiFieldComponent } from './ui-field/ui-field.component';
import { UiInputComponent } from './ui-input/ui-input.component';
import { UiSectionComponent } from './ui-section/ui-section.component';
import { UiSelectComponent } from './ui-select/ui-select.component';
import { UiTableComponent } from './ui-table/ui-table.component';
import { UiToolbarComponent } from './ui-toolbar/ui-toolbar.component';
import { RiyalComponent } from './riyal/riyal.component';

describe('U5 shared UI primitives', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UiButtonComponent, UiCardComponent, UiFieldComponent, UiInputComponent, UiSectionComponent, UiSelectComponent, UiTableComponent, UiToolbarComponent, RiyalComponent]
    }).compileComponents();
  });

  it('renders the Riyal asset as a masked glyph with an accessible name', () => {
    const fixture = TestBed.createComponent(RiyalComponent);
    fixture.detectChanges();

    // The glyph must be the shared asset, never literal "SAR"/"ر.س" text.
    const icon = fixture.nativeElement.querySelector('.riyal-icon') as HTMLElement;
    expect(icon).toBeTruthy();
    expect(icon.getAttribute('aria-hidden')).toBe('true');
    expect(fixture.nativeElement.textContent).not.toContain('SAR');
    expect(fixture.nativeElement.querySelector('.sr-only').textContent).toContain('Saudi Riyal');
  });

  it('drops the Riyal accessible name when an adjacent label already states the currency', () => {
    const fixture = TestBed.createComponent(RiyalComponent);
    fixture.componentRef.setInput('decorative', true);
    fixture.componentRef.setInput('size', 0.85);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sr-only')).toBeNull();
    expect((fixture.nativeElement.querySelector('.riyal-icon') as HTMLElement).style.width).toBe('0.85em');
  });

  it('emits a button action but blocks disabled and loading activation', () => {
    const fixture = TestBed.createComponent(UiButtonComponent);
    const pressed: number[] = [];
    fixture.componentInstance.pressed.subscribe(() => pressed.push(1));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button').click();
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();
    fixture.componentRef.setInput('disabled', false);
    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();

    expect(pressed).toEqual([1]);
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(true);
  });

  it('exposes accessible collapse state and completion on a section', () => {
    const fixture = TestBed.createComponent(UiSectionComponent);
    fixture.componentRef.setInput('title', 'Order header');
    fixture.componentRef.setInput('completed', true);
    fixture.detectChanges();
    const toggle = fixture.nativeElement.querySelector('.ui-section__toggle') as HTMLButtonElement;

    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('.is-complete')).toBeTruthy();
    toggle.click();
    fixture.detectChanges();

    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('.ui-section__body')).toBeNull();
  });

  it('keeps a collapsed invalid section visibly marked', () => {
    const fixture = TestBed.createComponent(UiSectionComponent);
    fixture.componentRef.setInput('title', 'Products');
    fixture.componentRef.setInput('hasIssues', true);
    fixture.componentRef.setInput('issueCount', 2);
    fixture.detectChanges();
    const toggle = fixture.nativeElement.querySelector('.ui-section__toggle') as HTMLButtonElement;

    toggle.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.ui-section__marker.is-issue')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.ui-section__issue').textContent).toContain('2 issues');
    expect(fixture.nativeElement.querySelector('.ui-section__body')).toBeNull();
  });

  it('links field labels and errors to projected controls', () => {
    const fixture = TestBed.createComponent(UiFieldComponent);
    fixture.componentRef.setInput('label', 'Item code');
    fixture.componentRef.setInput('forId', 'item-code');
    fixture.componentRef.setInput('required', true);
    fixture.componentRef.setInput('error', 'Item code is required.');
    fixture.detectChanges();

    const label = fixture.nativeElement.querySelector('label');
    const error = fixture.nativeElement.querySelector('[role="alert"]');
    expect(label.getAttribute('for')).toBe('item-code');
    expect(label.textContent).toContain('*');
    expect(error.textContent).toContain('Item code is required.');
    expect(fixture.componentInstance.describedBy()).toContain('-error');
  });

  it('renders tokenized select/table/toolbar structures and card focus semantics', () => {
    const card = TestBed.createComponent(UiCardComponent);
    card.componentRef.setInput('variant', 'interactive');
    card.detectChanges();
    expect(card.nativeElement.querySelector('[role="button"]').getAttribute('tabindex')).toBe('0');

    const select = TestBed.createComponent(UiSelectComponent);
    select.componentRef.setInput('options', [{ value: 'testing', label: 'Testing' }]);
    select.detectChanges();
    expect(select.nativeElement.querySelector('select')).toBeTruthy();
    expect(select.nativeElement.querySelectorAll('option')).toHaveLength(1);

    const table = TestBed.createComponent(UiTableComponent);
    table.componentRef.setInput('dense', true);
    table.componentRef.setInput('stickyHeader', true);
    table.detectChanges();
    expect(table.nativeElement.querySelector('.ui-table--dense')).toBeTruthy();
    expect(table.nativeElement.querySelector('.ui-table--sticky')).toBeTruthy();

    const toolbar = TestBed.createComponent(UiToolbarComponent);
    toolbar.componentRef.setInput('compact', true);
    toolbar.detectChanges();
    expect(toolbar.nativeElement.querySelector('.ui-toolbar--compact')).toBeTruthy();
  });
});
