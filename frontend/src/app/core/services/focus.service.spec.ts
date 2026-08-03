import { TestBed } from '@angular/core/testing';
import { FocusService } from './focus.service';

describe('FocusService', () => {
  let service: FocusService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(FocusService);
    document.body.innerHTML = '';
  });

  it('returns false when the target does not exist', () => {
    expect(service.scrollToAndFocus('missing-id')).toBe(false);
  });

  it('focuses the element itself when it is a control', () => {
    const input = document.createElement('input');
    input.id = 'field-order-code';
    input.scrollIntoView = vi.fn();
    document.body.appendChild(input);

    expect(service.scrollToAndFocus('field-order-code')).toBe(true);
    expect(document.activeElement).toBe(input);
    expect(input.scrollIntoView).toHaveBeenCalled();
  });

  it('focuses the first focusable control inside a section container', () => {
    const section = document.createElement('div');
    section.id = 'products-card';
    section.scrollIntoView = vi.fn();
    const button = document.createElement('button');
    section.appendChild(button);
    document.body.appendChild(section);

    expect(service.scrollToAndFocus('products-card')).toBe(true);
    expect(document.activeElement).toBe(button);
  });
});
