import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JsonTreeComponent } from '../../../shared/ui';
import { EnvironmentDto, ModuleEndpoint, SendOrderResult } from '../../../core/models';

/**
 * U4 (UI_Rework_Plan.md D13): the send area shows the active environment's
 * resolved endpoint read-only (from GET modules/{key}/endpoint -- the
 * module catalog deliberately never carries URLs, B16) and loading is the
 * real request lifecycle driven by the parent, not a fixed timeout. Endpoint
 * authority remains entirely on the server; this component emits only the
 * send action and never accepts a target URL.
 */
@Component({
  selector: 'app-api-config',
  standalone: true,
  imports: [CommonModule, JsonTreeComponent],
  template: `
    <section class="card-section" [class.embedded]="embedded">
      <div class="card-title" *ngIf="!embedded">
        <i class="bi bi-send"></i>
        <span>API Request Execution</span>
        <span class="env-tag" [class.env-tag--prod]="isProduction()" *ngIf="environment">
          <i class="bi" [class.bi-exclamation-triangle-fill]="isProduction()" [class.bi-shield-check]="!isProduction()"></i>
          {{ environment.environment === 'Production' ? 'PROD' : 'TEST' }} · {{ environment.key }}
        </span>
      </div>

      <div class="form-group mb-3">
        <label class="form-label" for="endpoint-url">Target API URL</label>
        <input
          id="endpoint-url"
          type="text"
          class="token-input"
          [value]="endpoint?.apiUrl || ''"
          [placeholder]="endpoint ? 'No endpoint configured for this environment.' : 'Resolving endpoint…'"
          readonly
          aria-readonly="true" />
      </div>

      <button type="button" class="send-button" *ngIf="showSend" [disabled]="loading" (click)="onSend()">
        <i class="bi" [class.bi-send]="!loading" [class.bi-arrow-repeat]="loading" [class.spin]="loading"></i>
        <span>{{ loading ? 'Sending...' : 'Send Order Request' }}</span>
      </button>

      <div class="validation-summary" *ngIf="validationSummary as summary" role="alert">
        <div class="validation-header">
          <i class="bi bi-exclamation-circle"></i>
          <span>{{ summary.totalCount }} validation issue(s) — review the highlighted fields above.</span>
        </div>
        <ul class="validation-list" *ngIf="summary.globalErrors.length > 0">
          <li *ngFor="let message of summary.globalErrors">{{ message }}</li>
        </ul>
      </div>

      <div class="json-preview mb-4">
        <app-json-tree [data]="compiledJson" title="Compiled JSON Payload"></app-json-tree>
      </div>

      <div class="response-section" *ngIf="apiResponse">
        <div class="response-header">
          <span>API Response</span>
          <span class="badge" [class.badge-success]="apiResponse.success" [class.badge-danger]="!apiResponse.success">
            Status: {{ apiResponse.statusCode }}
          </span>
        </div>
        <app-json-tree [data]="apiResponse.responseText" title="Raw Response Body"></app-json-tree>
      </div>
    </section>
  `,
  styles: [`
    .card-section { padding: var(--panel-padding); margin-bottom: var(--section-gap); background: var(--surface-panel); border: 1px solid var(--border-subtle); border-radius: var(--panel-radius); box-shadow: var(--shadow-sm); }
    .card-section.embedded { padding: 0; margin: 0; background: transparent; border: 0; box-shadow: none; }
    .card-title { display: flex; align-items: center; gap: var(--panel-gap); font-size: 1.1rem; font-weight: 600; margin-bottom: var(--panel-gap); color: var(--text-primary); }
    .card-title i { color: var(--accent); }
    .env-tag {
      margin-left: auto; display: flex; align-items: center; gap: 6px;
      font-size: 0.75rem; font-weight: 700; letter-spacing: 0.03em;
      padding: 4px 10px; border-radius: var(--radius-pill);
      background: var(--state-info-bg); color: var(--state-info-fg);
    }
    .env-tag--prod { background: var(--state-danger-bg); color: var(--state-danger-fg); }
    .token-input { width: 100%; box-sizing: border-box; min-height: var(--control-height); padding: 0 var(--panel-padding-compact); border: 1px solid var(--input-border); border-radius: var(--radius-md); background: var(--input-bg); color: var(--text-primary); font: inherit; }
    .token-input:focus { outline: none; border-color: var(--border-focus); box-shadow: var(--focus-ring); }
    .send-button { display: inline-flex; align-items: center; gap: 8px; min-height: var(--control-height); margin-bottom: var(--panel-gap); padding: 0 var(--panel-padding); border: 1px solid transparent; border-radius: var(--radius-md); background: var(--accent); color: var(--text-inverse); cursor: pointer; font: inherit; font-weight: 750; }
    .send-button:hover:not(:disabled) { background: var(--accent-hover); }
    .send-button:focus-visible { outline: none; box-shadow: var(--focus-ring); }
    .send-button:disabled { opacity: .55; cursor: not-allowed; }
    .spin { animation: spin 1s infinite linear; }
    @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    .validation-summary {
      border: 1px solid var(--state-danger-border); border-radius: var(--radius-md);
      background: var(--state-danger-bg); padding: 12px 16px; margin-bottom: 16px;
    }
    .validation-header { display: flex; align-items: center; gap: 8px; font-weight: 600; color: var(--state-danger-fg); font-size: 0.9rem; }
    .validation-list { margin: 8px 0 0; padding-left: 20px; color: var(--text-primary); font-size: 0.85rem; }
    .response-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; font-weight: 600; }
    .badge { padding: 4px 8px; border-radius: var(--radius-sm); font-size: 0.8rem; }
    .badge-success { background: var(--state-success-bg); color: var(--state-success-fg); }
    .badge-danger { background: var(--state-danger-bg); color: var(--state-danger-fg); }
  `]
})
export class ApiConfigComponent {
  @Input() compiledJson: Record<string, unknown> | null = null;
  @Input() apiResponse: SendOrderResult | null = null;
  /** Real request-lifecycle state owned by the parent (set before the send
   * request, cleared in finalize) -- replaces the fixed 2.5s spinner. */
  @Input() loading: boolean = false;
  /** The active environment's resolved endpoint (read-only display). */
  @Input() endpoint: ModuleEndpoint | null = null;
  @Input() environment: EnvironmentDto | null = null;
  /** Server validation outcome of the last send attempt (U4): issue count
   * plus the errors that have no field/section home. */
  @Input() validationSummary: { totalCount: number, globalErrors: string[] } | null = null;
  /** Flat-order U6 owns the send action in the summary rail; other consumers
   * keep the legacy local send button by default. */
  @Input() showSend = true;
  @Input() embedded = false;

  @Output() sendRequest = new EventEmitter<void>();

  isProduction(): boolean {
    return this.environment?.environment === 'Production';
  }

  onSend() {
    this.sendRequest.emit();
  }
}
