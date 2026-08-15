import { CommonModule } from '@angular/common';
import { Component, OnDestroy, inject, signal, WritableSignal } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import { components } from '../../core/pos-agent/generated/pos-agent-api.generated';
import { PosAgentTransportError, classifyPosAgentError } from '../../core/pos-agent/pos-agent-error';
import { PosAgentTransportService } from '../../core/pos-agent/pos-agent-transport.service';
import { POS_AGENT_OPERATION_IDS } from '../../core/pos-agent/pos-agent.constants';
import { BuildIdentityService } from '../../core/services/build-identity.service';
import { ToastService } from '../../core/services/toast.service';
import { NavbarComponent } from '../../layout/navbar/navbar.component';
import {
  ConfirmDialogComponent,
  PageHeaderComponent,
  UiButtonComponent
} from '../../shared/ui';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

type HealthStatus = components['schemas']['HealthStatusDto'];
type HealthReport = components['schemas']['HealthReportDto'];
type MainServerProfiles = components['schemas']['MainServerProfilesDto'];
type MainServerState = components['schemas']['MainServerStateEvidenceDto'];
type SafetySnapshotPreview = components['schemas']['SafetySnapshotPreviewDto'];
type SafetySnapshot = components['schemas']['SafetySnapshotDto'];
type DiagnosticConsoleTarget = components['schemas']['DiagnosticConsoleTargetDto'];
type DiagnosticConsolePreview = components['schemas']['DiagnosticConsolePreviewDto'];
type DiagnosticConsoleRun = components['schemas']['DiagnosticConsoleRunDto'];
type AgentPackageStatus = components['schemas']['AgentPackageStatusDto'];
type AgentPackagePreview = components['schemas']['AgentPackagePreviewDto'];
type AgentPackageOperation = components['schemas']['AgentPackageOperationDto'];
type RepairOperationKind = components['schemas']['RepairOperationKindDto'];
type RepairPreview = components['schemas']['RepairPreviewDto'];
type RepairOperation = components['schemas']['RepairOperationDto'];
type GuidedRepair = components['schemas']['GuidedRepairDto'];
type HealthState = components['schemas']['HealthState'];
type HealthCheck = components['schemas']['HealthCheckDto'];
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
type RmsOperationalHealth = components['schemas']['RmsOperationalHealthDto'];
type RmsFixedRootHealth = components['schemas']['RmsFixedRootHealthDto'];
type RmsFixedRootState = components['schemas']['RmsFixedRootStateDto'];
type RmsDatabaseDiagnostic = components['schemas']['RmsDatabaseDiagnosticDto'];
type RmsEndpointDiagnostic = components['schemas']['RmsEndpointDiagnosticDto'];
type RmsDatabaseTarget = components['schemas']['RmsDatabaseTarget'];
type RmsDatabaseWorkspace = components['schemas']['RmsDatabaseWorkspaceDto'];
type RmsDatabaseOperation = components['schemas']['RmsDatabaseOperationDto'];
type RmsDatabaseArtifact = components['schemas']['RmsDatabaseArtifactDto'];
type RmsDatabaseHealth = components['schemas']['RmsDatabaseHealthDto'];
type RmsComponentDriftState = components['schemas']['RmsComponentDriftState'];
type ServiceFailureAnalysis = components['schemas']['ServiceFailureAnalysisDto'];
type IncidentTimeline = components['schemas']['IncidentTimelineDto'];
type SupportBundle = components['schemas']['SupportBundleDto'];
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
type PendingSliceBAction = Readonly<{
  kind: 'snapshot' | 'console' | 'package' | 'repair' | 'guided';
  previewId?: string;
  guidedRepairId?: string;
  stepId?: string;
  snapshotId?: string;
  operationId?: string;
  confirmationText: string;
  message: string;
}>;

