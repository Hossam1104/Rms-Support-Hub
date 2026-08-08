import { Component, ElementRef, NgZone, OnDestroy, effect, inject, signal, viewChild } from '@angular/core';
import { MotionService } from '../../../core/services/motion.service';
import { ThemeService } from '../../../core/services/theme.service';

/** Node count and link radius are deliberately small: the scene is decoration,
 * not a visualization, so it must stay cheap on integrated GPUs. */
const NODE_COUNT = 88;
const LINK_DISTANCE = 2.15;
const FIELD = 9;
/** Retina panels gain nothing here and cost 4x the fragments. */
const MAX_PIXEL_RATIO = 1.5;

/**
 * Decorative constellation behind the Hub hero.
 *
 * Contract:
 * - Three.js is imported dynamically, so it lands in its own lazy chunk and
 *   never reaches the initial bundle or any other feature.
 * - The canvas is `aria-hidden` and pointer-transparent; the Hub is fully
 *   usable without it. Missing WebGL, a failed import, or reduced motion all
 *   fall back to the CSS `--scene-backdrop` gradient painted on the host.
 * - Rendering pauses while the document is hidden and every listener,
 *   animation frame, geometry, material, and renderer is released on destroy.
 */
@Component({
  selector: 'app-hub-scene',
  standalone: true,
  template: `
    <div class="hub-scene__backdrop" aria-hidden="true"></div>
    @if (active()) {
      <canvas #canvas class="hub-scene__canvas" aria-hidden="true"></canvas>
    }
  `,
  styles: [`
    :host { position: absolute; inset: 0; display: block; overflow: hidden; pointer-events: none; }
    .hub-scene__backdrop { position: absolute; inset: 0; background: var(--scene-backdrop); }
    .hub-scene__canvas { position: absolute; inset: 0; width: 100%; height: 100%; opacity: 0; transition: opacity var(--transition-slow); }
    .hub-scene__canvas--ready { opacity: 1; }
  `]
})
export class HubSceneComponent implements OnDestroy {
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly motion = inject(MotionService);
  private readonly theme = inject(ThemeService);
  private readonly zone = inject(NgZone);
  private readonly host = inject(ElementRef<HTMLElement>);

  /** False whenever the scene must not run: reduced motion or no WebGL. */
  readonly active = signal(false);

  private teardown: (() => void) | null = null;
  private applyTheme: (() => void) | null = null;
  private starting = false;

  constructor() {
    effect(() => {
      const wanted = !this.motion.reducedMotion() && supportsWebGl();
      this.active.set(wanted);
    });

    // The canvas only exists once `active` flips, so start after the view syncs.
    effect(() => {
      const canvas = this.canvasRef()?.nativeElement;
      if (!canvas || this.teardown || this.starting) {
        if (!this.active()) this.stop();
        return;
      }
      void this.start(canvas);
    });

    // Repaint node/link colors when the user switches theme mid-session.
    effect(() => {
      this.theme.theme();
      this.applyTheme?.();
    });
  }

  ngOnDestroy() {
    this.stop();
  }

  private async start(canvas: HTMLCanvasElement) {
    this.starting = true;
    try {
      const THREE = await import('three');
      // A teardown may have been requested while the chunk was loading.
      if (!this.active() || !canvas.isConnected) return;

      const renderer = new THREE.WebGLRenderer({
        canvas,
        alpha: true,
        antialias: false,
        powerPreference: 'low-power'
      });
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO));

      const scene = new THREE.Scene();
      const camera = new THREE.PerspectiveCamera(58, 1, 0.1, 100);
      camera.position.z = 13;

      const positions = new Float32Array(NODE_COUNT * 3);
      for (let i = 0; i < NODE_COUNT; i++) {
        positions[i * 3] = (Math.random() - 0.5) * FIELD * 1.9;
        positions[i * 3 + 1] = (Math.random() - 0.5) * FIELD;
        positions[i * 3 + 2] = (Math.random() - 0.5) * FIELD;
      }

      const nodeGeometry = new THREE.BufferGeometry();
      nodeGeometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
      const nodeMaterial = new THREE.PointsMaterial({
        size: 0.075,
        transparent: true,
        opacity: 0.78,
        depthWrite: false
      });
      const nodes = new THREE.Points(nodeGeometry, nodeMaterial);

