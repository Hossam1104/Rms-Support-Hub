import { Directive, ElementRef, Input, OnChanges, SimpleChanges, inject } from '@angular/core';

function easeOutCubic(t: number): number {
  return 1 - Math.pow(1 - t, 3);
}

/**
 * Animates the host element's text content from its current numeric value
 * up to `appCountUp` over ~900ms using requestAnimationFrame + easeOutCubic,
 * thousands-separated via toLocaleString(). Short-circuits to the final
 * value immediately when reduced motion is active -- an explicit
 * `data-motion` choice from MotionService wins over the
 * `prefers-reduced-motion` media query (see core/services/motion.service.ts).
 */
@Directive({
  selector: '[appCountUp]',
  standalone: true
})
export class CountUpDirective implements OnChanges {
  private el = inject(ElementRef<HTMLElement>);
  private frame: number | null = null;
  private current = 0;

  @Input('appCountUp') target: number = 0;
  @Input() countUpDuration: number = 900;
  @Input() countUpDecimals: number = 0;

  ngOnChanges(changes: SimpleChanges) {
    if (!changes['target']) return;

    const from = changes['target'].firstChange ? 0 : this.current;
    const to = this.target || 0;

    if (this.prefersReducedMotion()) {
      this.render(to);
      return;
    }

    this.animate(from, to);
  }

  private prefersReducedMotion(): boolean {
    if (typeof window === 'undefined') return false;
    const motion = document.documentElement.getAttribute('data-motion');
    if (motion === 'reduce') return true;
    if (motion === 'full') return false;
    return !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
  }

  private animate(from: number, to: number) {
    if (this.frame !== null) cancelAnimationFrame(this.frame);

    const start = performance.now();
    const duration = this.countUpDuration;

    const step = (now: number) => {
      const elapsed = now - start;
      const t = Math.min(1, elapsed / duration);
      const value = from + (to - from) * easeOutCubic(t);
      this.render(value);

      if (t < 1) {
        this.frame = requestAnimationFrame(step);
      } else {
        this.frame = null;
        this.render(to);
      }
    };

    this.frame = requestAnimationFrame(step);
  }

  private render(value: number) {
    this.current = value;
    const rounded = this.countUpDecimals > 0
      ? Number(value.toFixed(this.countUpDecimals))
      : Math.round(value);
    this.el.nativeElement.textContent = rounded.toLocaleString('en-US', {
      minimumFractionDigits: this.countUpDecimals,
      maximumFractionDigits: this.countUpDecimals
    });
  }
}