const POS_MAINTENANCE_TEMPLATE = `
  <app-navbar></app-navbar>

  <main class="pos-shell" aria-label="POS Maintenance service control and evidence">
    <app-page-header
      title="POS Maintenance"
      [subtitle]="operatorHeaderLine()"
      [compact]="true">
      <ui-button
        variant="secondary"
        size="sm"
        icon="bi-arrow-clockwise"
        [loading]="refreshing()"
        loadingLabel="Refreshing"
        [ariaLabel]="refreshing() ? 'Refreshing POS Agent data' : 'Refresh POS Agent data'"
        (pressed)="refresh()">
        Refresh
      </ui-button>
    </app-page-header>

    <section class="ops-command-center" aria-labelledby="ops-command-center-title">
      <div class="ops-command-center__signal">
        <div class="section-kicker">RMS Support Agent / Local operations</div>
        <div class="ops-command-center__headline">
          <div>
            <h2 id="ops-command-center-title">A clear lane from signal to action</h2>
            <p>Read the machine-owned evidence first. Every action below stays typed, bounded, and visible before it can cross the Agent boundary.</p>
          </div>
          <div class="ops-status-orbit" [class.ops-status-orbit--healthy]="health()?.overallState === 'healthy'" [class.ops-status-orbit--warning]="health()?.overallState === 'warning'" [class.ops-status-orbit--danger]="health()?.overallState === 'actionRequired'" role="status" aria-live="polite">
            <span class="ops-status-orbit__ring" aria-hidden="true"><span></span></span>
            <span class="ops-status-orbit__copy"><small>Current lane</small><strong>{{ healthStateLabel(health()?.overallState) }}</strong></span>
          </div>
        </div>
      </div>
      <div class="ops-command-center__metrics" role="list" aria-label="POS Agent command metrics">
        <div class="ops-metric" role="listitem"><span class="ops-metric__index">01</span><span class="ops-metric__label">Agent</span><strong>{{ agentStatusLabel() }}</strong><small>{{ agentVersion() }} / API {{ apiVersion() }}</small></div>
        <div class="ops-metric" role="listitem"><span class="ops-metric__index">02</span><span class="ops-metric__label">Product Release</span><strong>{{ productRelease() }}</strong><small>{{ rmsDiagnostics()?.installation?.installationMode || 'Awaiting installation evidence' }}</small></div>
        <div class="ops-metric" role="listitem"><span class="ops-metric__index">03</span><span class="ops-metric__label">Root coverage</span><strong>{{ operationalRootSummary() }}</strong><small>Fixed roots / bounded aggregate reads</small></div>
        <div class="ops-metric" role="listitem"><span class="ops-metric__index">04</span><span class="ops-metric__label">Attachment aggregate</span><strong>{{ operationalAttachmentSummary() }}</strong><small>No patient or insurance identity leaves the Agent</small></div>
      </div>
      <div class="ops-command-center__scope"><i class="bi bi-shield-check" aria-hidden="true"></i><span><strong>Safe scope:</strong> fixed RMS roots, typed diagnostics, allow-listed services, and opaque Agent operations.</span><span class="ops-command-center__timestamp">{{ health()?.checkedAtUtc ? ('Last signal ' + formatSignalTime(health()?.checkedAtUtc)) : 'Waiting for first signal' }}</span></div>
    </section>

    <section class="peer-status" aria-labelledby="peer-status-title">
      <div class="section-kicker">Operator workspace</div>
      <div class="section-heading section-heading--compact">
        <div>
        <h2 id="peer-status-title">Live evidence at a glance</h2>
          <p>Five peer states keep the first decision visible: what is healthy, what is stale, and what needs attention.</p>
        </div>
        <span class="last-read" aria-live="polite">{{ health()?.checkedAtUtc ? ('Checked ' + health()?.checkedAtUtc) : 'Waiting for the first health check' }}</span>
      </div>
      <div class="peer-status__grid" role="list">
        <div class="peer-status__item" role="listitem">
          <span class="peer-status__label">POS Agent</span>
          <app-status-badge [label]="agentStatusLabel()" [variant]="agentStatusVariant()" role="status"></app-status-badge>
          <span class="peer-status__detail">{{ agentStatusDetail() }}</span>
        </div>
        <div class="peer-status__item" role="listitem">
          <span class="peer-status__label">Windows Auth / Authorization</span>
          <app-status-badge [label]="authStatusLabel() + ' · ' + authorizationLabel()" [variant]="authStatusVariant()" role="status"></app-status-badge>
          <span class="peer-status__detail">{{ authStatusDetail() }}</span>
        </div>
        <div class="peer-status__item" role="listitem">
          <span class="peer-status__label">Main Server</span>
          <app-status-badge [label]="peerHealthLabel('main-server')" [variant]="peerHealthVariant('main-server')" role="status"></app-status-badge>
          <span class="peer-status__detail">{{ peerHealthDetail('main-server') }}</span>
        </div>
        <div class="peer-status__item" role="listitem">
          <span class="peer-status__label">Configuration Consistency</span>
          <app-status-badge [label]="consistencyHeaderLabel()" [variant]="consistencyHeaderVariant()" role="status"></app-status-badge>
          <span class="peer-status__detail">{{ consistencyHeaderDetail() }}</span>
        </div>
        <div class="peer-status__item peer-status__item--overall" role="listitem">
          <span class="peer-status__label">Overall Health</span>
          <app-status-badge [label]="healthStateLabel(health()?.overallState)" [variant]="healthStateVariant(health()?.overallState)" role="status"></app-status-badge>
          <span class="peer-status__detail">{{ health()?.summary || 'Health evidence is not available yet.' }}</span>
        </div>
      </div>
    </section>

    @if (globalError()) {
      <section class="workspace-notice" role="alert" aria-label="POS Agent read warning">
        <i class="bi bi-exclamation-triangle" aria-hidden="true"></i>
        <div><strong>Some direct Agent reads are unavailable.</strong><p>{{ globalError() }}</p></div>
      </section>
    }

    <section class="workspace-panel installation-panel" aria-labelledby="installation-title">
      <div class="section-heading">
        <div><div class="section-kicker">01 / Identity</div><h2 id="installation-title">Installation</h2><p>Server-owned RMS identity and release evidence. Values are read from known installation files only.</p></div>
        <app-status-badge [label]="rmsDiagnostics() ? componentInstallLabel(rmsDiagnostics()!) : 'Reading'" [variant]="rmsDiagnostics() ? 'success' : 'info'" role="status"></app-status-badge>
      </div>
      @if (rmsDiagnostics()?.installation; as installation) {
        <div class="field-grid field-grid--six">
          <div class="field"><span>Branch</span><strong>{{ installation.branchCode || 'Unavailable' }}</strong></div>
          <div class="field"><span>POS</span><strong>{{ installation.posNumber || 'Unavailable' }}</strong></div>
          <div class="field"><span>Client</span><strong>{{ installation.clientName || 'Unavailable' }}</strong></div>
          <div class="field"><span>Product Release</span><strong>{{ productRelease() }}</strong></div>
          <div class="field"><span>Mode</span><strong>{{ installation.installationMode || 'Unavailable' }}</strong></div>
          <div class="field"><span>GUID</span><strong>{{ installation.installationGuid || 'Unavailable' }}</strong></div>
        </div>
      } @else {
        <p class="empty-copy">{{ readError('rms') }}</p>
      }
      <details class="advanced-disclosure">
        <summary>Advanced installation evidence <span>Builds, detailed connectivity, and consistency</span></summary>
        <div class="advanced-grid">
          <div class="evidence-block"><h3>Component builds</h3><dl class="key-value-list"><div><dt>Branch Server</dt><dd>{{ rmsDiagnostics()?.installation?.versions?.branchServerBuildNumber || 'Unavailable' }}</dd></div><div><dt>Cashier Server</dt><dd>{{ rmsDiagnostics()?.installation?.versions?.cashierServerBuildNumber || 'Unavailable' }}</dd></div><div><dt>Cashier UI</dt><dd>{{ rmsDiagnostics()?.installation?.versions?.cashierUiBuildNumber || 'Unavailable' }}</dd></div><div><dt>Product Release</dt><dd>{{ productRelease() }}</dd></div></dl></div>
          <div class="evidence-block"><h3>Detailed connectivity</h3><dl class="key-value-list"><div><dt>Main Server</dt><dd>{{ endpointText(rmsDiagnostics()?.connectivity?.mainServer) }}</dd></div><div><dt>Branch Server</dt><dd>{{ endpointText(rmsDiagnostics()?.connectivity?.branchServer) }}</dd></div><div><dt>Main endpoint read</dt><dd>{{ rmsDiagnostics()?.connectivity?.mainServer?.reachability?.detail || 'Unavailable' }}</dd></div><div><dt>Branch endpoint read</dt><dd>{{ rmsDiagnostics()?.connectivity?.branchServer?.reachability?.detail || 'Unavailable' }}</dd></div></dl></div>
          <div class="evidence-block evidence-block--wide"><h3>Release drift</h3><div class="drift-list">@for (drift of rmsDiagnostics()?.installation?.componentDrift || []; track drift.component) { <div class="drift-row"><span>{{ drift.component }}</span><app-status-badge [label]="driftLabel(drift.state)" [variant]="driftVariant(drift.state)" role="status"></app-status-badge><small>{{ drift.reason }}</small></div> } @empty { <span class="empty-copy">Release drift evidence is unavailable.</span> }</div></div>
          <div class="evidence-block evidence-block--wide"><h3>Consistency</h3><div class="warning-list">@for (warning of rmsDiagnostics()?.installation?.consistency?.warnings || []; track warning) { <span>{{ warning }}</span> } @empty { <span>Known duplicated values are consistent or no warning was returned.</span> }</div></div>
        </div>
      </details>
    </section>

    <section class="workspace-panel operational-health-panel" aria-labelledby="operational-health-title">
      <div class="section-heading">
        <div><div class="section-kicker">01A / Storage signal</div><h2 id="operational-health-title">Fixed-root operational health</h2><p>Aggregate evidence for RMS setup, update, data, log, and attachment roots. Names, paths, file content, and identity-bearing attachment details stay on the machine.</p></div>
        <app-status-badge [label]="operationalHealthLabel()" [variant]="operationalHealthVariant()" role="status"></app-status-badge>
      </div>
      @if (operationalHealth(); as operational) {
        <div class="operational-health-layout">
          <div class="root-signal-list" aria-label="Fixed RMS root health">
            @for (root of operational.fixedRoots; track root.rootId) {
              <article class="root-signal" [class.root-signal--warning]="root.state === 'stale' || root.state === 'inaccessible'" [class.root-signal--danger]="root.state === 'missing'">
                <div class="root-signal__heading"><div><span class="root-signal__id">{{ root.rootId }}</span><h3>{{ root.displayName }}</h3></div><app-status-badge [label]="rootStateLabel(root.state)" [variant]="rootStateVariant(root.state)" role="status"></app-status-badge></div>
                <div class="root-signal__facts"><span><strong>{{ numberValue(root.fileCount).toLocaleString() }}</strong> files</span><span><strong>{{ formatBytes(numberValue(root.totalBytes)) }}</strong> observed</span><span><strong>{{ root.newestFileUtc ? formatSignalTime(root.newestFileUtc) : 'No timestamp' }}</strong> newest write</span></div>
                <p>{{ root.detail }}</p>
              </article>
            } @empty {
              <p class="empty-copy">{{ readError('operationalHealth') }}</p>
            }
          </div>
          <aside class="operational-health-rail" aria-label="Update and attachment aggregates">
            <div class="health-rail-card"><span class="health-rail-card__label">Update lane</span><strong>{{ operational.updates.productRelease || 'Release unavailable' }}</strong><app-status-badge [label]="operational.updates.packageState" [variant]="updateStateVariant(operational.updates.packageState)" role="status"></app-status-badge><p>{{ operational.updates.detail }}</p></div>
            <div class="health-rail-card health-rail-card--quiet"><span class="health-rail-card__label">Insurance attachments</span><strong>{{ numberValue(operational.insuranceAttachments.attachmentCount).toLocaleString() }} / {{ formatBytes(numberValue(operational.insuranceAttachments.totalBytes)) }}</strong><span class="health-rail-card__subline">aggregate files / bytes</span><p>{{ operational.insuranceAttachments.detail }}</p></div>
          </aside>
        </div>
        <p class="operational-health-footer"><i class="bi bi-clock-history" aria-hidden="true"></i> Checked {{ formatSignalTime(operational.checkedAtUtc) }} · bounded to the server-owned root catalog · no arbitrary browser filesystem scope.</p>
      } @else {
        <p class="empty-copy">{{ readError('operationalHealth') }}</p>
      }
    </section>

    <section class="workspace-panel database-panel" aria-labelledby="database-title">
      <div class="section-heading"><div><div class="section-kicker">02 / Recovery evidence</div><h2 id="database-title">Database health</h2><p>Branch and Cashier stay side by side on desktop and stack cleanly on narrow screens.</p></div><ui-button variant="secondary" size="sm" icon="bi-heart-pulse" (pressed)="runHealthCheck()">Health Check</ui-button></div>
      <div class="database-grid">
        @for (target of databaseTargets; track target) {
          <article class="database-card" [attr.aria-labelledby]="target + '-database-title'">
            <div class="database-card__heading"><div><div class="section-kicker">{{ target === 'branch' ? 'Branch' : 'Cashier' }}</div><h3 [id]="target + '-database-title'">{{ databaseTargetLabel(target) }}</h3><p>{{ databaseDiagnostic(target)?.serverDisplay || 'Server display unavailable' }}</p></div><app-status-badge [label]="databaseStatusLabel(databaseDiagnostic(target))" [variant]="databaseStatusVariant(databaseDiagnostic(target))" role="status"></app-status-badge></div>
            <dl class="key-value-list database-facts"><div><dt>Configured DB</dt><dd>{{ databaseDiagnostic(target)?.configuredDatabase || 'Unavailable' }}</dd></div><div><dt>Expected DB</dt><dd>{{ databaseDiagnostic(target)?.expectedDatabase || databaseTargetLabel(target) }}</dd></div><div><dt>Approved backups</dt><dd>{{ databaseHealth(target)?.backups?.count ?? databaseWorkspace(target)?.approvedBackups?.length ?? 0 }}</dd></div><div><dt>Latest backup</dt><dd>{{ latestBackupLabel(target) }}</dd></div><div><dt>Freshness</dt><dd>{{ databaseHealthFreshnessLabel(target) }}</dd></div><div><dt>Storage</dt><dd>{{ databaseHealth(target)?.storage?.summary || 'Capacity evidence unavailable' }}</dd></div></dl>
            <p class="evidence-copy">{{ databaseDiagnostic(target)?.evidence?.detail || readError(target + 'Database') }}</p>
            <div class="database-actions" aria-label="Database actions">
              <ui-button variant="primary" size="sm" icon="bi-cloud-arrow-up" [disabled]="!canControlDatabases()" [ariaLabel]="'Back up ' + databaseTargetLabel(target)" (pressed)="requestDatabaseBackup(target)">Backup</ui-button>
              <ui-button variant="danger" size="sm" icon="bi-arrow-counterclockwise" [disabled]="!canRestore(target)" [ariaLabel]="restoreDisabledReason(target)" (pressed)="restoreLatestBackup(target)">Restore</ui-button>
              <ui-button variant="secondary" size="sm" icon="bi-archive" (pressed)="toggleBackupList(target)">View Backups</ui-button>
              <ui-button variant="ghost" size="sm" icon="bi-heart-pulse" (pressed)="runHealthCheck()">Health</ui-button>
            </div>
            @if (!canRestore(target)) { <p class="action-hint" role="note">{{ restoreDisabledReason(target) }}</p> }
            @if (backupListOpen(target)) {
              <div class="backup-list" aria-label="Approved backups">
                @for (artifact of databaseWorkspace(target)?.approvedBackups || []; track artifact.artifactId) { <div class="backup-row"><div><strong>{{ artifact.displayName }}</strong><small>{{ formatArtifactSize(artifact) }} · {{ formatArtifactDate(artifact) }}</small></div><ui-button variant="ghost" size="sm" icon="bi-arrow-counterclockwise" [disabled]="!canControlDatabases()" (pressed)="requestDatabaseRestore(target, artifact)">Restore</ui-button></div> } @empty { <span class="empty-copy">No approved backups are retained for this database. Restore remains disabled.</span> }
              </div>
            }
            @if (databaseWorkspace(target)?.latestOperation; as operation) { <div class="operation-strip" role="status"><strong>{{ databaseOperationLabel(operation) }}</strong><span>{{ operation.detail }}</span></div> }
          </article>
        }
      </div>
    </section>

    <section class="workspace-panel services-panel" aria-labelledby="services-title">
      <div class="section-heading"><div><div class="section-kicker">03 / Service control</div><h2 id="services-title">RMS services</h2><p>Only the three canonical RMS services are shown here. Diagnostics and state-valid actions remain separate.</p></div><span class="last-read">{{ services()?.length || 0 }} allow-listed services</span></div>
      <div class="service-table-wrap"><table class="service-table"><caption class="sr-only">Canonical RMS Windows service state and typed actions</caption><thead><tr><th scope="col">Service</th><th scope="col">State</th><th scope="col">Diagnostics</th><th scope="col">Actions</th></tr></thead><tbody>@for (service of services() || []; track service.serviceId) { <tr><th scope="row"><span class="service-name">{{ service.displayName }}</span><small>{{ service.lastChecked.detail }}</small></th><td><app-status-badge [label]="serviceStateLabel(service.state)" [variant]="serviceStateVariant(service.state)" role="status"></app-status-badge></td><td><ui-button variant="secondary" size="sm" icon="bi-search" [ariaLabel]="'Diagnose ' + service.displayName" [loading]="diagnosingService() === service.serviceId" [disabled]="diagnosingService() !== null" (pressed)="diagnoseServiceFailure(service.serviceId)">Diagnose</ui-button></td><td><div class="service-actions">@if (canControlServices()) { @for (action of service.allowedActions; track action) { <ui-button variant="ghost" size="sm" [icon]="action === 'start' ? 'bi-play' : action === 'stop' ? 'bi-stop' : 'bi-arrow-repeat'" (pressed)="requestServiceAction(service, action)">{{ actionLabel(action) }}</ui-button> } @empty { <span class="muted">No state-valid action</span> } } @else { <span class="muted">Authorization required</span> } @if (actionOutcome(service); as outcome) { <span class="muted">{{ actionOutcomeLabel(outcome.outcome) }}</span> }</div></td></tr> } @empty { <tr><td colspan="4" class="empty-copy">{{ readError('services') }}</td></tr> }</tbody></table></div>
      @if (failureAnalysis(); as analysis) { <div class="analysis-panel" role="status" aria-live="polite"><div class="analysis-panel__heading"><div><div class="section-kicker">Service Failure Analyzer</div><h3>{{ analysis.serviceDisplayName }}</h3></div><app-status-badge [label]="failureCategoryLabel(analysis.category)" [variant]="failureSeverityVariant(analysis.severity)" role="status"></app-status-badge></div><p>{{ analysis.summary }}</p><div class="analysis-grid"><div><strong>Confidence</strong><span>{{ analysis.confidence }}</span></div><div><strong>Evidence</strong><span>{{ analysis.evidence.length }} bounded item(s)</span></div><div><strong>Unknowns</strong><span>{{ analysis.unknownReasons.length }}</span></div></div>@if (analysis.evidence.length) { <details><summary>Exception / stack evidence</summary><div class="evidence-list">@for (item of analysis.evidence; track item.source + item.eventId) { <div><strong>{{ item.source }}</strong><span>{{ item.summary }}</span>@if (item.exceptionType) { <small>{{ item.exceptionType }}</small> } @for (frame of item.stackFrames; track frame) { <small>{{ frame }}</small> }</div> }</div></details> }</div> }
    </section>

    <section class="workspace-panel maintenance-panel" aria-labelledby="maintenance-title">
      <div class="section-heading"><div><div class="section-kicker">04 / Operator actions</div><h2 id="maintenance-title">Maintenance</h2><p>Slice A controls remain preserved. Slice B adds truthful server-owned previews for Main Server evidence, diagnostics, safety snapshots, package lifecycle, and repair.</p></div></div>
      <div class="action-grid action-grid--primary">
        <ui-button variant="primary" icon="bi-heart-pulse" [loading]="loadingHealth()" (pressed)="runHealthCheck()">Run Health Check</ui-button>
        <ui-button variant="secondary" icon="bi-activity" (pressed)="viewIncidentTimeline()">View Incident Timeline</ui-button>
        <ui-button variant="secondary" icon="bi-box-arrow-down" [loading]="generatingBundle()" [disabled]="!canOperate()" (pressed)="generateSupportBundle()">Generate Support Bundle</ui-button>
      </div>
      @if (supportBundle(); as bundle) { <div class="result-strip" role="status"><strong>Support Bundle ready</strong><span>{{ bundle.artifact.displayName }} · {{ formatBytes(numberValue(bundle.artifact.sizeBytes)) }}</span><ui-button variant="ghost" size="sm" icon="bi-download" (pressed)="downloadArtifact(bundle.artifact.artifactId, bundle.artifact.displayName)">Download</ui-button></div> }
      <div class="operator-grid">
        <article class="operator-card"><div class="operator-card__heading"><div><div class="section-kicker">Preserved PR #10</div><h3>Downloader / Artifact</h3></div><app-status-badge label="Available" variant="success" role="status"></app-status-badge></div><p>Trigger the typed Downloader flow and download only server-registered opaque artifacts.</p><div class="branch-picker">@for (branch of downloaderBranches() || []; track branch.branchCode) { <label class="branch-chip"><input type="checkbox" [checked]="isBranchSelected(branch.branchCode)" (change)="toggleBranch(branch.branchCode)" />{{ branch.branchCode }}</label> } @empty { <span class="empty-copy">{{ readError('downloaderBranches') }}</span> }</div><ui-button variant="secondary" size="sm" icon="bi-cloud-download" [loading]="submittingDownloader()" [disabled]="!canOperate() || !selectedBranches().length" (pressed)="startDownloader()">Start Downloader</ui-button>@if (downloaderOperation(); as operation) { <p class="result-copy" role="status">{{ downloaderOperationLabel(operation) }}</p> }</article>
        <article class="operator-card"><div class="operator-card__heading"><div><div class="section-kicker">Preserved PR #10</div><h3>Cleanup / Branch Reset</h3></div><app-status-badge label="Available" variant="warning" role="status"></app-status-badge></div><p>Preview and confirm the existing policy-bound cleanup and branch reset workflows.</p><div class="action-row"><ui-button variant="secondary" size="sm" [loading]="previewingMaintenance() === 'cleanup'" [disabled]="!canOperate()" (pressed)="previewCleanup()">Preview Cleanup</ui-button><ui-button variant="danger" size="sm" [loading]="previewingMaintenance() === 'branch-reset'" [disabled]="!canOperate()" (pressed)="previewBranchReset()">Preview Branch Reset</ui-button></div>@if (cleanupPreview(); as preview) { <p class="result-copy">Cleanup: {{ preview.ready ? 'Ready for confirmation' : 'Rejected by policy' }}</p> } @if (branchResetPreview(); as preview) { <p class="result-copy">Branch reset: {{ preview.ready ? 'Ready for confirmation' : 'Rejected by policy' }}</p> }</article>
      </div>
      <div class="slice-b-grid" aria-label="Slice B operator boundaries">
        <article class="operator-card slice-b-card">
          <div class="operator-card__heading"><div><div class="section-kicker">Read-only profile</div><h3>Main Server state</h3></div><app-status-badge [label]="mainServerBindingLabel(mainServerProfiles()?.activeBinding)" [variant]="mainServerBindingVariant(mainServerProfiles()?.activeBinding)" role="status"></app-status-badge></div>
          <p>{{ mainServerDetail() }}</p>
          @if (mainServerState(); as state) { <dl class="slice-b-facts"><div><dt>Environment</dt><dd>{{ state.environment }}</dd></div><div><dt>Branch / POS</dt><dd>{{ state.branchCode || 'Unavailable' }} / {{ state.posNumber || 'Unavailable' }}</dd></div><div><dt>Read outcome</dt><dd>{{ state.outcome }}</dd></div></dl> }
          <p class="boundary-copy"><i class="bi bi-eye" aria-hidden="true"></i> Only fixed Agent-owned GET projections are permitted. Branch/POS installation-state PUT acknowledgements are not called.</p>
        </article>

        <article class="operator-card slice-b-card">
          <div class="operator-card__heading"><div><div class="section-kicker">Before repair</div><h3>Safety Snapshot</h3></div><app-status-badge [label]="snapshotStateLabel(safetySnapshot())" [variant]="snapshotVariant(safetySnapshot())" role="status"></app-status-badge></div>
          <p>{{ safetySnapshot()?.detail || safetySnapshotPreview()?.blockers?.[0] || 'Capture a fresh, integrity-protected evidence record before any package or repair operation.' }}</p>
          <div class="action-row"><ui-button variant="secondary" size="sm" icon="bi-search" [disabled]="!canOperate()" (pressed)="previewSafetySnapshot()">Preview snapshot</ui-button><ui-button variant="primary" size="sm" icon="bi-shield-check" [loading]="capturingSafetySnapshot()" [disabled]="!canOperate() || !safetySnapshotPreview()?.ready" (pressed)="requestSafetySnapshotCapture()">Capture snapshot</ui-button></div>
          @if (safetySnapshot(); as snapshot) { <p class="result-copy">Opaque snapshot {{ snapshot.snapshotId }} · expires {{ snapshot.expiresAtUtc }}</p> }
          @if (safetySnapshotPreview()?.blockers?.length) { <ul class="slice-b-blockers">@for (blocker of safetySnapshotPreview()?.blockers || []; track blocker) { <li>{{ blocker }}</li> }</ul> }
        </article>
      </div>

      <div class="slice-b-grid slice-b-grid--wide" aria-label="Slice B local diagnostic and package boundaries">
        <article class="operator-card slice-b-card">
          <div class="operator-card__heading"><div><div class="section-kicker">Fixed manifest</div><h3>Safe Diagnostic Console</h3></div><app-status-badge [label]="consoleStateLabel(consoleRun()?.outcome)" [variant]="consoleStateVariant(consoleRun()?.outcome)" role="status"></app-status-badge></div>
          <p>Choose a logical target only. The Agent resolves the exact executable, arguments, working directory, empty environment, timeout, and output limits.</p>
          <label class="slice-b-label" for="diagnostic-target">Logical target</label><select id="diagnostic-target" class="slice-b-select" [value]="consoleTarget()" (change)="setConsoleTarget($any($event.target).value)"><option value="branchServerApi">Branch Server API</option><option value="cashierServerApi">Cashier Server API</option><option value="serviceManager">RMS Services Manager</option><option value="cashierUi">Cashier UI</option></select>
          <div class="action-row"><ui-button variant="secondary" size="sm" icon="bi-search" [loading]="previewingConsole()" [disabled]="!canOperate()" (pressed)="previewDiagnosticConsole()">Preview run</ui-button><ui-button variant="primary" size="sm" icon="bi-terminal" [loading]="startingConsole()" [disabled]="!canOperate() || !consolePreview()?.ready" (pressed)="requestDiagnosticConsoleRun()">Run diagnostics</ui-button></div>
          @if (consolePreview(); as preview) { <p class="result-copy">{{ preview.displayName }} · {{ preview.ready ? 'Ready for exact confirmation' : preview.blockers.join(', ') }}</p> }
          @if (consoleRun(); as run) { @if (run.result; as result) { <div class="artifact-actions">@if (result.stdoutArtifactId; as artifactId) { <button type="button" class="artifact-link" (click)="downloadArtifact(artifactId, 'diagnostic-stdout.txt')">Download stdout</button> } @if (result.stderrArtifactId; as artifactId) { <button type="button" class="artifact-link" (click)="downloadArtifact(artifactId, 'diagnostic-stderr.txt')">Download stderr</button> }<span>{{ result.stdoutLines + ' stdout lines · ' + result.stderrLines + ' stderr lines' }}</span></div> } }
          <p class="boundary-copy"><i class="bi bi-lock" aria-hidden="true"></i> No shell, arbitrary path, arbitrary arguments, inherited secret environment, or user-selected process is exposed.</p>
        </article>

        <article class="operator-card slice-b-card">
          <div class="operator-card__heading"><div><div class="section-kicker">Versioned boundary</div><h3>Agent Package</h3></div><app-status-badge [label]="packageStateLabel()" [variant]="packageStateVariant()" role="status"></app-status-badge></div>
          <p>{{ packageStatus()?.detail || 'The Agent has not returned a server-owned package manifest.' }}</p>
          @if (packageStatus()?.manifest; as manifest) { <dl class="slice-b-facts"><div><dt>Installed</dt><dd>{{ manifest.version }}</dd></div><div><dt>Service</dt><dd>{{ manifest.serviceDisplayName }}</dd></div><div><dt>Trust</dt><dd>{{ packageStatus()?.verification }}</dd></div></dl> }
          <div class="action-row"><ui-button variant="secondary" size="sm" icon="bi-search" [loading]="previewingPackage()" [disabled]="!canOperate()" (pressed)="previewAgentPackage('upgrade')">Preview upgrade</ui-button><ui-button variant="primary" size="sm" icon="bi-box-seam" [loading]="startingPackage()" [disabled]="!canOperate() || !packagePreview()?.ready || !safetySnapshot()?.snapshotId" (pressed)="requestAgentPackageOperation()">Apply verified package</ui-button></div>
          @if (packagePreview(); as preview) { <p class="result-copy">{{ preview.ready ? preview.effects.join(' · ') : preview.blockers.join(', ') }}</p> }
          @if (packageOperation(); as operation) { <p class="result-copy" role="status">{{ operation.detail }} @if (operation.recoveryRequired) { <strong>Recovery required.</strong> }</p> }
          <p class="boundary-copy"><i class="bi bi-shield-lock" aria-hidden="true"></i> Installation, upgrade, uninstall, rollback, service registration, ACL, certificate, and health actions remain Agent-owned and fail closed when trust is unavailable.</p>
        </article>

        <article class="operator-card slice-b-card">
          <div class="operator-card__heading"><div><div class="section-kicker">Local repair</div><h3>Repair Installation</h3></div><app-status-badge [label]="repairStateLabel()" [variant]="repairStateVariant()" role="status"></app-status-badge></div>
          <p>Repair is never inferred from a health recommendation. It requires a fresh verified snapshot, package identity/signature/checksum, capacity, explicit preview, exact confirmation, and one-use authorization.</p>
          <div class="action-row"><ui-button variant="secondary" size="sm" icon="bi-search" [loading]="previewingRepair()" [disabled]="!canOperate()" (pressed)="previewRepairInstallation()">Preview repair</ui-button><ui-button variant="danger" size="sm" icon="bi-wrench-adjustable" [loading]="startingRepair()" [disabled]="!canOperate() || !repairPreview()?.ready" (pressed)="requestRepairInstallation()">Repair installation</ui-button></div>
          @if (repairPreview(); as preview) { <p class="result-copy">{{ preview.ready ? preview.effects.join(' · ') : preview.blockers.join(', ') }}</p> }
          @if (repairOperation(); as operation) { <p class="result-copy" role="status">{{ operation.detail }} @if (operation.rollbackAttempted) { <strong>{{ operation.rollbackSucceeded ? 'Rollback confirmed.' : 'Rollback not confirmed.' }}</strong> }</p> }
          <p class="boundary-copy"><i class="bi bi-shield-check" aria-hidden="true"></i> This local typed boundary does not invoke Main Server installation-state acknowledgement routes.</p>
        </article>

        <article class="operator-card slice-b-card">
          <div class="operator-card__heading"><div><div class="section-kicker">Coordinated checkpoints</div><h3>Guided Repair</h3></div><app-status-badge [label]="guidedRepair()?.state || 'Not started'" [variant]="guidedReadyStep() ? 'warning' : 'info'" role="status"></app-status-badge></div>
          <p>{{ guidedRepair()?.detail || 'Load the fixed checkpoint sequence to see which typed precondition is next.' }}</p>
          <div class="action-row"><ui-button variant="secondary" size="sm" icon="bi-list-check" [loading]="loadingGuidedRepair()" [disabled]="!canOperate()" (pressed)="previewGuidedRepair()">Load checkpoints</ui-button><ui-button variant="primary" size="sm" icon="bi-arrow-right-circle" [loading]="advancingGuidedRepair()" [disabled]="!canOperate() || !guidedReadyStep()" (pressed)="requestGuidedRepairAdvance()">Confirm next checkpoint</ui-button></div>
          @if (guidedRepair(); as guided) { <div class="guided-steps">@for (step of guided.steps; track step.stepId) { <div class="guided-step" [class.guided-step--ready]="step.state === 'ready'"><span>{{ step.title }}</span><app-status-badge [label]="step.state" [variant]="step.state === 'completed' ? 'success' : step.state === 'blocked' ? 'warning' : step.state === 'ready' ? 'info' : 'info'" role="status"></app-status-badge></div> }</div> }
          <p class="boundary-copy"><i class="bi bi-signpost-split" aria-hidden="true"></i> Checkpoints recommend and coordinate only fixed typed actions; a checkpoint cannot activate a package implicitly.</p>
        </article>
      </div>
    </section>

    <section class="workspace-panel advanced-panel" aria-labelledby="advanced-title">
      <details>
        <summary id="advanced-title">Advanced Diagnostics <span>Agent / API / OS, safe configuration statements, and deeper evidence</span></summary>
        <div class="advanced-grid advanced-grid--three"><div class="evidence-block"><h3>Agent / API / OS</h3><dl class="key-value-list"><div><dt>Agent</dt><dd>{{ agentVersion() }}</dd></div><div><dt>API</dt><dd>{{ apiVersion() }}</dd></div><div><dt>OS</dt><dd>{{ capabilities()?.operatingSystem || 'Unavailable' }}</dd></div><div><dt>Authorization</dt><dd>{{ authorizationLabel() }}</dd></div></dl></div><div class="evidence-block"><h3>Safe configuration statements</h3><p>{{ configurationSummary() }}</p><p>Secret values, presence flags, and source paths are intentionally not displayed in this workspace.</p></div><div class="evidence-block"><h3>Legacy SQL evidence</h3><p>{{ connectivity()?.localSql?.detail || 'Local SQL evidence is unavailable.' }}</p><p>Reachability is not database health; database identity and backup evidence are shown above.</p></div></div>
        <div class="advanced-grid advanced-grid--three"><div class="evidence-block"><h3>Frontend build identity</h3><p data-testid="frontend-build-identity">{{ frontendBuildSummary() }}</p><p>Commit, build hash, asset count, and build time prove this origin is serving the current build. Filesystem paths are intentionally not displayed.</p></div><div class="evidence-block"><h3>Testing infrastructure</h3><p>{{ testingInfrastructureSummary() }}</p></div><div class="evidence-block"><h3>Incident Timeline</h3>@if (timeline(); as incidents) { <div class="timeline-list">@for (event of incidents.events; track event.eventId) { <div><time>{{ event.atUtc }}</time><strong>{{ event.kind }}</strong><span>{{ event.summary }}</span></div> } @empty { <span class="empty-copy">No retained events for this principal.</span> }</div> } @else { <p>Use View Incident Timeline to load the bounded local timeline.</p> }</div></div>
      </details>
    </section>

    @if (pendingAction(); as pending) { <app-confirm-dialog variant="danger" [title]="'Confirm ' + actionLabel(pending.action).toLowerCase() + ' service'" [message]="confirmationMessage(pending)" confirmLabel="Continue" cancelLabel="Cancel" (cancel)="cancelPendingAction()" (confirm)="executePendingAction()"></app-confirm-dialog> }
    @if (pendingDatabaseAction(); as pending) { <app-confirm-dialog variant="danger" [title]="'Restore ' + pending.displayName.toLowerCase() + '?'" [message]="databaseConfirmationMessage(pending)" [requireReason]="true" [requiredTypedValue]="pending.confirmationText" reasonLabel="Type the exact confirmation phrase" [reasonPlaceholder]="pending.confirmationText" confirmLabel="Restore database" cancelLabel="Cancel" (cancel)="cancelPendingDatabaseAction()" (confirm)="executePendingDatabaseRestore($event)"></app-confirm-dialog> }
    @if (pendingMaintenanceAction(); as pending) { <app-confirm-dialog variant="danger" [title]="pending.mode === 'cleanup' ? 'Confirm cleanup' : 'Confirm branch reset'" [message]="pending.mode === 'cleanup' ? 'The Agent will re-check its configured cleanup policy and may stop approved services before deleting approved targets.' : 'The Agent will re-check the configured branch and approved table scope before resetting data.'" [requireReason]="true" [requiredTypedValue]="pending.confirmationText" reasonLabel="Type the exact Agent confirmation phrase" [reasonPlaceholder]="pending.confirmationText" confirmLabel="Execute" cancelLabel="Cancel" (cancel)="cancelPendingMaintenance()" (confirm)="executePendingMaintenance($event)"></app-confirm-dialog> }
    @if (pendingSliceBAction(); as pending) { <app-confirm-dialog variant="danger" [title]="pending.kind === 'snapshot' ? 'Capture Safety Snapshot' : pending.kind === 'console' ? 'Run fixed diagnostics' : pending.kind === 'package' ? 'Apply verified Agent package' : pending.kind === 'repair' ? 'Repair installation' : 'Confirm Guided Repair checkpoint'" [message]="pending.message" [requireReason]="true" [requiredTypedValue]="pending.confirmationText" reasonLabel="Type the exact Agent confirmation phrase" [reasonPlaceholder]="pending.confirmationText" confirmLabel="Continue" cancelLabel="Cancel" (cancel)="cancelPendingSliceBAction()" (confirm)="executePendingSliceBAction($event)"></app-confirm-dialog> }
  </main>
`;

