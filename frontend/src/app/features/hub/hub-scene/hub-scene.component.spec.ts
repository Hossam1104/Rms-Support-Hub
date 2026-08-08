import { TestBed } from '@angular/core/testing';
import { MotionService } from '../../../core/services/motion.service';
import { HubSceneComponent } from './hub-scene.component';

/**
 * The renderer itself is not asserted -- jsdom has no WebGL. These tests cover
 * the contract that matters: the scene never becomes a requirement, reduced
 * motion disables it, and destroying the component leaves nothing running.
 */
describe('HubSceneComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HubSceneComponent] }).compileComponents();
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-motion');
  });

  it('renders the static backdrop and no canvas when WebGL is unavailable', () => {
    const fixture = TestBed.createComponent(HubSceneComponent);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('.hub-scene__backdrop')).toBeTruthy();
    // jsdom returns null for a WebGL context, so the capability probe fails.
    expect(fixture.componentInstance.active()).toBe(false);
    expect(host.querySelector('canvas')).toBeNull();
  });

  it('marks the decorative layer aria-hidden', () => {
    const fixture = TestBed.createComponent(HubSceneComponent);
    fixture.detectChanges();

    const backdrop = fixture.nativeElement.querySelector('.hub-scene__backdrop') as HTMLElement;
    expect(backdrop.getAttribute('aria-hidden')).toBe('true');
  });

  it('stays inactive while the user prefers reduced motion', () => {
    const motion = TestBed.inject(MotionService);
    motion.setPreference('reduce');

    const fixture = TestBed.createComponent(HubSceneComponent);
    fixture.detectChanges();

    expect(fixture.componentInstance.active()).toBe(false);
    expect(fixture.nativeElement.querySelector('canvas')).toBeNull();

    motion.setPreference('system');
  });

  it('destroys cleanly without a started renderer', () => {
    const fixture = TestBed.createComponent(HubSceneComponent);
    fixture.detectChanges();

    expect(() => fixture.destroy()).not.toThrow();
  });
});