      // Links are computed once: the field only rotates, so pair distances
      // never change and there is nothing to recompute per frame.
      const linkPositions: number[] = [];
      for (let a = 0; a < NODE_COUNT; a++) {
        for (let b = a + 1; b < NODE_COUNT; b++) {
          const dx = positions[a * 3] - positions[b * 3];
          const dy = positions[a * 3 + 1] - positions[b * 3 + 1];
          const dz = positions[a * 3 + 2] - positions[b * 3 + 2];
          if (dx * dx + dy * dy + dz * dz > LINK_DISTANCE * LINK_DISTANCE) continue;
          linkPositions.push(
            positions[a * 3], positions[a * 3 + 1], positions[a * 3 + 2],
            positions[b * 3], positions[b * 3 + 1], positions[b * 3 + 2]
          );
        }
      }
      const linkGeometry = new THREE.BufferGeometry();
      linkGeometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(linkPositions), 3));
      const linkMaterial = new THREE.LineBasicMaterial({ transparent: true, opacity: 0.17, depthWrite: false });
      const links = new THREE.LineSegments(linkGeometry, linkMaterial);

      const field = new THREE.Group();
      field.add(nodes, links);
      scene.add(field);

      this.applyTheme = () => {
        const styles = getComputedStyle(this.host.nativeElement);
        nodeMaterial.color.set(readColor(styles, '--scene-node', '#6fe2d7'));
        linkMaterial.color.set(readColor(styles, '--scene-link', '#4fb4ff'));
      };
      this.applyTheme();

      const resize = () => {
        const { clientWidth, clientHeight } = this.host.nativeElement;
        if (!clientWidth || !clientHeight) return;
        camera.aspect = clientWidth / clientHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(clientWidth, clientHeight, false);
      };
      resize();

      let pointerX = 0;
      let pointerY = 0;
      const onPointerMove = (event: PointerEvent) => {
        pointerX = (event.clientX / window.innerWidth - 0.5) * 0.4;
        pointerY = (event.clientY / window.innerHeight - 0.5) * 0.25;
      };

      let frame = 0;
      const render = () => {
        // Two cheap rotations plus an eased parallax -- no physics, no
        // per-frame allocation, no post-processing.
        field.rotation.y += 0.0011;
        field.rotation.x += 0.0004;
        camera.position.x += (pointerX * 2.4 - camera.position.x) * 0.035;
        camera.position.y += (-pointerY * 2.4 - camera.position.y) * 0.035;
        camera.lookAt(0, 0, 0);
        renderer.render(scene, camera);
        frame = requestAnimationFrame(render);
      };

      const pause = () => {
        if (frame) cancelAnimationFrame(frame);
        frame = 0;
      };
      const resume = () => {
        if (!frame) frame = requestAnimationFrame(render);
      };
      const onVisibility = () => (document.hidden ? pause() : resume());

      // Outside Angular: an animation frame must never schedule change detection.
      this.zone.runOutsideAngular(() => {
        window.addEventListener('resize', resize);
        window.addEventListener('pointermove', onPointerMove, { passive: true });
        document.addEventListener('visibilitychange', onVisibility);
        resume();
      });

      canvas.classList.add('hub-scene__canvas--ready');

      this.teardown = () => {
        pause();
        window.removeEventListener('resize', resize);
        window.removeEventListener('pointermove', onPointerMove);
        document.removeEventListener('visibilitychange', onVisibility);
        nodeGeometry.dispose();
        linkGeometry.dispose();
        nodeMaterial.dispose();
        linkMaterial.dispose();
        renderer.dispose();
        this.applyTheme = null;
      };
    } catch {
      // No WebGL context, a blocked chunk, or a driver failure: the CSS
      // backdrop stays and the Hub is unaffected.
      this.active.set(false);
    } finally {
      this.starting = false;
    }
  }

  private stop() {
    this.teardown?.();
    this.teardown = null;
  }
}

/**
 * Cheap capability probe. The constructor check runs first so environments
 * without WebGL at all (jsdom, locked-down browsers) short-circuit before
 * requesting a context; a thrown or null context also means no WebGL.
 */
function supportsWebGl(): boolean {
  if (typeof window === 'undefined' || typeof document === 'undefined') return false;
  if (!('WebGL2RenderingContext' in window) && !('WebGLRenderingContext' in window)) return false;
  try {
    const probe = document.createElement('canvas');
    return !!(probe.getContext('webgl2') || probe.getContext('webgl'));
  } catch {
    return false;
  }
}

function readColor(styles: CSSStyleDeclaration, token: string, fallback: string): string {
  return styles.getPropertyValue(token).trim() || fallback;
}
