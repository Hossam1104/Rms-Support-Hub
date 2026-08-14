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
type RmsDiagnostics = components['schemas']['RmsDiagnosticsDto'];
type RmsDatabaseDiagnostic = components['schemas']['RmsDatabaseDiagnosticDto'];
type RmsEndpointDiagnostic = components['schemas']['RmsEndpointDiagnosticDto'];
type RmsDatabaseTarget = components['schemas']['RmsDatabaseTarget'];
type RmsDatabaseWorkspace = components['schemas']['RmsDatabaseWorkspaceDto'];
type RmsDatabaseOperation = components['schemas']['RmsDatabaseOperationDto'];
type RmsDatabaseArtifact = components['schemas']['RmsDatabaseArtifactDto'];
type BranchCatalogEntry = components['schemas']['BranchCatalogEntryDto'];
type DownloaderOperation = components['schemas']['DownloaderOperationDto'];
type DownloaderBranchOutcome = components['schemas']['DownloaderBranchOutcomeDto'];
type CleanupPreview = components['schemas']['CleanupPreviewDto'];
type BranchResetPreview = components['schemas']['BranchResetPreviewDto'];
type MaintenanceOperation = components['schemas']['MaintenanceOperationDto'];
type AgentState = 'loading' | 'reachable' | 'unreachable';
type AuthState = 'loading' | 'authenticated' | 'authentication-required' | 'unavailable';
type Settled<T> = { readonly ok: true; readonly value: T } | { readonly ok: false; readonly error: PosAgentTransportError };
type PendingServiceAction = Readonly<{ service: ServiceSummary; action: ServiceActionKind }>;
type PendingDatabaseRestore = Readonly<{
  target: RmsDatabaseTarget;
  artifactId: string;
  displayName: string;
  confirmationText: string;
}>;
type PendingMaintenanceAction = Readonly<{
  mode: 'cleanup' | 'branch-reset';
  challengeId: string;
  confirmationText: string;
}>;

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

      <section class="rms-dashboard" aria-label="RMS support dashboard">
        <div class="dashboard-heading">
          <div>
            <p class="eyebrow">Installed RMS+ suite</p>
            <h2>Support dashboard</h2>
          </div>
          <p class="dashboard-heading__copy">Discovery from the installed RMS files, with sanitized database probes and tightly bounded backup/restore actions.</p>
        </div>
        @if (loading()) {
          <div class="dashboard-grid">
            <ui-card variant="raised" class="workspace-card"><div class="skeleton-stack"><app-skeleton></app-skeleton><app-skeleton></app-skeleton><app-skeleton></app-skeleton></div></ui-card>
            <ui-card variant="raised" class="workspace-card"><div class="skeleton-stack"><app-skeleton></app-skeleton><app-skeleton></app-skeleton><app-skeleton></app-skeleton></div></ui-card>
            <ui-card variant="raised" class="workspace-card"><div class="skeleton-stack"><app-skeleton></app-skeleton><app-skeleton></app-skeleton><app-skeleton></app-skeleton></div></ui-card>
          </div>
        } @else if (rmsDiagnostics(); as rms) {
          <div class="dashboard-grid">
            <ui-card variant="raised" class="workspace-card dashboard-card dashboard-card--wide">
              <div uiCardHeader class="card-heading card-heading--split">
                <div class="card-heading__copy"><p class="eyebrow">RMS installation</p><h2>Detected automatically</h2></div>
                <i class="bi bi-box-seam card-heading__mark" aria-hidden="true"></i>
              </div>
              <dl class="data-list">
                <div><dt>Branch Code</dt><dd>{{ rms.installation.branchCode || 'Unavailable' }}</dd></div>
                <div><dt>POS Number</dt><dd>{{ rms.installation.posNumber || 'Unavailable' }}</dd></div>
                <div><dt>Installation mode</dt><dd>{{ rms.installation.installationMode || 'Unavailable' }}</dd></div>
                <div><dt>Installation GUID</dt><dd>{{ rms.installation.installationGuid || 'Unavailable' }}</dd></div>
                <div><dt>Components</dt><dd>{{ componentInstallLabel(rms) }}</dd></div>
                <div><dt>Version consistency</dt><dd><app-status-badge [label]="consistencyLabel(rms.installation.consistency.version)" [variant]="consistencyVariant(rms.installation.consistency.version)" role="status"></app-status-badge></dd></div>
              </dl>
              @if (rms.installation.consistency.warnings.length) {
                <div class="consistency-warning" role="alert"><i class="bi bi-exclamation-triangle" aria-hidden="true"></i><div><strong>Configuration consistency warning</strong><ul>@for (warning of rms.installation.consistency.warnings; track warning) {<li>{{ warning }}</li>}</ul></div></div>
              } @else {
                <p class="boundary-copy"><i class="bi bi-check2-circle" aria-hidden="true"></i> Duplicated RMS identity and version metadata agrees.</p>
              }
            </ui-card>

            <ui-card variant="raised" class="workspace-card dashboard-card">
              <div uiCardHeader class="card-heading card-heading--split"><div class="card-heading__copy"><p class="eyebrow">Main server</p><h2>Configured endpoint</h2></div><i class="bi bi-cloud-check card-heading__mark" aria-hidden="true"></i></div>
              <div class="endpoint-summary"><app-status-badge [label]="endpointConfiguredLabel(rms.connectivity.mainServer)" [variant]="endpointConfiguredVariant(rms.connectivity.mainServer)" role="status"></app-status-badge><strong>{{ rms.connectivity.mainServer.endpoint || rms.installation.mainServerUrl || 'Unavailable' }}</strong></div>
              <p class="evidence-detail">{{ rms.connectivity.mainServer.reachability.detail }}</p>
              <small>Checked {{ checkedAt(rms.connectivity.mainServer.reachability) }}</small>
            </ui-card>

            <ui-card variant="raised" class="workspace-card dashboard-card">
              <div uiCardHeader class="card-heading card-heading--split"><div class="card-heading__copy"><p class="eyebrow">Branch server</p><h2>Local endpoint</h2></div><i class="bi bi-diagram-3 card-heading__mark" aria-hidden="true"></i></div>
              <div class="endpoint-summary"><app-status-badge [label]="endpointConfiguredLabel(rms.connectivity.branchServer)" [variant]="endpointConfiguredVariant(rms.connectivity.branchServer)" role="status"></app-status-badge><strong>{{ rms.connectivity.branchServer.endpoint || rms.installation.branchServerAddress || 'Unavailable' }}</strong></div>
              <p class="evidence-detail">{{ rms.connectivity.branchServer.reachability.detail }}</p>
              <small>Checked {{ checkedAt(rms.connectivity.branchServer.reachability) }}</small>
            </ui-card>

            <ui-card variant="raised" class="workspace-card dashboard-card">
              <div uiCardHeader class="card-heading card-heading--split"><div class="card-heading__copy"><p class="eyebrow">Branch database</p><h2>{{ rms.branchDatabase.expectedDatabase }}</h2></div><i class="bi bi-database-check card-heading__mark" aria-hidden="true"></i></div>
              <div class="database-summary"><app-status-badge [label]="databaseStatusLabel(rms.branchDatabase)" [variant]="databaseStatusVariant(rms.branchDatabase)" role="status"></app-status-badge><strong>{{ rms.branchDatabase.configuredDatabase || 'Not detected' }}</strong></div>
              <dl class="data-list data-list--compact"><div><dt>Detected automatically</dt><dd>Yes</dd></div><div><dt>Server</dt><dd>{{ rms.branchDatabase.serverDisplay || 'Unavailable' }}</dd></div></dl>
              <p class="evidence-detail">{{ rms.branchDatabase.evidence.detail }}</p>
              @if (databaseWorkspace('branch'); as workspace) {
                <div class="database-ops" aria-label="Branch database backup and restore">
                  <div class="database-ops__header"><span class="eyebrow">Recovery shelf</span><span class="database-ops__scope">Agent-owned artifacts</span></div>
                  @if (workspace.latestOperation; as operation) {
                    <div class="database-operation" [class.database-operation--danger]="operation.outcome === 'outcomeUnknown' || operation.outcome === 'failed'" role="status">
                      <div class="database-operation__heading"><strong>{{ databaseOperationLabel(operation) }}</strong><span>{{ operation.progressPercent }}%</span></div>
                      <div class="progress-track" role="progressbar" [attr.aria-valuenow]="operation.progressPercent" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="operation.stage"><span [style.width.%]="operation.progressPercent"></span></div>
                      <p>{{ operation.detail }}</p>
                    </div>
                  }
                  <div class="database-ops__actions" role="group" aria-label="Branch database actions">
                    @if (canControlDatabases()) {
                      <ui-button variant="secondary" size="sm" icon="bi-archive" [loading]="isDatabaseSubmitting('branch') && pendingDatabaseKind() === 'backup'" [disabled]="submittingDatabaseAction() !== null" (pressed)="requestDatabaseBackup('branch')">Backup</ui-button>
                    }
                    <span class="database-ops__hint">Restore uses an approved artifact and an exact confirmation.</span>
                  </div>
                  @if (workspace.approvedBackups.length) {
                    <div class="backup-list" role="list" aria-label="Approved Branch backups">
                      @for (backup of workspace.approvedBackups; track backup.artifactId) {
                        <div class="backup-row" role="listitem">
                          <div><strong>{{ backup.displayName }}</strong><small>{{ formatArtifactSize(backup) }} · {{ formatArtifactDate(backup) }}</small></div>
                          @if (canControlDatabases()) {
                            <ui-button variant="danger" size="sm" icon="bi-arrow-counterclockwise" [disabled]="submittingDatabaseAction() !== null" (pressed)="requestDatabaseRestore('branch', backup)">Restore</ui-button>
                          }
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="database-empty">No approved Branch backup is available yet.</p>
                  }
                </div>
              } @else {
                <p class="database-empty">Backup and restore controls are unavailable until the Agent workspace read succeeds.</p>
              }
            </ui-card>

            <ui-card variant="raised" class="workspace-card dashboard-card">
              <div uiCardHeader class="card-heading card-heading--split"><div class="card-heading__copy"><p class="eyebrow">Cashier database</p><h2>{{ rms.cashierDatabase.expectedDatabase }}</h2></div><i class="bi bi-database-check card-heading__mark" aria-hidden="true"></i></div>
              <div class="database-summary"><app-status-badge [label]="databaseStatusLabel(rms.cashierDatabase)" [variant]="databaseStatusVariant(rms.cashierDatabase)" role="status"></app-status-badge><strong>{{ rms.cashierDatabase.configuredDatabase || 'Not detected' }}</strong></div>
              <dl class="data-list data-list--compact"><div><dt>Detected automatically</dt><dd>Yes</dd></div><div><dt>Server</dt><dd>{{ rms.cashierDatabase.serverDisplay || 'Unavailable' }}</dd></div></dl>
              <p class="evidence-detail">{{ rms.cashierDatabase.evidence.detail }}</p>
              @if (databaseWorkspace('cashier'); as workspace) {
                <div class="database-ops" aria-label="Cashier database backup and restore">
                  <div class="database-ops__header"><span class="eyebrow">Recovery shelf</span><span class="database-ops__scope">Agent-owned artifacts</span></div>
                  @if (workspace.latestOperation; as operation) {
                    <div class="database-operation" [class.database-operation--danger]="operation.outcome === 'outcomeUnknown' || operation.outcome === 'failed'" role="status">
                      <div class="database-operation__heading"><strong>{{ databaseOperationLabel(operation) }}</strong><span>{{ operation.progressPercent }}%</span></div>
                      <div class="progress-track" role="progressbar" [attr.aria-valuenow]="operation.progressPercent" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="operation.stage"><span [style.width.%]="operation.progressPercent"></span></div>
                      <p>{{ operation.detail }}</p>
                    </div>
                  }
                  <div class="database-ops__actions" role="group" aria-label="Cashier database actions">
                    @if (canControlDatabases()) {
                      <ui-button variant="secondary" size="sm" icon="bi-archive" [loading]="isDatabaseSubmitting('cashier') && pendingDatabaseKind() === 'backup'" [disabled]="submittingDatabaseAction() !== null" (pressed)="requestDatabaseBackup('cashier')">Backup</ui-button>
                    }
                    <span class="database-ops__hint">Restore uses an approved artifact and an exact confirmation.</span>
                  </div>
                  @if (workspace.approvedBackups.length) {
                    <div class="backup-list" role="list" aria-label="Approved Cashier backups">
                      @for (backup of workspace.approvedBackups; track backup.artifactId) {
                        <div class="backup-row" role="listitem">
                          <div><strong>{{ backup.displayName }}</strong><small>{{ formatArtifactSize(backup) }} · {{ formatArtifactDate(backup) }}</small></div>
                          @if (canControlDatabases()) {
                            <ui-button variant="danger" size="sm" icon="bi-arrow-counterclockwise" [disabled]="submittingDatabaseAction() !== null" (pressed)="requestDatabaseRestore('cashier', backup)">Restore</ui-button>
                          }
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="database-empty">No approved Cashier backup is available yet.</p>
                  }
                </div>
              } @else {
                <p class="database-empty">Backup and restore controls are unavailable until the Agent workspace read succeeds.</p>
              }
            </ui-card>
          </div>
        } @else {
          <app-empty-state icon="bi-box-seam" title="RMS discovery is unavailable" [description]="readError('rms')"></app-empty-state>
        }
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
              <p class="eyebrow">RMS services</p>
              <h2>SCM status and controls</h2>
            </div>
            <i class="bi bi-gear-wide-connected card-heading__mark" aria-hidden="true"></i>
          </div>
          @if (loading()) {
            <div class="skeleton-stack"><app-skeleton height="44px"></app-skeleton><app-skeleton height="44px"></app-skeleton></div>
          } @else if (services(); as values) {
            @if (values.length) {
              <div class="service-list" role="list" aria-label="Canonical RMS Windows services">
                @for (service of values; track service.serviceId) {
                  <div class="service-row" role="listitem">
                    <div class="service-row__copy"><strong>{{ service.displayName }}</strong><small>{{ service.installed ? 'Installed' : 'Not installed' }} · {{ service.lastChecked.detail }}</small></div>
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
              <app-empty-state icon="bi-gear-wide-connected" title="No RMS services were found" description="The Agent returned no canonical RMS service rows."></app-empty-state>
            }
            <p class="boundary-copy"><i class="bi bi-shield-check" aria-hidden="true"></i> Controls appear only for an authorized local Administrator and only for the state-valid actions returned by the Agent.</p>
          } @else {
            <app-empty-state icon="bi-gear-wide-connected" title="Service status is unavailable" [description]="readError('services')"></app-empty-state>
          }
        </ui-card>
      </section>

      <section class="operator-rail" aria-label="Downloader and maintenance operations">
        <div class="dashboard-heading operator-rail__heading">
          <div>
            <p class="eyebrow">Operator safety rail</p>
            <h2>Downloader and maintenance</h2>
          </div>
          <p class="dashboard-heading__copy">Server-owned targets, short-lived confirmation, and retained outcome evidence keep high-impact POS work reviewable.</p>
        </div>
        <div class="operator-grid">
          <ui-card variant="raised" class="workspace-card operator-card operator-card--downloader">
            <div uiCardHeader class="card-heading card-heading--split">
              <div class="card-heading__copy"><p class="eyebrow">Artifact conveyor</p><h2>Download branch backups</h2></div>
              <i class="bi bi-cloud-arrow-down card-heading__mark" aria-hidden="true"></i>
            </div>
            @if (downloaderBranches(); as branches) {
              @if (branches.length) {
                <div class="branch-picker" role="group" aria-label="Server-approved downloader branches">
                  @for (branch of branches; track branch.branchCode) {
                    <label class="branch-chip">
                      <input type="checkbox" [checked]="isBranchSelected(branch.branchCode)" (change)="toggleBranch(branch.branchCode)">
                      <span>{{ branch.branchCode }}</span>
                    </label>
                  }
                </div>
                <ui-button variant="secondary" size="sm" icon="bi-cloud-arrow-down" [loading]="submittingDownloader()" [disabled]="!canOperate() || !selectedBranches().length || submittingDownloader()" (pressed)="startDownloader()">Download selected</ui-button>
              } @else {
                <app-empty-state icon="bi-cloud-slash" title="No approved branches" description="The Agent has no server-approved downloader branch selection." />
              }
            } @else {
              <app-empty-state icon="bi-cloud-slash" title="Downloader is unavailable" [description]="readError('downloader')" />
            }
            @if (downloaderOperation(); as operation) {
              <div class="operation-ledger" [class.operation-ledger--danger]="operation.outcome === 'outcomeUnknown' || operation.outcome === 'failed'" role="status">
                <div class="operation-ledger__heading"><strong>{{ downloaderOperationLabel(operation) }}</strong><span>{{ operation.progressPercent }}%</span></div>
                <div class="progress-track" role="progressbar" [attr.aria-valuenow]="operation.progressPercent" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="operation.stage"><span [style.width.%]="numberValue(operation.progressPercent)"></span></div>
                <p>{{ operation.detail }}</p>
                @if (operation.downloaderOutcome; as outcome) {
                  <div class="branch-results" role="list" aria-label="Downloader branch results">
                    @for (branch of outcome.branches; track branch.branchCode) {
                      <div class="branch-result" role="listitem"><span><strong>{{ branch.branchCode }}</strong> · {{ downloaderBranchLabel(branch.state) }}</span>@if (branch.artifactId; as artifactId) {<button type="button" class="artifact-link" (click)="downloadArtifact(artifactId, branch.branchCode + '.zip')">Download artifact</button>}</div>
                    }
                  </div>
                }
              </div>
            }
            <p class="boundary-copy"><i class="bi bi-lock" aria-hidden="true"></i> Remote SMB paths and credentials stay inside the Agent; the browser receives only branch state and opaque artifacts.</p>
          </ui-card>

          <ui-card variant="raised" class="workspace-card operator-card operator-card--maintenance">
            <div uiCardHeader class="card-heading card-heading--split">
              <div class="card-heading__copy"><p class="eyebrow">Controlled cleanup</p><h2>Preview before mutation</h2></div>
              <i class="bi bi-shield-check card-heading__mark" aria-hidden="true"></i>
            </div>
            <div class="maintenance-actions" role="group" aria-label="Maintenance previews">
              <ui-button variant="secondary" size="sm" icon="bi-search" [loading]="previewingMaintenance() === 'cleanup'" [disabled]="!canOperate() || previewingMaintenance() !== null" (pressed)="previewCleanup()">Preview cleanup</ui-button>
              <ui-button variant="secondary" size="sm" icon="bi-database-x" [loading]="previewingMaintenance() === 'branch-reset'" [disabled]="!canOperate() || previewingMaintenance() !== null" (pressed)="previewBranchReset()">Preview branch reset</ui-button>
            </div>
            @if (cleanupPreview(); as preview) {
              <div class="preview-ledger" [class.preview-ledger--danger]="!preview.ready">
                <div class="preview-ledger__heading"><strong>Cleanup preview</strong><app-status-badge [label]="preview.ready ? 'Ready' : 'Rejected'" [variant]="preview.ready ? 'warning' : 'danger'" role="status"></app-status-badge></div>
                <p>{{ preview.ready ? preview.pathsToDelete.length + ' logical target(s) will be reviewed by the Agent.' : 'The Agent rejected this preview; no mutation challenge is available.' }}</p>
                @if (preview.ready) { <ui-button variant="danger" size="sm" icon="bi-trash3" [disabled]="!canOperate()" (pressed)="requestMaintenanceExecution('cleanup')">Continue to cleanup</ui-button> }
              </div>
            }
            @if (branchResetPreview(); as preview) {
              <div class="preview-ledger" [class.preview-ledger--danger]="!preview.ready">
                <div class="preview-ledger__heading"><strong>Branch reset preview</strong><app-status-badge [label]="preview.ready ? 'Ready' : 'Rejected'" [variant]="preview.ready ? 'warning' : 'danger'" role="status"></app-status-badge></div>
                <p>{{ preview.ready ? preview.affectedTables.length + ' approved table scope(s) for ' + preview.branchCode + '.' : 'The Agent rejected this preview; no mutation challenge is available.' }}</p>
                @if (preview.ready) { <ui-button variant="danger" size="sm" icon="bi-database-x" [disabled]="!canOperate()" (pressed)="requestMaintenanceExecution('branch-reset')">Continue to reset</ui-button> }
              </div>
            }
            @if (maintenanceOperation(); as operation) {
              <div class="operation-ledger" [class.operation-ledger--danger]="operation.outcome === 'outcomeUnknown' || operation.outcome === 'failed'" role="status">
                <div class="operation-ledger__heading"><strong>{{ maintenanceOperationLabel(operation) }}</strong><span>{{ operation.progressPercent }}%</span></div>
                <div class="progress-track" role="progressbar" [attr.aria-valuenow]="operation.progressPercent" aria-valuemin="0" aria-valuemax="100" [attr.aria-label]="operation.stage"><span [style.width.%]="numberValue(operation.progressPercent)"></span></div>
                <p>{{ operation.detail }}</p>
                @if (operation.maintenanceOutcome?.recoveryRequired) { <p class="recovery-note"><i class="bi bi-exclamation-triangle" aria-hidden="true"></i> Recovery verification is required before retrying.</p> }
              </div>
            }
            <p class="boundary-copy"><i class="bi bi-shield-lock" aria-hidden="true"></i> Every cleanup or reset re-runs server policy and requires an expiring preview challenge plus a one-use mutation token.</p>
          </ui-card>
        </div>
      </section>

      <section class="boundary-banner" aria-label="POS Agent mutation boundary">
        <div class="boundary-banner__icon" aria-hidden="true"><i class="bi bi-shield-lock"></i></div>
        <div>
          <p class="eyebrow">INT-08 boundary</p>
          <h2>Typed service controls with safe outcome truth</h2>
          <p>Service actions and database backup/restore use typed server-owned targets, exact-origin Windows authorization, short-lived one-use Agent tokens, and bounded idempotency keys. The database cards expose only approved artifact handles; paths, credentials, arbitrary SQL, and generic command execution remain outside this surface. An unknown outcome is never retried automatically.</p>
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
    @if (pendingDatabaseAction(); as pending) {
      <app-confirm-dialog
        variant="danger"
        [title]="'Restore ' + pending.displayName.toLowerCase() + '?'"
        [message]="databaseConfirmationMessage(pending)"
        [requireReason]="true"
        [requiredTypedValue]="pending.confirmationText"
        reasonLabel="Type the exact confirmation phrase"
        [reasonPlaceholder]="pending.confirmationText"
        confirmLabel="Restore database"
        cancelLabel="Cancel"
        (cancel)="cancelPendingDatabaseAction()"
        (confirm)="executePendingDatabaseRestore($event)">
      </app-confirm-dialog>
    }
    @if (pendingMaintenanceAction(); as pending) {
      <app-confirm-dialog
        variant="danger"
        [title]="pending.mode === 'cleanup' ? 'Confirm cleanup' : 'Confirm branch reset'"
        [message]="pending.mode === 'cleanup' ? 'The Agent will re-check its configured cleanup policy and may stop approved services before deleting approved targets.' : 'The Agent will re-check the configured branch and approved table scope before resetting data.'"
        [requireReason]="true"
        [requiredTypedValue]="pending.confirmationText"
        reasonLabel="Type the exact Agent confirmation phrase"
        [reasonPlaceholder]="pending.confirmationText"
        confirmLabel="Execute"
        cancelLabel="Cancel"
        (cancel)="cancelPendingMaintenance()"
        (confirm)="executePendingMaintenance($event)">
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
    @media (max-width: 1000px) { .status-grid, .workspace-grid { grid-template-columns: 1fr; } }
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
  readonly rmsDiagnostics = signal<RmsDiagnostics | null>(null);
  readonly branchDatabaseWorkspace = signal<RmsDatabaseWorkspace | null>(null);
  readonly cashierDatabaseWorkspace = signal<RmsDatabaseWorkspace | null>(null);
  readonly globalError = signal<string | null>(null);
  readonly pendingAction = signal<PendingServiceAction | null>(null);
  readonly pendingDatabaseAction = signal<PendingDatabaseRestore | null>(null);
  readonly submittingAction = signal<string | null>(null);
  readonly submittingDatabaseAction = signal<RmsDatabaseTarget | null>(null);
  readonly databaseOperationKind = signal<'backup' | 'restore' | null>(null);
  readonly downloaderBranches = signal<BranchCatalogEntry[] | null>(null);
  readonly selectedBranches = signal<string[]>([]);
  readonly downloaderOperation = signal<DownloaderOperation | null>(null);
  readonly cleanupPreview = signal<CleanupPreview | null>(null);
  readonly branchResetPreview = signal<BranchResetPreview | null>(null);
  readonly maintenanceOperation = signal<MaintenanceOperation | null>(null);
  readonly pendingMaintenanceAction = signal<PendingMaintenanceAction | null>(null);
  readonly submittingDownloader = signal(false);
  readonly previewingMaintenance = signal<'cleanup' | 'branch-reset' | null>(null);
  readonly submittingMaintenance = signal(false);

  private readonly errors = signal<Record<string, string>>({});
  private readonly actionOutcomes = signal<Record<string, ServiceActionResponse>>({});
  private downloaderSelectionInitialized = false;

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

  canControlDatabases(): boolean {
    return this.session()?.isAuthorized === true;
  }

  canOperate(): boolean {
    return this.session()?.isAuthorized === true;
  }

  isBranchSelected(branchCode: string): boolean {
    return this.selectedBranches().includes(branchCode);
  }

  toggleBranch(branchCode: string): void {
    this.selectedBranches.update(selected => selected.includes(branchCode)
      ? selected.filter(code => code !== branchCode)
      : [...selected, branchCode]);
  }

  numberValue(value: number | string): number {
    const number = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(number) ? number : 0;
  }

  downloaderOperationLabel(operation: DownloaderOperation): string {
    switch (operation.outcome) {
      case 'completed': return 'Downloader completed';
      case 'failed': return 'Downloader failed';
      case 'outcomeUnknown': return 'Downloader outcome unknown';
      case 'accepted': return 'Downloader accepted';
      default: return 'Downloader not attempted';
    }
  }

  downloaderBranchLabel(state: DownloaderBranchOutcome['state']): string {
    switch (state) {
      case 'pending': return 'Pending';
      case 'triggered': return 'Triggered';
      case 'waiting': return 'Waiting for artifact';
      case 'detected': return 'Artifact detected';
      case 'validating': return 'Validating';
      case 'ready': return 'Ready to download';
      case 'downloading': return 'Downloading';
      case 'completed': return 'Completed';
      case 'timedOut': return 'Timed out';
      case 'cancelled': return 'Cancelled';
      case 'failed': return 'Failed';
    }
  }

  maintenanceOperationLabel(operation: MaintenanceOperation): string {
    const action = operation.mode === 'cleanup' ? 'Cleanup' : 'Branch reset';
    switch (operation.outcome) {
      case 'completed': return `${action} completed`;
      case 'failed': return `${action} failed`;
      case 'outcomeUnknown': return `${action} outcome unknown`;
      case 'accepted': return `${action} accepted`;
      default: return `${action} not attempted`;
    }
  }

  async startDownloader(): Promise<void> {
    if (!this.canOperate() || !this.selectedBranches().length || this.submittingDownloader()) return;

    this.submittingDownloader.set(true);
    try {
      const issued = await firstValueFrom(
        this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.downloaderBatchTrigger)
      );
      const accepted = await firstValueFrom(
        this.transport.triggerDownloaderBatch(
          this.selectedBranches(),
          this.createIdempotencyKey(),
          issued.token
        )
      );
      const operation = await this.followDownloaderOperation(accepted);
      this.downloaderOperation.set(operation);
      this.notifyDownloaderOutcome(operation);
      if (operation.outcome === 'completed') {
        const branches = await this.settle(this.transport.getDownloaderBranches());
        if (branches.ok) this.applyDownloaderBranches(branches, this.errors());
      }
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.submittingDownloader.set(false);
    }
  }

  async downloadArtifact(artifactId: string, fileName: string): Promise<void> {
    if (!this.canOperate()) return;
    try {
      const artifact = await firstValueFrom(this.transport.downloadArtifact(artifactId));
      const objectUrl = URL.createObjectURL(artifact);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = this.safeDownloadName(fileName);
      link.click();
      URL.revokeObjectURL(objectUrl);
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    }
  }

  async previewCleanup(): Promise<void> {
    if (!this.canOperate() || this.previewingMaintenance() !== null) return;
    this.previewingMaintenance.set('cleanup');
    try {
      const preview = await firstValueFrom(this.transport.previewCleanup());
      this.cleanupPreview.set(preview);
      this.toast.showInfo(preview.ready === true ? 'Cleanup preview is ready for operator confirmation.' : 'Cleanup preview was rejected by the Agent policy.');
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.previewingMaintenance.set(null);
    }
  }

  async previewBranchReset(): Promise<void> {
    if (!this.canOperate() || this.previewingMaintenance() !== null) return;
    this.previewingMaintenance.set('branch-reset');
    try {
      const preview = await firstValueFrom(this.transport.previewBranchReset());
      this.branchResetPreview.set(preview);
      this.toast.showInfo(preview.ready === true ? 'Branch reset preview is ready for operator confirmation.' : 'Branch reset preview was rejected by the Agent policy.');
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.previewingMaintenance.set(null);
    }
  }

  requestMaintenanceExecution(mode: 'cleanup' | 'branch-reset'): void {
    if (!this.canOperate() || this.submittingMaintenance()) return;
    const preview = mode === 'cleanup' ? this.cleanupPreview() : this.branchResetPreview();
    if (!preview || preview.ready !== true) return;
    this.pendingMaintenanceAction.set({
      mode,
      challengeId: preview.challengeId,
      confirmationText: preview.confirmationPhrase
    });
  }

  cancelPendingMaintenance(): void {
    this.pendingMaintenanceAction.set(null);
  }

  async executePendingMaintenance(confirmationText: string): Promise<void> {
    const pending = this.pendingMaintenanceAction();
    if (!pending || !this.canOperate() || this.submittingMaintenance()) {
      this.pendingMaintenanceAction.set(null);
      return;
    }

    if (confirmationText !== pending.confirmationText) {
      this.toast.showError('Type the exact Agent confirmation phrase before continuing.');
      return;
    }

    this.pendingMaintenanceAction.set(null);
    this.submittingMaintenance.set(true);
    try {
      const operationId = pending.mode === 'cleanup'
        ? POS_AGENT_OPERATION_IDS.maintenanceCleanup
        : POS_AGENT_OPERATION_IDS.maintenanceBranchReset;
      const issued = await firstValueFrom(this.transport.issueMutationToken(operationId));
      const accepted = pending.mode === 'cleanup'
        ? await firstValueFrom(this.transport.executeCleanup(
          pending.challengeId,
          confirmationText,
          this.createIdempotencyKey(),
          issued.token
        ))
        : await firstValueFrom(this.transport.executeBranchReset(
          pending.challengeId,
          confirmationText,
          this.createIdempotencyKey(),
          issued.token
        ));
      const operation = await this.followMaintenanceOperation(accepted);
      this.maintenanceOperation.set(operation);
      this.notifyMaintenanceOutcome(operation);
      if (pending.mode === 'cleanup') this.cleanupPreview.set(null);
      else this.branchResetPreview.set(null);
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.submittingMaintenance.set(false);
    }
  }

  databaseWorkspace(target: RmsDatabaseTarget): RmsDatabaseWorkspace | null {
    return target === 'branch' ? this.branchDatabaseWorkspace() : this.cashierDatabaseWorkspace();
  }

  requestDatabaseBackup(target: RmsDatabaseTarget): void {
    if (!this.canControlDatabases() || this.submittingDatabaseAction() !== null) return;
    void this.executeDatabaseBackup(target);
  }

  requestDatabaseRestore(target: RmsDatabaseTarget, artifact: RmsDatabaseArtifact): void {
    if (!this.canControlDatabases() || this.submittingDatabaseAction() !== null) return;
    const workspace = this.databaseWorkspace(target);
    if (!workspace) return;
    this.pendingDatabaseAction.set({
      target,
      artifactId: artifact.artifactId,
      displayName: workspace.databaseDisplayName,
      confirmationText: workspace.restoreConfirmationText
    });
  }

  cancelPendingDatabaseAction(): void {
    this.pendingDatabaseAction.set(null);
  }

  pendingDatabaseKind(): 'backup' | 'restore' | null {
    return this.databaseOperationKind();
  }

  isDatabaseSubmitting(target: RmsDatabaseTarget): boolean {
    return this.submittingDatabaseAction() === target;
  }

  databaseConfirmationMessage(pending: PendingDatabaseRestore): string {
    return `This will replace the live ${pending.displayName} with the selected Agent-approved backup. Only the corresponding RMS service will be coordinated. Type ${pending.confirmationText} below to continue; the Agent will enforce the exact confirmation and will report if recovery remains required.`;
  }

  databaseOperationLabel(operation: RmsDatabaseOperation): string {
    const action = operation.operation === 'backup' ? 'Backup' : 'Restore';
    switch (operation.outcome) {
      case 'completed': return `${action} completed`;
      case 'failed': return `${action} failed`;
      case 'outcomeUnknown': return `${action} outcome unknown`;
      case 'accepted': return `${action} accepted`;
      default: return `${action} not attempted`;
    }
  }

  formatArtifactSize(artifact: RmsDatabaseArtifact): string {
    const size = typeof artifact.sizeBytes === 'number' ? artifact.sizeBytes : Number(artifact.sizeBytes);
    if (!Number.isFinite(size) || size < 0) return 'Size unavailable';
    if (size < 1024 * 1024) return `${Math.max(1, Math.round(size / 1024))} KB`;
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  }

  formatArtifactDate(artifact: RmsDatabaseArtifact): string {
    const date = new Date(artifact.createdAtUtc);
    return Number.isNaN(date.getTime()) ? 'date unavailable' : date.toLocaleString();
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
      case 'paused': return 'Paused';
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

  async executePendingDatabaseRestore(confirmationText: string): Promise<void> {
    const pending = this.pendingDatabaseAction();
    if (!pending || !this.canControlDatabases() || this.submittingDatabaseAction() !== null) {
      this.pendingDatabaseAction.set(null);
      return;
    }

    if (confirmationText !== pending.confirmationText) {
      this.toast.showError('Type the exact restore confirmation phrase before continuing.');
      return;
    }

    this.pendingDatabaseAction.set(null);
    this.submittingDatabaseAction.set(pending.target);
    this.databaseOperationKind.set('restore');
    try {
      const issued = await firstValueFrom(
        this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.rmsDatabaseRestore, pending.target)
      );
      const accepted = await firstValueFrom(
        this.transport.restoreRmsDatabase(
          pending.target,
          pending.artifactId,
          confirmationText,
          this.createIdempotencyKey(),
          issued.token
        )
      );
      const operation = await this.followDatabaseOperation(pending.target, accepted);
      this.applyDatabaseOperation(pending.target, operation);
      this.notifyDatabaseOutcome(operation);
      await this.refreshDatabaseWorkspace(pending.target);
    } catch (error) {
      this.toast.showError(this.databaseActionErrorMessage(classifyPosAgentError(error)));
    } finally {
      if (this.submittingDatabaseAction() === pending.target) {
        this.submittingDatabaseAction.set(null);
      }
      this.databaseOperationKind.set(null);
    }
  }

  private async executeDatabaseBackup(target: RmsDatabaseTarget): Promise<void> {
    this.submittingDatabaseAction.set(target);
    this.databaseOperationKind.set('backup');
    try {
      const issued = await firstValueFrom(
        this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.rmsDatabaseBackup, target)
      );
      const accepted = await firstValueFrom(
        this.transport.backupRmsDatabase(target, this.createIdempotencyKey(), issued.token)
      );
      const operation = await this.followDatabaseOperation(target, accepted);
      this.applyDatabaseOperation(target, operation);
      this.notifyDatabaseOutcome(operation);
      await this.refreshDatabaseWorkspace(target);
    } catch (error) {
      this.toast.showError(this.databaseActionErrorMessage(classifyPosAgentError(error)));
    } finally {
      if (this.submittingDatabaseAction() === target) {
        this.submittingDatabaseAction.set(null);
      }
      this.databaseOperationKind.set(null);
    }
  }

  private async followDatabaseOperation(
    target: RmsDatabaseTarget,
    accepted: RmsDatabaseOperation
  ): Promise<RmsDatabaseOperation> {
    let current = accepted;
    for (let attempt = 0; attempt < 80; attempt++) {
      if (!['accepted', 'running'].includes(current.state)) return current;
      await this.delay(250);
      current = await firstValueFrom(
        this.transport.getRmsDatabaseOperation(target, current.operationId)
      );
    }
    return current;
  }

  private async followDownloaderOperation(accepted: DownloaderOperation): Promise<DownloaderOperation> {
    let current = accepted;
    for (let attempt = 0; attempt < 80; attempt++) {
      if (!['accepted', 'running'].includes(current.state)) return current;
      await this.delay(250);
      current = await firstValueFrom(this.transport.getDownloaderOperation(current.operationId));
    }
    return current;
  }

  private async followMaintenanceOperation(accepted: MaintenanceOperation): Promise<MaintenanceOperation> {
    let current = accepted;
    for (let attempt = 0; attempt < 80; attempt++) {
      if (!['accepted', 'running'].includes(current.state)) return current;
      await this.delay(250);
      current = await firstValueFrom(this.transport.getMaintenanceOperation(current.operationId));
    }
    return current;
  }

  private applyDownloaderBranches(
    result: Settled<BranchCatalogEntry[]>,
    errors: Record<string, string>
  ): void {
    if (!result.ok) {
      this.downloaderBranches.set(null);
      errors['downloader'] = this.userFacingError(result.error);
      return;
    }

    this.downloaderBranches.set(result.value);
    const available = new Set(result.value.map(branch => branch.branchCode));
    if (!this.downloaderSelectionInitialized) {
      this.selectedBranches.set(result.value.filter(branch => branch.isSelected).map(branch => branch.branchCode));
      this.downloaderSelectionInitialized = true;
    } else {
      this.selectedBranches.update(selected => selected.filter(branchCode => available.has(branchCode)));
    }
  }

  private applyDatabaseOperation(target: RmsDatabaseTarget, operation: RmsDatabaseOperation): void {
    const current = this.databaseWorkspace(target);
    if (!current) return;
    this.setDatabaseWorkspace(target, { ...current, latestOperation: operation });
  }

  private async refreshDatabaseWorkspace(target: RmsDatabaseTarget): Promise<void> {
    try {
      const workspace = await firstValueFrom(this.transport.getRmsDatabaseWorkspace(target));
      this.setDatabaseWorkspace(target, workspace);
    } catch (error) {
      this.errors.update(errors => ({
        ...errors,
        [target === 'branch' ? 'branchDatabase' : 'cashierDatabase']: this.userFacingError(classifyPosAgentError(error))
      }));
    }
  }

  private setDatabaseWorkspace(target: RmsDatabaseTarget, workspace: RmsDatabaseWorkspace | null): void {
    if (target === 'branch') this.branchDatabaseWorkspace.set(workspace);
    else this.cashierDatabaseWorkspace.set(workspace);
  }

  private notifyDatabaseOutcome(operation: RmsDatabaseOperation): void {
    switch (operation.outcome) {
      case 'completed':
        this.toast.showSuccess(`${operation.databaseDisplayName} ${operation.operation} completed.`);
        return;
      case 'failed':
        this.toast.showError(`${operation.databaseDisplayName} ${operation.operation} failed.`);
        return;
      case 'outcomeUnknown':
        this.toast.showWarning(`${operation.databaseDisplayName} ${operation.operation} outcome is unknown. Inspect the database and service state before retrying.`);
        return;
      default:
        this.toast.showInfo(`${operation.databaseDisplayName} ${operation.operation} was not attempted.`);
    }
  }

  private databaseActionErrorMessage(error: PosAgentTransportError): string {
    switch (error.kind) {
      case 'authenticationRequired': return 'Windows authentication is required for database backup or restore.';
      case 'originRejected': return 'The Support Hub origin is not accepted by the POS Agent.';
      case 'notAuthorized': return 'The signed-in Windows account is not authorized for database backup or restore.';
      case 'transportUnavailableOrBlocked': return 'The fixed POS Agent endpoint could not be reached or the browser blocked the database action.';
      case 'agentServerError': return 'The POS Agent reported a database-operation error without exposing implementation details.';
      case 'contractMismatch': return 'The POS Agent rejected the typed database-operation contract.';
      default: return 'The POS Agent did not complete the database action.';
    }
  }

  private delay(milliseconds: number): Promise<void> {
    return new Promise(resolve => globalThis.setTimeout(resolve, milliseconds));
  }

  private safeDownloadName(fileName: string): string {
    const safeName = fileName.replace(/[^a-zA-Z0-9._-]/g, '_').replace(/^\.+/, '');
    return safeName || 'rms-artifact.zip';
  }

  private notifyDownloaderOutcome(operation: DownloaderOperation): void {
    switch (operation.outcome) {
      case 'completed':
        this.toast.showSuccess('The downloader completed and published approved artifacts.');
        return;
      case 'failed':
        this.toast.showError('The downloader did not complete the requested branch batch.');
        return;
      case 'outcomeUnknown':
        this.toast.showWarning('The downloader outcome is unknown. Inspect the Agent evidence before deciding whether to retry.');
        return;
      default:
        this.toast.showInfo('The downloader request was not attempted.');
    }
  }

  private notifyMaintenanceOutcome(operation: MaintenanceOperation): void {
    const action = operation.mode === 'cleanup' ? 'Cleanup' : 'Branch reset';
    switch (operation.outcome) {
      case 'completed':
        this.toast.showSuccess(`${action} completed under the Agent policy.`);
        return;
      case 'failed':
        this.toast.showError(`${action} failed; review the retained Agent outcome.`);
        return;
      case 'outcomeUnknown':
        this.toast.showWarning(`${action} outcome is unknown. Verify recovery evidence before retrying.`);
        return;
      default:
        this.toast.showInfo(`${action} was not attempted.`);
    }
  }

  private operatorErrorMessage(error: PosAgentTransportError): string {
    switch (error.kind) {
      case 'authenticationRequired': return 'Windows authentication is required for this Agent operation.';
      case 'originRejected': return 'The Support Hub origin is not accepted by the POS Agent.';
      case 'notAuthorized': return 'The signed-in Windows account is not authorized for this Agent operation.';
      case 'transportUnavailableOrBlocked': return 'The fixed POS Agent endpoint could not be reached or the browser blocked this operation.';
      case 'contractMismatch': return 'The POS Agent rejected the typed operation contract.';
      case 'agentServerError': return 'The POS Agent reported an operation error without exposing implementation details.';
      default: return 'The POS Agent did not complete the requested operation.';
    }
  }

  componentInstallLabel(rms: RmsDiagnostics): string {
    const installed = [
      rms.installation.branchInstalled ? 'Branch' : null,
      rms.installation.cashierInstalled ? 'Cashier' : null
    ].filter((value): value is string => value !== null);
    return installed.length ? installed.join(' + ') : 'Not detected';
  }

  consistencyLabel(state: components['schemas']['RmsConsistencyState']): string {
    switch (state) {
      case 'consistent': return 'Consistent';
      case 'mismatch': return 'Mismatch';
      default: return 'Unavailable';
    }
  }

  consistencyVariant(state: components['schemas']['RmsConsistencyState']): 'success' | 'warning' | 'info' {
    return state === 'consistent' ? 'success' : state === 'mismatch' ? 'warning' : 'info';
  }

  endpointConfiguredLabel(endpoint: RmsEndpointDiagnostic): string {
    return endpoint.configured ? this.freshnessLabel(endpoint.reachability) : 'Not configured';
  }

  endpointConfiguredVariant(endpoint: RmsEndpointDiagnostic): 'success' | 'warning' | 'info' {
    return endpoint.configured ? this.freshnessVariant(endpoint.reachability) : 'info';
  }

  databaseStatusLabel(database: RmsDatabaseDiagnostic): string {
    switch (database.connectivityStatus) {
      case 'reachable': return 'Reachable';
      case 'authenticationFailed': return 'Authentication failed';
      case 'databaseUnavailable': return 'Database unavailable';
      case 'databaseNameMismatch': return 'Name mismatch';
      case 'configurationInvalid': return 'Configuration invalid';
      case 'unreachable': return 'Unreachable';
      default: return 'Not configured';
    }
  }

  databaseStatusVariant(database: RmsDatabaseDiagnostic): 'success' | 'warning' | 'danger' | 'info' {
    switch (database.connectivityStatus) {
      case 'reachable': return 'success';
      case 'databaseNameMismatch':
      case 'configurationInvalid':
      case 'authenticationFailed':
      case 'databaseUnavailable': return 'warning';
      case 'unreachable': return 'danger';
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

    const [live, session, identity, connectivity, capabilities, configuration, services, rms, branchDatabase, cashierDatabase, downloaderBranches] = await Promise.all([
      this.settle(this.transport.getLive()),
      this.settle(this.transport.getSession()),
      this.settle(this.transport.getDeviceIdentity()),
      this.settle(this.transport.getDeviceConnectivity()),
      this.settle(this.transport.getDeviceCapabilities()),
      this.settle(this.transport.getConfiguration()),
      this.settle(this.transport.getServices()),
      this.settle(this.transport.getRmsDiagnostics()),
      this.settle(this.transport.getRmsDatabaseWorkspace('branch')),
      this.settle(this.transport.getRmsDatabaseWorkspace('cashier')),
      this.settle(this.transport.getDownloaderBranches())
    ]);

    const nextErrors: Record<string, string> = {};
    this.applyLive(live, nextErrors);
    this.applySession(session, nextErrors);
    this.applyValue('identity', identity, this.identity, nextErrors);
    this.applyValue('connectivity', connectivity, this.connectivity, nextErrors);
    this.applyValue('capabilities', capabilities, this.capabilities, nextErrors);
    this.applyValue('configuration', configuration, this.configuration, nextErrors);
    this.applyValue('services', services, this.services, nextErrors);
    this.applyValue('rms', rms, this.rmsDiagnostics, nextErrors);
    this.applyValue('branchDatabase', branchDatabase, this.branchDatabaseWorkspace, nextErrors);
    this.applyValue('cashierDatabase', cashierDatabase, this.cashierDatabaseWorkspace, nextErrors);
    this.applyDownloaderBranches(downloaderBranches, nextErrors);
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
