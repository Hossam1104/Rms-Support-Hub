import { CommonModule } from '@angular/common';
import { Component, inject, signal, WritableSignal } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { components } from '../../core/pos-agent/generated/pos-agent-api.generated';
import { PosAgentTransportError, classifyPosAgentError } from '../../core/pos-agent/pos-agent-error';
import { PosAgentTransportService } from '../../core/pos-agent/pos-agent-transport.service';
import { POS_AGENT_OPERATION_IDS } from '../../core/pos-agent/pos-agent.constants';
import { ToastService } from '../../core/services/toast.service';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import {
  ConfirmDialogComponent,
  EmptyStateComponent,
  PageHeaderComponent,
  SkeletonComponent,
  UiButtonComponent,
  UiCardComponent
} from '../../shared/ui';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

type HealthStatus = components['schemas']['HealthStatusDto'];
type SessionInfo = components['schemas']['SessionInfoDto'];
type DeviceIdentity = components['schemas']['DeviceIdentityDto'];
type DeviceConnectivity = components['schemas']['DeviceConnectivityDto'];
type DeviceCapabilities = components['schemas']['DeviceCapabilitiesDto'];
type RedactedConfiguration = components['schemas']['RedactedConfigurationDto'];
type ServiceSummary = components['schemas']['ServiceSummaryDto'];
type ServiceActionKind = components['schemas']['ServiceActionKind'];
type ServiceActionOutcome = components['schemas']['ServiceActionOutcome'];
type ServiceActionResponse = components['schemas']['ServiceActionResponseDto'];
type Evidence = components['schemas']['EvidenceDto'];
type AgentState = 'loading' | 'reachable' | 'unreachable';
type AuthState = 'loading' | 'authenticated' | 'authentication-required' | 'unavailable';
type Settled<T> = { readonly ok: true; readonly value: T } | { readonly ok: false; readonly error: PosAgentTransportError };
type PendingServiceAction = Readonly<{ service: ServiceSummary; action: ServiceActionKind }>;