@Component({
  selector: 'app-pos-maintenance',
  standalone: true,
  imports: [
    CommonModule,
    NavbarComponent,
    PageHeaderComponent,
    StatusBadgeComponent,
    UiButtonComponent,
    ConfirmDialogComponent
  ],
  template: POS_MAINTENANCE_TEMPLATE, /* legacy template retained below while the Slice A workspace is verified
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
              <div><dt>Database configuration</dt><dd>Detected</dd></div>
              <div><dt>Credentials</dt><dd>Available internally</dd></div>
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
  */
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
    @media (max-width: 1000px) { .status-grid, .workspace-grid, .operator-grid, .slice-b-grid, .slice-b-grid--wide { grid-template-columns: 1fr; } }
    @media (max-width: 680px) { .pos-page { padding-inline: var(--space-4); } .evidence-grid { grid-template-columns: 1fr; } .service-row { grid-template-columns: 1fr; align-items: flex-start; } .service-row__actions { justify-content: flex-start; } }
  `]
})
export class PosMaintenanceComponent implements OnDestroy {
  private readonly transport = inject(PosAgentTransportService);
  private readonly toast = inject(ToastService);
  private readonly buildIdentityService = inject(BuildIdentityService);

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
  readonly operationalHealth = signal<RmsOperationalHealth | null>(null);
  readonly health = signal<HealthReport | null>(null);
  readonly failureAnalysis = signal<ServiceFailureAnalysis | null>(null);
  readonly timeline = signal<IncidentTimeline | null>(null);
  readonly supportBundle = signal<SupportBundle | null>(null);
  readonly diagnosingService = signal<string | null>(null);
  readonly loadingHealth = signal(false);
  readonly generatingBundle = signal(false);
  readonly expandedBackups = signal<Record<RmsDatabaseTarget, boolean>>({ branch: false, cashier: false });
  readonly databaseTargets: RmsDatabaseTarget[] = ['branch', 'cashier'];
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
  readonly mainServerProfiles = signal<MainServerProfiles | null>(null);
  readonly mainServerState = signal<MainServerState | null>(null);
  readonly safetySnapshotPreview = signal<SafetySnapshotPreview | null>(null);
  readonly safetySnapshot = signal<SafetySnapshot | null>(null);
  readonly capturingSafetySnapshot = signal(false);
  readonly consoleTarget = signal<DiagnosticConsoleTarget>('branchServerApi');
  readonly consolePreview = signal<DiagnosticConsolePreview | null>(null);
  readonly consoleRun = signal<DiagnosticConsoleRun | null>(null);
  readonly previewingConsole = signal(false);
  readonly startingConsole = signal(false);
  readonly packageStatus = signal<AgentPackageStatus | null>(null);
  readonly packagePreview = signal<AgentPackagePreview | null>(null);
  readonly packageOperation = signal<AgentPackageOperation | null>(null);
  readonly previewingPackage = signal(false);
  readonly startingPackage = signal(false);
  readonly repairPreview = signal<RepairPreview | null>(null);
  readonly repairOperation = signal<RepairOperation | null>(null);
  readonly previewingRepair = signal(false);
  readonly startingRepair = signal(false);
  readonly guidedRepair = signal<GuidedRepair | null>(null);
  readonly loadingGuidedRepair = signal(false);
  readonly advancingGuidedRepair = signal(false);
  readonly pendingSliceBAction = signal<PendingSliceBAction | null>(null);

  private readonly errors = signal<Record<string, string>>({});
  private readonly actionOutcomes = signal<Record<string, ServiceActionResponse>>({});
  private downloaderSelectionInitialized = false;
  private readonly delayHandles = new Set<ReturnType<typeof globalThis.setTimeout>>();
  private destroyed = false;

  constructor() {
    void this.buildIdentityService.load();
    void this.load();
  }

  /**
   * Non-secret identity of the frontend bundle this browser actually loaded.
   * It is what distinguishes a current secure origin from one still serving a
   * previously staged build; it never exposes a filesystem path.
   */
  frontendBuildSummary(): string {
    return this.buildIdentityService.summary();
  }

  async refresh(): Promise<void> {
    if (this.refreshing()) return;
    await this.load();
  }

  operatorHeaderLine(): string {
    const installation = this.rmsDiagnostics()?.installation;
    const branch = installation?.branchCode || this.identity()?.branchCode || 'Branch unavailable';
    const pos = installation?.posNumber || this.identity()?.posNumber || 'POS unavailable';
    return `${branch} · ${pos} · ${this.productRelease()} · ${installation?.clientName || this.identityClientName()}`;
  }

  identityClientName(): string {
    return this.identity()?.clientName || 'Client unavailable';
  }

  productRelease(): string {
    const installation = this.rmsDiagnostics()?.installation;
    const identity = this.identity() as (DeviceIdentity & { release?: string }) | null;
    return installation?.productRelease || identity?.productRelease || identity?.release || 'Unavailable';
  }

  databaseWorkspace(target: RmsDatabaseTarget): RmsDatabaseWorkspace | null {
    return target === 'branch' ? this.branchDatabaseWorkspace() : this.cashierDatabaseWorkspace();
  }

  databaseDiagnostic(target: RmsDatabaseTarget): RmsDatabaseDiagnostic | null {
    const rms = this.rmsDiagnostics();
    return target === 'branch' ? rms?.branchDatabase || null : rms?.cashierDatabase || null;
  }

  databaseHealth(target: RmsDatabaseTarget): RmsDatabaseHealth | null {
    return this.databaseDiagnostic(target)?.health || null;
  }

  databaseTargetLabel(target: RmsDatabaseTarget): string {
    return target === 'branch' ? 'RmsBranchSrv' : 'RmsCashierSrv';
  }

  latestBackupLabel(target: RmsDatabaseTarget): string {
    const latest = this.databaseHealth(target)?.backups?.latestCreatedAtUtc;
    if (latest) return this.formatDate(latest);
    const artifact = this.databaseWorkspace(target)?.approvedBackups?.[0];
    return artifact ? this.formatArtifactDate(artifact) : 'None approved';
  }

  databaseHealthFreshnessLabel(target: RmsDatabaseTarget): string {
    const freshness = this.databaseHealth(target)?.backups?.freshness;
    return freshness ? this.freshnessLabel({ freshness, lastCheckedUtc: null, detail: '' } as Evidence) : 'Unknown';
  }

  canRestore(target: RmsDatabaseTarget): boolean {
    return this.canControlDatabases() && (this.databaseWorkspace(target)?.approvedBackups?.length || 0) > 0;
  }

  restoreDisabledReason(target: RmsDatabaseTarget): string {
    if (!this.canControlDatabases()) return 'Restore requires local Administrator authorization.';
    return (this.databaseWorkspace(target)?.approvedBackups?.length || 0) > 0
      ? 'Restore the newest approved backup.'
      : 'Restore is disabled until an approved backup is available.';
  }

  restoreLatestBackup(target: RmsDatabaseTarget): void {
    const artifact = this.databaseWorkspace(target)?.approvedBackups?.[0];
    if (artifact) this.requestDatabaseRestore(target, artifact);
  }

  backupListOpen(target: RmsDatabaseTarget): boolean {
    return this.expandedBackups()[target] === true;
  }

  toggleBackupList(target: RmsDatabaseTarget): void {
    this.expandedBackups.update(current => ({ ...current, [target]: !current[target] }));
  }

  formatBytes(value: number): string {
    if (!Number.isFinite(value) || value < 0) return 'Unavailable';
    if (value < 1024) return `${value} B`;
    if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
    if (value < 1024 * 1024 * 1024) return `${(value / (1024 * 1024)).toFixed(1)} MB`;
    return `${(value / (1024 * 1024 * 1024)).toFixed(1)} GB`;
  }

  async runHealthCheck(): Promise<void> {
    if (this.loadingHealth()) return;
    this.loadingHealth.set(true);
    const result = await this.settle(this.transport.getHealthCheck());
    if (result.ok) {
      this.health.set(result.value);
      this.toast.showSuccess('Health Check completed.');
    } else {
      this.errors.update(errors => ({ ...errors, health: this.userFacingError(result.error) }));
      this.toast.showError('Health Check could not be completed.');
    }
    this.loadingHealth.set(false);
  }

  async diagnoseServiceFailure(serviceId: string): Promise<void> {
    if (this.diagnosingService()) return;
    this.diagnosingService.set(serviceId);
    const result = await this.settle(this.transport.getServiceFailureAnalysis(serviceId));
    if (result.ok) {
      this.failureAnalysis.set(result.value);
      this.toast.showInfo('Bounded service evidence loaded.');
    } else {
      this.errors.update(errors => ({ ...errors, failureAnalysis: this.userFacingError(result.error) }));
      this.toast.showError('Service failure analysis could not be completed.');
    }
    this.diagnosingService.set(null);
  }

  async viewIncidentTimeline(): Promise<void> {
    const result = await this.settle(this.transport.getIncidentTimeline());
    if (result.ok) this.timeline.set(result.value);
    else this.toast.showError('Incident Timeline could not be loaded.');
  }

  async generateSupportBundle(): Promise<void> {
    if (this.generatingBundle() || !this.canOperate()) return;
    this.generatingBundle.set(true);
    try {
      const tokenResult = await firstValueFrom(this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.supportBundleGenerate));
      const bundle = await firstValueFrom(this.transport.generateSupportBundle(tokenResult.token));
      this.supportBundle.set(bundle);
      this.toast.showSuccess('Support Bundle generated from redacted evidence.');
    } catch (error) {
      this.toast.showError(this.userFacingError(classifyPosAgentError(error)));
    } finally {
      this.generatingBundle.set(false);
    }
  }

  async previewSafetySnapshot(): Promise<void> {
    if (!this.canOperate() || this.capturingSafetySnapshot()) return;
    const result = await this.settle(this.transport.getSafetySnapshotPreview());
    if (result.ok) {
      this.safetySnapshotPreview.set(result.value);
      this.toast.showInfo(result.value.ready ? 'Safety Snapshot is ready for typed capture.' : 'Safety Snapshot remains blocked by current evidence.');
    } else {
      this.errors.update(errors => ({ ...errors, safetySnapshot: this.userFacingError(result.error) }));
      this.toast.showError('Safety Snapshot preview could not be loaded.');
    }
  }

  requestSafetySnapshotCapture(): void {
    const preview = this.safetySnapshotPreview();
    if (!this.canOperate() || !preview?.ready) return;
    this.pendingSliceBAction.set({
      kind: 'snapshot',
      confirmationText: 'CAPTURE SAFETY SNAPSHOT',
      message: 'The Agent will capture only the bounded safe identity, health, capacity, backup metadata, and drift evidence listed in the preview. No installer or Main Server mutation is involved.'
    });
  }

  async previewDiagnosticConsole(): Promise<void> {
    if (!this.canOperate() || this.previewingConsole()) return;
    this.previewingConsole.set(true);
    try {
      const preview = await firstValueFrom(this.transport.getDiagnosticConsolePreview(this.consoleTarget()));
      this.consolePreview.set(preview);
      this.toast.showInfo(preview.ready ? 'The fixed diagnostic console preview is ready.' : 'The diagnostic console remains unavailable; no process can be started.');
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.previewingConsole.set(false);
    }
  }

  requestDiagnosticConsoleRun(): void {
    const preview = this.consolePreview();
    if (!this.canOperate() || !preview?.ready || !preview.previewId) return;
    this.pendingSliceBAction.set({
      kind: 'console',
      previewId: preview.previewId,
      confirmationText: preview.confirmationPhrase,
      message: 'Only the Agent manifest target, fixed diagnostic arguments, fixed working directory, empty child environment, and bounded redacted output are permitted.'
    });
  }

  async previewAgentPackage(operation: components['schemas']['AgentPackageOperationKindDto'] = 'upgrade'): Promise<void> {
    if (!this.canOperate() || this.previewingPackage()) return;
    this.previewingPackage.set(true);
    try {
      const preview = await firstValueFrom(this.transport.getAgentPackagePreview(operation));
      this.packagePreview.set(preview);
      this.toast.showInfo(preview.ready ? 'The server-owned package preview is ready.' : 'No verified server-owned package operation is currently available.');
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.previewingPackage.set(false);
    }
  }

  requestAgentPackageOperation(): void {
    const preview = this.packagePreview();
    if (!this.canOperate() || !preview?.ready || !preview.previewId) return;
    this.pendingSliceBAction.set({
      kind: 'package',
      previewId: preview.previewId,
      snapshotId: this.safetySnapshot()?.snapshotId || undefined,
      confirmationText: preview.confirmationPhrase,
      message: 'Package changes require a fresh verified Safety Snapshot. The Agent owns the exact package, service identity, ACL, certificate, activation, health, and rollback boundary.'
    });
  }

  async previewRepairInstallation(): Promise<void> {
    if (!this.canOperate() || this.previewingRepair()) return;
    this.previewingRepair.set(true);
    try {
      const snapshotId = this.safetySnapshot()?.snapshotId;
      const preview = await firstValueFrom(this.transport.getRepairPreview('repair', snapshotId || undefined));
      this.repairPreview.set(preview);
      this.toast.showInfo(preview.ready ? 'Repair Installation preview is ready.' : 'Repair Installation is blocked until all preconditions are verified.');
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.previewingRepair.set(false);
    }
  }

  requestRepairInstallation(): void {
    const preview = this.repairPreview();
    if (!this.canOperate() || !preview?.ready || !preview.previewId) return;
    this.pendingSliceBAction.set({
      kind: 'repair',
      previewId: preview.previewId,
      snapshotId: preview.snapshot.snapshotId || undefined,
      confirmationText: preview.confirmationPhrase,
      message: 'Repair is local and Agent-owned. Main Server installation-state acknowledgements are not used as an installer. Activation will be reported only when typed health and rollback outcomes are confirmed.'
    });
  }

  async previewGuidedRepair(): Promise<void> {
    if (!this.canOperate() || this.loadingGuidedRepair()) return;
    this.loadingGuidedRepair.set(true);
    try {
      const guided = await firstValueFrom(this.transport.getGuidedRepairPreview(this.safetySnapshot()?.snapshotId || undefined));
      this.guidedRepair.set(guided);
      this.toast.showInfo('Guided Repair checkpoints loaded; no repair action was started.');
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.loadingGuidedRepair.set(false);
    }
  }

  requestGuidedRepairAdvance(): void {
    const guided = this.guidedRepair();
    const step = guided?.steps.find(candidate => candidate.state === 'ready');
    if (!this.canOperate() || !guided?.guidedRepairId || !step?.requiresConfirmation || !step.confirmationPhrase) return;
    this.pendingSliceBAction.set({
      kind: 'guided',
      guidedRepairId: guided.guidedRepairId,
      stepId: step.stepId,
      confirmationText: step.confirmationPhrase,
      message: 'Guided Repair advances exactly one server-owned checkpoint. Package staging and activation are never inferred from a recommendation or a checkpoint click.'
    });
  }

  cancelPendingSliceBAction(): void {
    this.pendingSliceBAction.set(null);
  }

  async executePendingSliceBAction(confirmationText: string): Promise<void> {
    const pending = this.pendingSliceBAction();
    if (!pending || !this.canOperate()) {
      this.pendingSliceBAction.set(null);
      return;
    }
    if (confirmationText !== pending.confirmationText) {
      this.toast.showError('Type the exact Agent confirmation phrase before continuing.');
      return;
    }

    this.pendingSliceBAction.set(null);
    try {
      if (pending.kind === 'snapshot') {
        this.capturingSafetySnapshot.set(true);
        const issued = await firstValueFrom(this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.safetySnapshotCapture));
        const snapshot = await firstValueFrom(this.transport.captureSafetySnapshot({ typedConfirmation: confirmationText, idempotencyKey: this.createIdempotencyKey() }, issued.token));
        this.safetySnapshot.set(snapshot);
        this.toast.showSuccess('Safety Snapshot captured and integrity protected by the Agent.');
      } else if (pending.kind === 'console') {
        this.startingConsole.set(true);
        const issued = await firstValueFrom(this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.diagnosticConsoleRun));
        const accepted = await firstValueFrom(this.transport.startDiagnosticConsoleRun({ previewId: pending.previewId!, typedConfirmation: confirmationText, idempotencyKey: this.createIdempotencyKey() }, issued.token));
        this.consoleRun.set(await this.followDiagnosticConsoleRun(accepted));
      } else if (pending.kind === 'package') {
        this.startingPackage.set(true);
        const issued = await firstValueFrom(this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.agentPackageOperation));
        const accepted = await firstValueFrom(this.transport.startAgentPackageOperation({ previewId: pending.previewId!, typedConfirmation: confirmationText, idempotencyKey: this.createIdempotencyKey(), snapshotId: pending.snapshotId || null }, issued.token));
        this.packageOperation.set(await this.followAgentPackageOperation(accepted));
      } else if (pending.kind === 'repair') {
        this.startingRepair.set(true);
        const issued = await firstValueFrom(this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.repairOperation));
        const accepted = await firstValueFrom(this.transport.startRepairOperation({ previewId: pending.previewId!, typedConfirmation: confirmationText, idempotencyKey: this.createIdempotencyKey(), snapshotId: pending.snapshotId || null }, issued.token));
        this.repairOperation.set(await this.followRepairOperation(accepted));
      } else {
        this.advancingGuidedRepair.set(true);
        const issued = await firstValueFrom(this.transport.issueMutationToken(POS_AGENT_OPERATION_IDS.guidedRepairCheckpoint));
        const guided = await firstValueFrom(this.transport.advanceGuidedRepair({ guidedRepairId: pending.guidedRepairId!, stepId: pending.stepId!, typedConfirmation: confirmationText, idempotencyKey: this.createIdempotencyKey() }, issued.token));
        this.guidedRepair.set(guided);
        this.toast.showInfo(guided.detail);
      }
    } catch (error) {
      this.toast.showError(this.operatorErrorMessage(classifyPosAgentError(error)));
    } finally {
      this.capturingSafetySnapshot.set(false);
      this.startingConsole.set(false);
      this.startingPackage.set(false);
      this.startingRepair.set(false);
      this.advancingGuidedRepair.set(false);
    }
  }

  mainServerBindingLabel(binding: MainServerProfiles['activeBinding'] | undefined): string {
    switch (binding) {
      case 'bound': return 'Bound';
      case 'mismatch': return 'Mismatch';
      case 'ambiguous': return 'Ambiguous';
      case 'unavailable': return 'Unavailable';
      default: return 'Unknown';
    }
  }

  mainServerBindingVariant(binding: MainServerProfiles['activeBinding'] | undefined): 'success' | 'warning' | 'danger' | 'info' {
    return binding === 'bound' ? 'success' : binding === 'mismatch' || binding === 'ambiguous' ? 'danger' : binding === 'unavailable' ? 'warning' : 'info';
  }

  mainServerDetail(): string {
    return this.mainServerState()?.detail || this.mainServerProfiles()?.detail || this.readError('mainServer');
  }

  snapshotStateLabel(snapshot: SafetySnapshot | null): string {
    if (!snapshot) return this.safetySnapshotPreview()?.ready ? 'Ready to capture' : 'Not captured';
    return snapshot.state === 'captured' || snapshot.state === 'verified' ? 'Verified evidence' : snapshot.state.replace(/([A-Z])/g, ' $1');
  }

  snapshotVariant(snapshot: SafetySnapshot | null): 'success' | 'warning' | 'danger' | 'info' {
    if (snapshot?.state === 'captured' || snapshot?.state === 'verified') return 'success';
    return this.safetySnapshotPreview()?.ready ? 'warning' : 'info';
  }

  consoleTargetLabel(target: DiagnosticConsoleTarget): string {
    return target === 'branchServerApi' ? 'Branch Server API' : target === 'cashierServerApi' ? 'Cashier Server API' : target === 'serviceManager' ? 'RMS Services Manager' : 'Cashier UI';
  }

  setConsoleTarget(target: string): void {
    if (target === 'branchServerApi' || target === 'cashierServerApi' || target === 'serviceManager' || target === 'cashierUi') {
      this.consoleTarget.set(target);
      this.consolePreview.set(null);
    }
  }

  consoleStateLabel(state: DiagnosticConsoleRun['outcome'] | undefined): string {
    return state ? state.replace(/([A-Z])/g, ' $1') : 'Not attempted';
  }

  consoleStateVariant(state: DiagnosticConsoleRun['outcome'] | undefined): 'success' | 'warning' | 'danger' | 'info' {
    return state === 'succeeded' ? 'success' : state === 'partial' || state === 'timedOut' ? 'warning' : state === 'failed' || state === 'outcomeUnknown' ? 'danger' : 'info';
  }

  packageStateLabel(): string {
    return this.packageStatus()?.verification === 'verified' ? 'Verified' : 'Unavailable / unverified';
  }

  packageStateVariant(): 'success' | 'warning' | 'danger' | 'info' {
    return this.packageStatus()?.verification === 'verified' ? 'success' : this.packageStatus() ? 'warning' : 'info';
  }

  repairStateLabel(): string {
    const preview = this.repairPreview();
    return preview?.ready ? 'Ready after verified preconditions' : 'Blocked until preconditions pass';
  }

  repairStateVariant(): 'success' | 'warning' | 'danger' | 'info' {
    return this.repairPreview()?.ready ? 'success' : this.repairPreview() ? 'warning' : 'info';
  }

  guidedReadyStep(): GuidedRepair['steps'][number] | null {
    return this.guidedRepair()?.steps.find(step => step.state === 'ready') || null;
  }

  healthCheck(code: string): HealthCheck | null {
    return this.health()?.checks.find(check => check.code === code) || null;
  }

  peerHealthLabel(code: string): string {
    return this.healthStateLabel(this.healthCheck(code)?.state);
  }

  peerHealthVariant(code: string): 'success' | 'warning' | 'danger' | 'info' {
    return this.healthStateVariant(this.healthCheck(code)?.state);
  }

  peerHealthDetail(code: string): string {
    return this.healthCheck(code)?.summary || 'Health evidence is not available yet.';
  }

  healthStateLabel(state: HealthState | null | undefined): string {
    switch (state) {
      case 'healthy': return 'Healthy';
      case 'warning': return 'Warning';
      case 'actionRequired': return 'Action required';
      default: return 'Unknown';
    }
  }

  healthStateVariant(state: HealthState | null | undefined): 'success' | 'warning' | 'danger' | 'info' {
    switch (state) {
      case 'healthy': return 'success';
      case 'warning': return 'warning';
      case 'actionRequired': return 'danger';
      default: return 'info';
    }
  }

  operationalHealthLabel(): string {
    const roots = this.operationalHealth()?.fixedRoots || [];
    if (!roots.length) return 'Awaiting signal';
    return roots.some(root => root.state === 'missing' || root.state === 'inaccessible')
      ? 'Action required'
      : roots.some(root => root.state === 'stale' || root.state === 'unknown')
        ? 'Review evidence'
        : 'Within bounds';
  }

  operationalHealthVariant(): 'success' | 'warning' | 'danger' | 'info' {
    const roots = this.operationalHealth()?.fixedRoots || [];
    if (!roots.length) return 'info';
    return roots.some(root => root.state === 'missing' || root.state === 'inaccessible')
      ? 'danger'
      : roots.some(root => root.state === 'stale' || root.state === 'unknown')
        ? 'warning'
        : 'success';
  }

  operationalRootSummary(): string {
    const roots = this.operationalHealth()?.fixedRoots || [];
    if (!roots.length) return '— / —';
    const healthy = roots.filter(root => root.state === 'healthy').length;
    return `${healthy} / ${roots.length}`;
  }

  operationalAttachmentSummary(): string {
    const attachment = this.operationalHealth()?.insuranceAttachments;
    return attachment ? `${this.numberValue(attachment.attachmentCount).toLocaleString()} files` : 'Awaiting signal';
  }

  rootStateLabel(state: RmsFixedRootState): string {
    switch (state) {
      case 'healthy': return 'Healthy';
      case 'missing': return 'Missing';
      case 'inaccessible': return 'Inaccessible';
      case 'stale': return 'Stale';
      default: return 'Unknown';
    }
  }

  rootStateVariant(state: RmsFixedRootState): 'success' | 'warning' | 'danger' | 'info' {
    switch (state) {
      case 'healthy': return 'success';
      case 'missing': return 'danger';
      case 'inaccessible':
      case 'stale': return 'warning';
      default: return 'info';
    }
  }

  updateStateVariant(state: string): 'success' | 'warning' | 'danger' | 'info' {
    const normalized = state.toLowerCase();
    return normalized.includes('available') || normalized.includes('installed') ? 'success'
      : normalized.includes('unavailable') || normalized.includes('error') ? 'danger'
        : 'warning';
  }

  formatSignalTime(value: string | null | undefined): string {
    return value ? this.formatDate(value) : 'Unavailable';
  }

  consistencyHeaderLabel(): string {
    const warnings = this.rmsDiagnostics()?.installation.consistency.warnings || [];
    return warnings.length ? 'Action required' : this.rmsDiagnostics() ? 'Consistent' : 'Unknown';
  }

  consistencyHeaderVariant(): 'success' | 'warning' | 'danger' | 'info' {
    return this.rmsDiagnostics()?.installation.consistency.warnings?.length ? 'danger' : this.rmsDiagnostics() ? 'success' : 'info';
  }

  consistencyHeaderDetail(): string {
    const warnings = this.rmsDiagnostics()?.installation.consistency.warnings || [];
    return warnings[0] || 'Known duplicated values are aligned or no warning was returned.';
  }

  endpointText(endpoint: RmsEndpointDiagnostic | undefined): string {
    if (!endpoint) return 'Unavailable';
    return `${endpoint.configured ? 'Configured' : 'Not configured'} · ${endpoint.reachability.detail}`;
  }

  driftLabel(state: RmsComponentDriftState): string {
    return state === 'aligned' ? 'Aligned' : state === 'drifted' ? 'Drifted' : 'Unavailable';
  }

  driftVariant(state: RmsComponentDriftState): 'success' | 'warning' | 'danger' | 'info' {
    return state === 'aligned' ? 'success' : state === 'drifted' ? 'danger' : 'info';
  }

  failureCategoryLabel(category: components['schemas']['FailureCategory']): string {
    return category === 'none' ? 'No failure found' : category.replace(/([A-Z])/g, ' $1').replace(/^./, value => value.toUpperCase());
  }

  failureSeverityVariant(severity: components['schemas']['FailureSeverity']): 'success' | 'warning' | 'danger' | 'info' {
    return severity === 'actionRequired' ? 'danger' : severity === 'warning' ? 'warning' : severity === 'informational' ? 'success' : 'info';
  }

  configurationSummary(): string {
    return this.configuration() ? 'The Agent returned a server-owned redacted configuration projection.' : 'Redacted configuration evidence is unavailable.';
  }

  testingInfrastructureSummary(): string {
    return 'Testing-only infrastructure and test service details remain outside the primary RMS service table.';
  }

  private formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? 'Unavailable' : date.toLocaleString();
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

  private async followDiagnosticConsoleRun(accepted: DiagnosticConsoleRun): Promise<DiagnosticConsoleRun> {
    let current = accepted;
    for (let attempt = 0; attempt < 80; attempt++) {
      if (!['accepted', 'running'].includes(current.state)) return current;
      await this.delay(250);
      current = await firstValueFrom(this.transport.getDiagnosticConsoleRun(current.operationId));
    }
    return current;
  }

  private async followAgentPackageOperation(accepted: AgentPackageOperation): Promise<AgentPackageOperation> {
    let current = accepted;
    for (let attempt = 0; attempt < 80; attempt++) {
      if (!['accepted', 'staging', 'activating', 'verifying'].includes(current.state)) return current;
      await this.delay(250);
      current = await firstValueFrom(this.transport.getAgentPackageOperation(current.operationId));
    }
    return current;
  }

  private async followRepairOperation(accepted: RepairOperation): Promise<RepairOperation> {
    let current = accepted;
    for (let attempt = 0; attempt < 80; attempt++) {
      if (!['accepted', 'running'].includes(current.state)) return current;
      await this.delay(250);
      current = await firstValueFrom(this.transport.getRepairOperation(current.operationId));
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
    return new Promise(resolve => {
      const handle = globalThis.setTimeout(() => {
        this.delayHandles.delete(handle);
        resolve();
      }, milliseconds);
      this.delayHandles.add(handle);
    });
  }

  ngOnDestroy() {
    this.destroyed = true;
    for (const handle of this.delayHandles) globalThis.clearTimeout(handle);
    this.delayHandles.clear();
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

  databaseStatusLabel(database: RmsDatabaseDiagnostic | null): string {
    if (!database) return 'Unavailable';
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

  databaseStatusVariant(database: RmsDatabaseDiagnostic | null): 'success' | 'warning' | 'danger' | 'info' {
    if (!database) return 'info';
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

  readError(area: string): string {
    return this.errors()[area] || 'The Agent did not return this read model.';
  }

  private async load(): Promise<void> {
    const firstLoad = this.loading();
    this.refreshing.set(true);
    if (firstLoad) this.loading.set(true);

    const [live, session, identity, connectivity, capabilities, configuration, services, rms, operationalHealth, health, branchDatabase, cashierDatabase, downloaderBranches, mainServerProfiles, mainServerState, safetySnapshotPreview, packageStatus, guidedRepair] = await Promise.all([
      this.settle(this.transport.getLive()),
      this.settle(this.transport.getSession()),
      this.settle(this.transport.getDeviceIdentity()),
      this.settle(this.transport.getDeviceConnectivity()),
      this.settle(this.transport.getDeviceCapabilities()),
      this.settle(this.transport.getConfiguration()),
      this.settle(this.transport.getServices()),
      this.settle(this.transport.getRmsDiagnostics()),
      this.settle(this.transport.getRmsOperationalHealth()),
      this.settle(this.transport.getHealthCheck()),
      this.settle(this.transport.getRmsDatabaseWorkspace('branch')),
      this.settle(this.transport.getRmsDatabaseWorkspace('cashier')),
      this.settle(this.transport.getDownloaderBranches()),
      this.settle(this.transport.getMainServerProfiles()),
      this.settle(this.transport.getMainServerState()),
      this.settle(this.transport.getSafetySnapshotPreview()),
      this.settle(this.transport.getAgentPackageStatus()),
      this.settle(this.transport.getGuidedRepairPreview())
    ]);

    if (this.destroyed) return;

    const nextErrors: Record<string, string> = {};
    this.applyLive(live, nextErrors);
    this.applySession(session, nextErrors);
    this.applyValue('identity', identity, this.identity, nextErrors);
    this.applyValue('connectivity', connectivity, this.connectivity, nextErrors);
    this.applyValue('capabilities', capabilities, this.capabilities, nextErrors);
    this.applyValue('configuration', configuration, this.configuration, nextErrors);
    this.applyValue('services', services, this.services, nextErrors);
    this.applyValue('rms', rms, this.rmsDiagnostics, nextErrors);
    this.applyValue('operationalHealth', operationalHealth, this.operationalHealth, nextErrors);
    this.applyValue('health', health, this.health, nextErrors);
    this.applyValue('branchDatabase', branchDatabase, this.branchDatabaseWorkspace, nextErrors);
    this.applyValue('cashierDatabase', cashierDatabase, this.cashierDatabaseWorkspace, nextErrors);
    this.applyDownloaderBranches(downloaderBranches, nextErrors);
    this.applyValue('mainServer', mainServerProfiles, this.mainServerProfiles, nextErrors);
    this.applyValue('mainServerState', mainServerState, this.mainServerState, nextErrors);
    this.applyValue('safetySnapshot', safetySnapshotPreview, this.safetySnapshotPreview, nextErrors);
    this.applyValue('packageStatus', packageStatus, this.packageStatus, nextErrors);
    this.applyValue('guidedRepair', guidedRepair, this.guidedRepair, nextErrors);
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
