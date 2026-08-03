import { TestBed } from '@angular/core/testing';
import { BranchOption } from '../../../core/models';
import { SearchableSelectComponent } from './searchable-select.component';

const branches: BranchOption[] = [
  { code: '101', name: 'Main Branch' },
  { code: 'P900', name: 'Riyadh Pharmacy' },
];

describe('SearchableSelectComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchableSelectComponent],
    }).compileComponents();
  });

  function createFixture() {
    const fixture = TestBed.createComponent(SearchableSelectComponent);
    fixture.componentRef.setInput('options', branches);
    fixture.detectChanges();
    return fixture;
  }

  it('creates the selector with combobox ARIA attributes', () => {
    const fixture = createFixture();
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;

    expect(input).toBeTruthy();
    expect(input.getAttribute('role')).toBe('combobox');
    expect(input.getAttribute('aria-controls')).toBe(fixture.componentInstance.listboxId);
    expect(input.getAttribute('aria-expanded')).toBe('false');
  });

  it('filters options by branch code and branch name', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.query = 'p900';
    expect(component.filteredOptions).toEqual([branches[1]]);

    component.query = 'main';
    expect(component.filteredOptions).toEqual([branches[0]]);
  });

  it('emits only the selected branch code', () => {
    const fixture = createFixture();
    const values: Array<string | null> = [];
    fixture.componentInstance.valueChange.subscribe((value) => values.push(value));

    fixture.componentInstance.select(branches[1]);

    expect(values).toEqual(['P900']);
    expect(fixture.componentInstance.open).toBe(false);
  });

  it('selects with ArrowDown and Enter', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;
    const values: Array<string | null> = [];
    component.valueChange.subscribe((value) => values.push(value));

    component.openPanel();
    component.onKeydown(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    component.onKeydown(new KeyboardEvent('keydown', { key: 'Enter' }));

    expect(values).toEqual(['P900']);
  });

  it('closes on Escape', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    component.openPanel();
    component.onKeydown(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(component.open).toBe(false);
    expect(component.activeIndex).toBe(-1);
  });

  it('tracks loading, empty, and error state inputs', () => {
    const fixture = createFixture();
    const component = fixture.componentInstance;

    fixture.componentRef.setInput('loading', true);
    fixture.detectChanges();
    expect(component.loading).toBe(true);

    fixture.componentRef.setInput('loading', false);
    fixture.componentRef.setInput('options', []);
    fixture.detectChanges();
    expect(component.filteredOptions).toEqual([]);

    fixture.componentRef.setInput('error', 'Branch lookup unavailable');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input').getAttribute('aria-invalid')).toBe('true');
  });
});