@Component({
  selector: 'app-pos-maintenance',
  standalone: true,
  imports: [
    CommonModule,
    NavbarComponent,
    PageHeaderComponent,
    StatusBadgeComponent,
    UiButtonComponent,
    UiCardComponent,
    EmptyStateComponent,
    SkeletonComponent,
    ConfirmDialogComponent
  ],
  template: `
    <app-navbar></app-navbar>

    <main class="pos-page" aria-label="POS Maintenance service control and evidence">
      <app-page-header
        title="POS Maintenance - service control and evidence"
        subtitle="Direct, credentialed reads from the local Windows POS Agent, with narrowly scoped controls for authorized allow-listed Windows services.">
        <ui-button
          variant="secondary"
          size="sm"
          icon="bi-arrow-clockwise"
          [loading]="refreshing()"
          loadingLabel="Refreshing"
          [ariaLabel]="refreshing() ? 'Refreshing POS Agent data' : 'Refresh POS Agent data'"
          (pressed)="refresh()">
          Refresh reads
        </ui-button>
      </app-page-header>

      @if (globalError()) {
        <section class="notice notice--danger" role="alert" aria-label="POS Agent read warning">
          <i class="bi bi-exclamation-triangle" aria-hidden="true"></i>
          <div>
            <strong>Some POS Agent reads are unavailable.</strong>
            <p>{{ globalError() }}</p>
          </div>
        </section>
      }

      <section class="status-grid" aria-label="POS Agent access status">
        <ui-card variant="raised" class="status-card status-card--agent">
          <div uiCardHeader class="card-heading">
            <span class="card-icon" aria-hidden="true"><i class="bi bi-pc-display"></i></span>
            <div>
              <p class="eyebrow">Agent process</p>
              <h2>Local Agent</h2>
            </div>
          </div>
          @if (loading()) {
            <app-skeleton height="18px" width="9rem"></app-skeleton>
          } @else {
            <app-status-badge [label]="agentStatusLabel()" [variant]="agentStatusVariant()" role="status"></app-status-badge>
            <p class="status-copy">{{ agentStatusDetail() }}</p>
          }
        </ui-card>

        <ui-card variant="raised" class="status-card">
          <div uiCardHeader class="card-heading">
            <span class="card-icon" aria-hidden="true"><i class="bi bi-shield-lock"></i></span>
            <div>
              <p class="eyebrow">Windows access</p>
              <h2>Authentication & authorization</h2>
            </div>
          </div>
          @if (loading()) {
            <app-skeleton height="18px" width="12rem"></app-skeleton>
          } @else {
            <div class="badge-stack">
              <app-status-badge [label]="authStatusLabel()" [variant]="authStatusVariant()" role="status"></app-status-badge>
              <app-status-badge [label]="authorizationLabel()" [variant]="authorizationVariant()" role="status"></app-status-badge>
            </div>
            <p class="status-copy">{{ authStatusDetail() }}</p>
          }
        </ui-card>

        <ui-card variant="raised" class="status-card">
          <div uiCardHeader class="card-heading">
            <span class="card-icon" aria-hidden="true"><i class="bi bi-braces"></i></span>
            <div>
              <p class="eyebrow">Contract</p>
              <h2>Agent/API version</h2>
            </div>
          </div>
          @if (loading()) {
            <app-skeleton height="18px" width="10rem"></app-skeleton>
          } @else {
            <dl class="compact-list">
              <div><dt>Agent</dt><dd>{{ agentVersion() }}</dd></div>
              <div><dt>API</dt><dd>{{ apiVersion() }}</dd></div>
            </dl>
            <p class="status-copy">Version metadata comes from the Agent response, not from browser configuration.</p>
          }
        </ui-card>
      </section>

      <section class="workspace-grid" aria-label="POS Agent evidence and service-control workspace">
        <ui-card variant="raised" class="workspace-card identity-card">
          <div uiCardHeader class="card-heading card-heading--split">
            <div class="card-heading__copy">
              <p class="eyebrow">Device identity</p>
              <h2>Local POS</h2>
            </div>
            <i class="bi bi-shop-window card-heading__mark" aria-hidden="true"></i>
          </div>
          @if (loading()) {
            <div class="skeleton-stack"><app-skeleton></app-skeleton><app-skeleton></app-skeleton><app-skeleton></app-skeleton></div>
          } @else if (identity(); as value) {
            <dl class="data-list">
              <div><dt>Branch</dt><dd>{{ value.branchCode }}</dd></div>
              <div><dt>POS number</dt><dd>{{ value.posNumber }}</dd></div>
              <div><dt>Release</dt><dd>{{ value.release }}</dd></div>
              <div><dt>Client</dt><dd>{{ value.clientName }}</dd></div>
            </dl>
          } @else {
            <app-empty-state icon="bi-pc-display" title="Identity is unavailable" [description]="readError('identity')"></app-empty-state>
          }
        </ui-card>

        <ui-card variant="raised" class="workspace-card connectivity-card">
          <div uiCardHeader class="card-heading card-heading--split">
            <div class="card-heading__copy">
              <p class="eyebrow">Connectivity evidence</p>
              <h2>Reachability, not health claims</h2>
            </div>
            <i class="bi bi-diagram-3 card-heading__mark" aria-hidden="true"></i>
          </div>
          @if (loading()) {
            <div class="skeleton-stack"><app-skeleton height="48px"></app-skeleton><app-skeleton height="48px"></app-skeleton></div>
          } @else if (connectivity(); as value) {
            <div class="evidence-grid">
              <div class="evidence-item">
                <div class="evidence-item__heading"><strong>Local SQL</strong><app-status-badge [label]="freshnessLabel(value.localSql)" [variant]="freshnessVariant(value.localSql)" role="status"></app-status-badge></div>
                <p>{{ value.localSql.detail }}</p>
                <small>Checked {{ checkedAt(value.localSql) }}</small>
              </div>
              <div class="evidence-item">
                <div class="evidence-item__heading"><strong>Main server</strong><app-status-badge [label]="freshnessLabel(value.mainServer)" [variant]="freshnessVariant(value.mainServer)" role="status"></app-status-badge></div>
                <p>{{ value.mainServer.detail }}</p>
                <small>Checked {{ checkedAt(value.mainServer) }}</small>
              </div>
            </div>
          } @else {
            <app-empty-state icon="bi-diagram-3" title="Connectivity evidence is unavailable" [description]="readError('connectivity')"></app-empty-state>
          }
        </ui-card>

        <ui-card variant="raised" class="workspace-card capabilities-card">
          <div uiCardHeader class="card-heading card-heading--split">
            <div class="card-heading__copy">
              <p class="eyebrow">Agent capabilities</p>
              <h2>Safe metadata</h2>
            </div>
            <i class="bi bi-list-check card-heading__mark" aria-hidden="true"></i>
          </div>
          @if (loading()) {
            <div class="skeleton-stack"><app-skeleton></app-skeleton><app-skeleton></app-skeleton></div>
          } @else if (capabilities(); as value) {
            <dl class="data-list">
              <div><dt>Agent version</dt><dd>{{ value.agentVersion }}</dd></div>
              <div><dt>Operating system</dt><dd>{{ value.operatingSystem }}</dd></div>
              <div><dt>Browse roots</dt><dd>{{ value.browseRoots.length ? value.browseRoots.length + ' display entries' : 'None published' }}</dd></div>
            </dl>
            <p class="boundary-copy"><i class="bi bi-lock" aria-hidden="true"></i> Host paths and file-browse operations stay server-owned.</p>
          } @else {
            <app-empty-state icon="bi-list-check" title="Capabilities are unavailable" [description]="readError('capabilities')"></app-empty-state>
          }
        </ui-card>

        <ui-card variant="raised" class="workspace-card configuration-card">
          <div uiCardHeader class="card-heading card-heading--split">
            <div class="card-heading__copy">
              <p class="eyebrow">Configuration</p>
              <h2>Redacted view</h2>
            </div>
            <i class="bi bi-sliders2-vertical card-heading__mark" aria-hidden="true"></i>
          </div>
          @if (loading()) {
            <div class="skeleton-stack"><app-skeleton></app-skeleton><app-skeleton></app-skeleton><app-skeleton></app-skeleton></div>
          } @else if (configuration(); as value) {
            <dl class="data-list">
              <div><dt>Branch / POS</dt><dd>{{ value.branchCode }} · {{ value.posNumber }}</dd></div>
              <div><dt>SQL user</dt><dd>{{ value.sqlUser || 'Not configured' }}</dd></div>
              <div><dt>SQL password</dt><dd>{{ presenceLabel(value.hasSqlPassword) }}</dd></div>
              <div><dt>RDB password</dt><dd>{{ presenceLabel(value.downloader.hasRdbPassword) }}</dd></div>
              <div><dt>Configured databases</dt><dd>{{ value.databases.length }}</dd></div>
              <div><dt>Configured services</dt><dd>{{ value.services.length }}</dd></div>
            </dl>
            <p class="boundary-copy"><i class="bi bi-shield-check" aria-hidden="true"></i> Password values, protected paths, and source locations are never returned.</p>
          } @else {
            <app-empty-state icon="bi-sliders2-vertical" title="Configuration is unavailable" [description]="readError('configuration')"></app-empty-state>
          }
        </ui-card>

        <ui-card variant="raised" class="workspace-card services-card">
          <div uiCardHeader class="card-heading card-heading--split">
            <div class="card-heading__copy">
              <p class="eyebrow">Windows services</p>
              <h2>Status and controls</h2>
            </div>
            <i class="bi bi-gear-wide-connected card-heading__mark" aria-hidden="true"></i>
          </div>
          @if (loading()) {
            <div class="skeleton-stack"><app-skeleton height="44px"></app-skeleton><app-skeleton height="44px"></app-skeleton></div>
          } @else if (services(); as values) {
            @if (values.length) {
              <div class="service-list" role="list" aria-label="Configured Windows services">
                @for (service of values; track service.serviceId) {
                  <div class="service-row" role="listitem">
                    <div class="service-row__copy"><strong>{{ service.displayName }}</strong><small>{{ service.lastChecked.detail }}</small></div>
                    <div class="service-row__actions">
                      <app-status-badge [label]="serviceStateLabel(service.state)" [variant]="serviceStateVariant(service.state)" role="status"></app-status-badge>
                      @if (canControlServices() && service.allowedActions.length) {
                        <div class="service-action-group" role="group" [attr.aria-label]="'Actions for ' + service.displayName">
                          @for (action of service.allowedActions; track action) {
                            <ui-button
                              variant="secondary"
                              size="sm"
                              [loading]="isActionSubmitting(service.serviceId, action)"
                              [disabled]="submittingAction() !== null && !isActionSubmitting(service.serviceId, action)"
                              [ariaLabel]="actionLabel(action) + ' ' + service.displayName"
                              (pressed)="requestServiceAction(service, action)">
                              {{ actionLabel(action) }}
                            </ui-button>
                          }
                        </div>
                      }
                    </div>
                    @if (actionOutcome(service); as outcome) {
                      <p class="service-row__outcome" [class.service-row__outcome--warning]="outcome.outcome === 'outcomeUnknown' || outcome.outcome === 'notAttempted'" [class.service-row__outcome--danger]="outcome.outcome === 'failed'" role="status">
                        {{ actionOutcomeLabel(outcome.outcome) }}: {{ outcome.detail }}
                      </p>
                    }
                  </div>
                }
              </div>
            } @else {
              <app-empty-state icon="bi-gear-wide-connected" title="No services are configured" description="The Agent returned an empty allow-list."></app-empty-state>
            }
            <p class="boundary-copy"><i class="bi bi-shield-check" aria-hidden="true"></i> Controls appear only for an authorized local Administrator and only for the state-valid actions returned by the Agent.</p>
          } @else {
            <app-empty-state icon="bi-gear-wide-connected" title="Service status is unavailable" [description]="readError('services')"></app-empty-state>
          }
        </ui-card>
      </section>

      <section class="boundary-banner" aria-label="POS Agent mutation boundary">
        <div class="boundary-banner__icon" aria-hidden="true"><i class="bi bi-shield-lock"></i></div>
        <div>
          <p class="eyebrow">INT-08 boundary</p>
          <h2>Typed service controls with safe outcome truth</h2>
          <p>Start, stop, and restart use an explicit confirmation, a short-lived one-use Agent token, and a bounded idempotency key. Configuration writes, backup/restore, file browsing, SQL execution, and generic command execution remain outside this surface. An unknown action outcome is never retried automatically.</p>
        </div>
      </section>
    </main>

    @if (pendingAction(); as pending) {
      <app-confirm-dialog
        variant="danger"
        [title]="'Confirm ' + actionLabel(pending.action).toLowerCase() + ' service'"
        [message]="confirmationMessage(pending)"
        confirmLabel="Continue"
        cancelLabel="Cancel"
        (cancel)="cancelPendingAction()"
        (confirm)="executePendingAction()">
      </app-confirm-dialog>
    }
  `,
  styles: [`
    :host { display: block; min-height: 100vh; background: var(--scene-backdrop), var(--surface-page); }
    .pos-page { width: min(100%, 1380px); box-sizing: border-box; margin: 0 auto; padding: calc(var(--navbar-height) + var(--page-padding-block)) var(--page-padding-inline) var(--section-gap); }
    .status-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--card-gap); margin-bottom: var(--section-gap); }
    .workspace-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--card-gap); }
    .workspace-card { min-width: 0; }
    .card-heading { display: flex; align-items: center; gap: var(--space-3); }
    .card-heading--split { justify-content: space-between; align-items: flex-start; }
    .card-heading__copy { min-width: 0; }
    .card-heading__mark { color: var(--text-accent); font-size: 1.25rem; }
    .card-icon { display: grid; flex: 0 0 38px; width: 38px; height: 38px; place-items: center; border-radius: var(--radius-md); background: var(--state-info-bg); color: var(--state-info-fg); }
    .eyebrow { margin: 0 0 var(--space-1); color: var(--text-accent); font-size: var(--text-xs); font-weight: var(--weight-bold); letter-spacing: .08em; text-transform: uppercase; }
    h2 { margin: 0; color: var(--text-primary); font-size: var(--text-lg); line-height: var(--leading-tight); }
    .status-card .ui-card__body { min-height: 132px; }
    .status-copy { margin: var(--space-3) 0 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .badge-stack { display: flex; flex-wrap: wrap; gap: var(--space-2); }
    .compact-list, .data-list { display: grid; gap: var(--space-3); margin: 0; }
    .compact-list div, .data-list div { display: flex; align-items: baseline; justify-content: space-between; gap: var(--space-4); border-bottom: 1px solid var(--divider); padding-bottom: var(--space-2); }
    .compact-list div:last-child, .data-list div:last-child { border-bottom: 0; padding-bottom: 0; }
    dt { color: var(--text-muted); font-size: var(--text-xs); font-weight: var(--weight-semibold); }
    dd { margin: 0; color: var(--text-primary); font-size: var(--text-sm); font-weight: var(--weight-semibold); text-align: right; overflow-wrap: anywhere; }
    .skeleton-stack { display: grid; gap: var(--space-3); }
    .skeleton-stack app-skeleton { min-height: 16px; }
    .evidence-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--card-gap); }
    .evidence-item { min-width: 0; padding: var(--space-3); border: 1px solid var(--card-border); border-radius: var(--radius-md); background: var(--surface-raised); }
    .evidence-item__heading { display: flex; align-items: center; justify-content: space-between; gap: var(--space-2); }
    .evidence-item strong { color: var(--text-primary); font-size: var(--text-sm); }
    .evidence-item p { margin: var(--space-2) 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    .evidence-item small, .service-row small { color: var(--text-muted); font-size: var(--text-xs); }
    .boundary-copy { display: flex; align-items: flex-start; gap: var(--space-2); margin: var(--space-4) 0 0; color: var(--text-muted); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .boundary-copy i { flex: 0 0 auto; color: var(--text-accent); }
    .service-list { display: grid; gap: var(--space-2); }
    .service-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: var(--space-3); padding: var(--space-3); border: 1px solid var(--card-border); border-radius: var(--radius-md); background: var(--surface-raised); }
    .service-row__copy { display: grid; min-width: 0; gap: var(--space-1); }
    .service-row strong { color: var(--text-primary); font-size: var(--text-sm); overflow-wrap: anywhere; }
    .service-row__actions { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: var(--space-2); }
    .service-action-group { display: flex; align-items: center; flex-wrap: wrap; gap: var(--space-2); }
    .service-row__outcome { grid-column: 1 / -1; margin: 0; color: var(--state-success-fg); font-size: var(--text-xs); line-height: var(--leading-normal); }
    .service-row__outcome--warning { color: var(--state-warning-fg); }
    .service-row__outcome--danger { color: var(--state-danger-fg); }
    .notice, .boundary-banner { display: flex; align-items: flex-start; gap: var(--space-3); padding: var(--panel-padding); border: 1px solid var(--state-danger-border); border-radius: var(--radius-lg); background: var(--state-danger-bg); color: var(--state-danger-fg); margin-bottom: var(--section-gap); }
    .notice > i { flex: 0 0 auto; margin-top: 2px; }
    .notice strong { color: var(--text-primary); }
    .notice p { margin: var(--space-1) 0 0; color: var(--text-secondary); font-size: var(--text-sm); }
    .boundary-banner { margin-top: var(--section-gap); margin-bottom: 0; border-color: var(--state-info-border); background: var(--state-info-bg); color: var(--state-info-fg); }
    .boundary-banner__icon { display: grid; flex: 0 0 38px; width: 38px; height: 38px; place-items: center; border-radius: var(--radius-md); background: var(--surface-raised); color: var(--state-info-fg); }
    .boundary-banner h2 { margin-bottom: var(--space-2); }
    .boundary-banner p:last-child { max-width: 78ch; margin: 0; color: var(--text-secondary); font-size: var(--text-sm); line-height: var(--leading-normal); }
    @media (max-width: 1000px) { .status-grid { grid-template-columns: 1fr; } .workspace-grid { grid-template-columns: 1fr; } }
    @media (max-width: 680px) { .pos-page { padding-inline: var(--space-4); } .evidence-grid { grid-template-columns: 1fr; } .service-row { grid-template-columns: 1fr; align-items: flex-start; } .service-row__actions { justify-content: flex-start; } }
  `]
})
export class PosMaintenanceComponent {
  private readonly transport = inject(PosAgentTransportService);
  private readonly toast = inject(ToastService);

  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly agentStatus = signal<AgentState>('loading');
  readonly authStatus = signal<AuthState>('loading');
  readonly session = signal<SessionInfo | null>(null);
  readonly identity = signal<DeviceIdentity | null>(null);
  readonly connectivity = signal<DeviceConnectivity | null>(null);
  readonly capabilities = signal<DeviceCapabilities | null>(null);
  readonly configuration = signal<RedactedConfiguration | null>(null);
  readonly services = signal<ServiceSummary[] | null>(null);
  readonly globalError = signal<string | null>(null);
  readonly pendingAction = signal<PendingServiceAction | null>(null);
  readonly submittingAction = signal<string | null>(null);

