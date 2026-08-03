import { TestBed } from '@angular/core/testing';
import { OrderSectionNavigationComponent, OrderBuilderSection } from './order-section-navigation.component';

describe('OrderSectionNavigationComponent', () => {
  const sections: OrderBuilderSection[] = [
    { id: 'order-header-section', label: 'Order header', description: '', completed: true, hasIssues: false, issueCount: 0 },
    { id: 'products-section', label: 'Products', description: '', completed: false, hasIssues: true, issueCount: 2 }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [OrderSectionNavigationComponent] }).compileComponents();
  });

  it('renders the ordered sections, active step, and issue count', () => {
    const fixture = TestBed.createComponent(OrderSectionNavigationComponent);
    fixture.componentRef.setInput('sections', sections);
    fixture.componentRef.setInput('activeSectionId', 'products-section');
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons).toHaveLength(2);
    expect(buttons[1].getAttribute('aria-current')).toBe('step');
    expect(buttons[1].getAttribute('aria-label')).toContain('2 issues');
    expect(fixture.nativeElement.querySelector('.section-navigation__count').textContent.trim()).toBe('2');
  });

  it('emits the selected anchor from keyboard-usable buttons', () => {
    const fixture = TestBed.createComponent(OrderSectionNavigationComponent);
    fixture.componentRef.setInput('sections', sections);
    fixture.detectChanges();
    const selected: string[] = [];
    fixture.componentInstance.sectionSelected.subscribe(id => selected.push(id));

    (fixture.nativeElement.querySelectorAll('button')[1] as HTMLButtonElement).click();

    expect(selected).toEqual(['products-section']);
  });
});