  private readonly errors = signal<Record<string, string>>({});
  private readonly actionOutcomes = signal<Record<string, ServiceActionResponse>>({});

  constructor() {
    void this.load();
  }

  async refresh(): Promise<void> {
    if (this.refreshing()) return;
    await this.load();
  }

  canControlServices(): boolean {
    return this.session()?.isAuthorized === true;
  }

  requestServiceAction(service: ServiceSummary, action: ServiceActionKind): void {
    if (!this.canControlServices()
      || !service.allowedActions.includes(action)
      || this.submittingAction() !== null) {
      return;
    }

    this.pendingAction.set({ service, action });
  }

  cancelPendingAction(): void {
    this.pendingAction.set(null);
  }

  actionLabel(action: ServiceActionKind): string {
    switch (action) {
      case 'start': return 'Start';
      case 'stop': return 'Stop';
      case 'restart': return 'Restart';
    }
  }

  confirmationMessage(pending: PendingServiceAction): string {
    return `${this.actionLabel(pending.action)} ${pending.service.displayName}? The Agent will check the current service state before dispatching this typed action.`;
  }

  isActionSubmitting(serviceId: string, action: ServiceActionKind): boolean {
    return this.submittingAction() === this.actionKey(serviceId, action);
  }

  actionOutcome(service: ServiceSummary): ServiceActionResponse | null {
    return this.actionOutcomes()[service.serviceId] ?? null;
  }

  actionOutcomeLabel(outcome: ServiceActionOutcome): string {
    switch (outcome) {
      case 'accepted': return 'Accepted';
      case 'failed': return 'Failed';
      case 'outcomeUnknown': return 'Outcome unknown';
      default: return 'Not attempted';
    }
  }

  async executePendingAction(): Promise<void> {
    const pending = this.pendingAction();
    if (!pending || !this.canControlServices() || !pending.service.allowedActions.includes(pending.action)) {
      this.pendingAction.set(null);
      return;
    }

    this.pendingAction.set(null);
    const operationKey = this.actionKey(pending.service.serviceId, pending.action);
    this.submittingAction.set(operationKey);
    const idempotencyKey = this.createIdempotencyKey();

    try {
      const issued = await firstValueFrom(
        this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.serviceControl, pending.service.serviceId)
      );
      const response = await firstValueFrom(
        this.transport.controlService(
          pending.service.serviceId,
          pending.action,
          idempotencyKey,
          issued.token
        )
      );
      this.actionOutcomes.update(outcomes => ({ ...outcomes, [pending.service.serviceId]: response }));
      this.notifyActionOutcome(response);
      await this.load();
    } catch (error) {
      this.toast.showError(this.actionErrorMessage(classifyPosAgentError(error)));
    } finally {
      if (this.submittingAction() === operationKey) {
        this.submittingAction.set(null);
      }
    }
  }

  agentStatusLabel(): string {
    switch (this.agentStatus()) {
      case 'reachable': return 'Reachable';
      case 'unreachable': return 'Unavailable';
      default: return 'Checking';
    }
  }

  agentStatusVariant(): 'success' | 'danger' | 'info' {
    return this.agentStatus() === 'reachable' ? 'success' : this.agentStatus() === 'unreachable' ? 'danger' : 'info';
  }

  agentStatusDetail(): string {
    return this.agentStatus() === 'reachable'
      ? 'The anonymous liveness endpoint answered directly.'
      : this.agentStatus() === 'unreachable'
        ? this.readError('agent')
        : 'Checking the fixed HTTPS loopback endpoint.';
  }

  authStatusLabel(): string {
    switch (this.authStatus()) {
      case 'authenticated': return 'Windows authenticated';
      case 'authentication-required': return 'Authentication required';
      case 'unavailable': return 'Authentication unavailable';
      default: return 'Checking authentication';
    }
  }

  authStatusVariant(): 'success' | 'danger' | 'info' {
    return this.authStatus() === 'authenticated' ? 'success' : this.authStatus() === 'loading' ? 'info' : 'danger';
  }

  authStatusDetail(): string {
    if (this.authStatus() === 'authenticated') return this.session()?.principalName || 'The Agent resolved the Windows principal.';
    if (this.authStatus() === 'authentication-required') return 'The browser/OS did not complete the Negotiate handshake.';
    if (this.authStatus() === 'unavailable') return this.readError('session');
    return 'Checking the Windows Negotiate session.';
  }

  authorizationLabel(): string {
    const authorized = this.session()?.isAuthorized;
    return authorized === true ? 'Local Administrator authorized' : authorized === false ? 'Local Administrator not authorized' : 'Authorization unknown';
  }

  authorizationVariant(): 'success' | 'danger' | 'info' {
    const authorized = this.session()?.isAuthorized;
    return authorized === true ? 'success' : authorized === false ? 'danger' : 'info';
  }

  agentVersion(): string {
    return this.session()?.agentVersion || this.capabilities()?.agentVersion || 'Unavailable';
  }

  apiVersion(): string {
    return this.session()?.apiVersion || 'Unavailable';
  }

  freshnessLabel(evidence: Evidence): string {
    return evidence.freshness === 'fresh' ? 'Fresh' : evidence.freshness === 'stale' ? 'Stale' : 'Unknown';
  }

  freshnessVariant(evidence: Evidence): 'success' | 'warning' | 'info' {
    return evidence.freshness === 'fresh' ? 'success' : evidence.freshness === 'stale' ? 'warning' : 'info';
  }

  checkedAt(evidence: Evidence): string {
    if (!evidence.lastCheckedUtc) return 'not available';
    const date = new Date(evidence.lastCheckedUtc);
    return Number.isNaN(date.getTime()) ? 'not available' : date.toLocaleString();
  }

  serviceStateLabel(state: components['schemas']['ServiceRuntimeState']): string {
    switch (state) {
      case 'running': return 'Running';
      case 'stopped': return 'Stopped';
      case 'transitioning': return 'Transitioning';
      case 'notFound': return 'Not found';
      default: return 'Unknown';
    }
  }

  serviceStateVariant(state: components['schemas']['ServiceRuntimeState']): 'success' | 'warning' | 'danger' | 'info' {
    switch (state) {
      case 'running': return 'success';
      case 'stopped': return 'warning';
      case 'notFound': return 'danger';
      default: return 'info';
    }
  }

  presenceLabel(present: boolean): string {
    return present ? 'Present (value hidden)' : 'Not present';
  }

  readError(area: string): string {
    return this.errors()[area] || 'The Agent did not return this read model.';
  }

  private async load(): Promise<void> {
    const firstLoad = this.loading();
    this.refreshing.set(true);
    if (firstLoad) this.loading.set(true);

    const [live, session, identity, connectivity, capabilities, configuration, services] = await Promise.all([
      this.settle(this.transport.getLive()),
      this.settle(this.transport.getSession()),
      this.settle(this.transport.getDeviceIdentity()),
      this.settle(this.transport.getDeviceConnectivity()),
      this.settle(this.transport.getDeviceCapabilities()),
      this.settle(this.transport.getConfiguration()),
      this.settle(this.transport.getServices())
    ]);

    const nextErrors: Record<string, string> = {};
    this.applyLive(live, nextErrors);
    this.applySession(session, nextErrors);
    this.applyValue('identity', identity, this.identity, nextErrors);
    this.applyValue('connectivity', connectivity, this.connectivity, nextErrors);
    this.applyValue('capabilities', capabilities, this.capabilities, nextErrors);
    this.applyValue('configuration', configuration, this.configuration, nextErrors);
    this.applyValue('services', services, this.services, nextErrors);
    this.errors.set(nextErrors);

    const failedReads = Object.keys(nextErrors).filter(key => key !== 'agent' && key !== 'session');
    this.globalError.set(failedReads.length ? 'The available panels show which direct Agent reads completed and which need attention.' : null);
    this.loading.set(false);
    this.refreshing.set(false);
  }

  private applyLive(result: Settled<HealthStatus>, errors: Record<string, string>): void {
    if (result.ok) {
      this.agentStatus.set(result.value.status === 'live' ? 'reachable' : 'reachable');
      return;
    }

    this.agentStatus.set('unreachable');
    errors['agent'] = this.userFacingError(result.error);
  }

  private applySession(result: Settled<SessionInfo>, errors: Record<string, string>): void {
    if (result.ok) {
      this.session.set(result.value);
      this.authStatus.set('authenticated');
      return;
    }

    this.session.set(null);
    this.authStatus.set(result.error.kind === 'authenticationRequired' ? 'authentication-required' : 'unavailable');
    errors['session'] = this.userFacingError(result.error);
  }

  private applyValue<T>(
    key: string,
    result: Settled<T>,
    target: WritableSignal<T | null>,
    errors: Record<string, string>
  ): void {
    if (result.ok) {
      target.set(result.value);
      return;
    }

    target.set(null);
    errors[key] = this.userFacingError(result.error);
  }

  private async settle<T>(request: Observable<T>): Promise<Settled<T>> {
    try {
      return { ok: true, value: await firstValueFrom(request) };
    } catch (error) {
      return { ok: false, error: classifyPosAgentError(error) };
    }
  }

  private userFacingError(error: PosAgentTransportError): string {
    switch (error.kind) {
      case 'transportUnavailableOrBlocked': return 'The fixed POS Agent endpoint could not be reached or the browser blocked the request.';
      case 'authenticationRequired': return 'Windows authentication is required for this read.';
      case 'originRejected': return 'The Support Hub origin was rejected by the POS Agent.';
      case 'notAuthorized': return 'The signed-in Windows account is not authorized for this read.';
      case 'contractMismatch': return error.code === 'host_rejected' ? 'The Agent rejected the request host.' : error.code === 'https_required' ? 'The Agent requires HTTPS for this read.' : 'The Agent contract rejected this read.';
      case 'agentServerError': return 'The POS Agent reported a server error without exposing implementation details.';
      default: return 'The POS Agent did not complete this read.';
    }
  }

  private actionKey(serviceId: string, action: ServiceActionKind): string {
    return `${serviceId}:${action}`;
  }

  private createIdempotencyKey(): string {
    const randomId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return `support-${randomId}`.slice(0, 128);
  }

  private notifyActionOutcome(response: ServiceActionResponse): void {
    switch (response.outcome) {
      case 'accepted':
        this.toast.showSuccess('The POS Agent acknowledged the service action.');
        return;
      case 'failed':
        this.toast.showError('The Windows service rejected the action.');
        return;
      case 'outcomeUnknown':
        this.toast.showWarning('The service action outcome is unknown. Check the service state before deciding whether to retry.');
        return;
      default:
        this.toast.showInfo('The service action was not attempted.');
    }
  }

  private actionErrorMessage(error: PosAgentTransportError): string {
    switch (error.kind) {
      case 'authenticationRequired': return 'Windows authentication is required to control a service.';
      case 'originRejected': return 'The Support Hub origin is not accepted by the POS Agent.';
      case 'notAuthorized': return 'The signed-in Windows account is not authorized to control services.';
      case 'transportUnavailableOrBlocked': return 'The fixed POS Agent endpoint could not be reached or the browser blocked the action.';
      case 'agentServerError': return 'The POS Agent reported a server error without exposing implementation details.';
      case 'contractMismatch': return 'The POS Agent rejected the service-control contract.';
      default: return 'The POS Agent did not complete the service action.';
    }
  }
}
