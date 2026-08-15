import { HttpBackend, HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, Observable, throwError } from 'rxjs';
import { components } from './generated/pos-agent-api.generated';
import { classifyPosAgentError } from './pos-agent-error';
import {
  POS_AGENT_MUTATION_TOKEN_HEADER,
  POS_AGENT_ORIGIN,
  POS_AGENT_PATHS
} from './pos-agent.constants';

type HealthStatus = components['schemas']['HealthStatusDto'];
type HealthReport = components['schemas']['HealthReportDto'];
type MainServerProfiles = components['schemas']['MainServerProfilesDto'];
type MainServerState = components['schemas']['MainServerStateEvidenceDto'];
type SafetySnapshotPreview = components['schemas']['SafetySnapshotPreviewDto'];
type SafetySnapshotCaptureRequest = components['schemas']['SafetySnapshotCaptureRequestDto'];
type SafetySnapshot = components['schemas']['SafetySnapshotDto'];
type SafetySnapshotVerification = components['schemas']['SafetySnapshotVerificationDto'];
type DiagnosticConsoleTarget = components['schemas']['DiagnosticConsoleTargetDto'];
type DiagnosticConsolePreview = components['schemas']['DiagnosticConsolePreviewDto'];
type DiagnosticConsolePreviewRequest = components['schemas']['DiagnosticConsolePreviewRequestDto'];
type DiagnosticConsoleStartRequest = components['schemas']['DiagnosticConsoleStartRequestDto'];
type DiagnosticConsoleRun = components['schemas']['DiagnosticConsoleRunDto'];
type AgentPackageOperationKind = components['schemas']['AgentPackageOperationKindDto'];
type AgentPackageStatus = components['schemas']['AgentPackageStatusDto'];
type AgentPackagePreview = components['schemas']['AgentPackagePreviewDto'];
type AgentPackagePreviewRequest = components['schemas']['AgentPackagePreviewRequestDto'];
type AgentPackageOperationRequest = components['schemas']['AgentPackageOperationRequestDto'];
type AgentPackageOperation = components['schemas']['AgentPackageOperationDto'];
type RepairOperationKind = components['schemas']['RepairOperationKindDto'];
type RepairPreview = components['schemas']['RepairPreviewDto'];
type RepairPreviewRequest = components['schemas']['RepairPreviewRequestDto'];
type RepairExecuteRequest = components['schemas']['RepairExecuteRequestDto'];
type RepairOperation = components['schemas']['RepairOperationDto'];
type GuidedRepair = components['schemas']['GuidedRepairDto'];
type GuidedRepairPreviewRequest = components['schemas']['GuidedRepairPreviewRequestDto'];
type GuidedRepairStepRequest = components['schemas']['GuidedRepairStepRequestDto'];
type SessionInfo = components['schemas']['SessionInfoDto'];
type MutationTokenIssueRequest = components['schemas']['MutationTokenIssueRequestDto'];
type MutationTokenIssueResponse = components['schemas']['MutationTokenIssueResponseDto'];
type DeviceIdentity = components['schemas']['DeviceIdentityDto'];
type DeviceConnectivity = components['schemas']['DeviceConnectivityDto'];
type DeviceCapabilities = components['schemas']['DeviceCapabilitiesDto'];
type RedactedConfiguration = components['schemas']['RedactedConfigurationDto'];
type ServiceSummary = components['schemas']['ServiceSummaryDto'];
type ServiceActionKind = components['schemas']['ServiceActionKind'];
type ServiceActionRequest = components['schemas']['ServiceActionRequestDto'];
type ServiceActionResponse = components['schemas']['ServiceActionResponseDto'];
type RmsDiagnostics = components['schemas']['RmsDiagnosticsDto'];
type RmsDatabaseTarget = components['schemas']['RmsDatabaseTarget'];
type RmsDatabaseWorkspace = components['schemas']['RmsDatabaseWorkspaceDto'];
type RmsDatabaseOperation = components['schemas']['RmsDatabaseOperationDto'];
type RmsDatabaseBackupRequest = components['schemas']['RmsDatabaseBackupRequestDto'];
type RmsDatabaseRestoreRequest = components['schemas']['RmsDatabaseRestoreRequestDto'];
type BranchCatalogEntry = components['schemas']['BranchCatalogEntryDto'];
type DownloaderOperation = components['schemas']['DownloaderOperationDto'];
type TriggerBatchRequest = components['schemas']['TriggerBatchRequestDto'];
type CleanupPreview = components['schemas']['CleanupPreviewDto'];
type CleanupExecuteRequest = components['schemas']['CleanupExecuteRequestDto'];
type BranchResetPreview = components['schemas']['BranchResetPreviewDto'];
type BranchResetExecuteRequest = components['schemas']['BranchResetExecuteRequestDto'];
type MaintenanceOperation = components['schemas']['MaintenanceOperationDto'];
type ServiceFailureAnalysis = components['schemas']['ServiceFailureAnalysisDto'];
type IncidentTimeline = components['schemas']['IncidentTimelineDto'];
type SupportBundle = components['schemas']['SupportBundleDto'];

@Injectable({ providedIn: 'root' })
export class PosAgentTransportService {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly jsonHeaders = new HttpHeaders({ Accept: 'application/json' });

  getLive(): Observable<HealthStatus> {
    return this.http
      .get<HealthStatus>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.live}`, { headers: this.jsonHeaders })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getReady(): Observable<HealthStatus> {
    return this.http
      .get<HealthStatus>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.ready}`, { headers: this.jsonHeaders })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getHealthCheck(): Observable<HealthReport> {
    return this.http
      .get<HealthReport>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.healthCheck}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getMainServerProfiles(): Observable<MainServerProfiles> {
    return this.http
      .get<MainServerProfiles>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.mainServerProfiles}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getMainServerState(): Observable<MainServerState> {
    return this.http
      .get<MainServerState>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.mainServerState}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getSafetySnapshotPreview(): Observable<SafetySnapshotPreview> {
    return this.http
      .get<SafetySnapshotPreview>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.safetySnapshotPreview}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  captureSafetySnapshot(request: SafetySnapshotCaptureRequest, mutationToken: string): Observable<SafetySnapshot> {
    return this.http
      .post<SafetySnapshot>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.safetySnapshotCapture}`, request, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getSafetySnapshot(snapshotId: string): Observable<SafetySnapshot> {
    return this.http
      .get<SafetySnapshot>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.safetySnapshots}/${encodeURIComponent(snapshotId)}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  verifySafetySnapshot(snapshotId: string): Observable<SafetySnapshotVerification> {
    return this.http
      .get<SafetySnapshotVerification>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.safetySnapshots}/${encodeURIComponent(snapshotId)}/verify`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDiagnosticConsolePreview(target: DiagnosticConsoleTarget, idempotencyKey = this.createPreviewIdempotencyKey()): Observable<DiagnosticConsolePreview> {
    const body: DiagnosticConsolePreviewRequest = { idempotencyKey };
    return this.http
      .post<DiagnosticConsolePreview>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.diagnosticConsolePreview}/${encodeURIComponent(target)}`, body, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  startDiagnosticConsoleRun(request: DiagnosticConsoleStartRequest, mutationToken: string): Observable<DiagnosticConsoleRun> {
    return this.http
      .post<DiagnosticConsoleRun>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.diagnosticConsoleRuns}`, request, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDiagnosticConsoleRun(operationId: string): Observable<DiagnosticConsoleRun> {
    return this.http
      .get<DiagnosticConsoleRun>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.diagnosticConsoleRuns}/${encodeURIComponent(operationId)}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getAgentPackageStatus(): Observable<AgentPackageStatus> {
    return this.http
      .get<AgentPackageStatus>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.packageStatus}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getAgentPackagePreview(operation: AgentPackageOperationKind, idempotencyKey = this.createPreviewIdempotencyKey()): Observable<AgentPackagePreview> {
    const body: AgentPackagePreviewRequest = { idempotencyKey };
    return this.http
      .post<AgentPackagePreview>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.packagePreview}/${encodeURIComponent(operation)}`, body, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  startAgentPackageOperation(request: AgentPackageOperationRequest, mutationToken: string): Observable<AgentPackageOperation> {
    return this.http
      .post<AgentPackageOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.packageOperations}`, request, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getAgentPackageOperation(operationId: string): Observable<AgentPackageOperation> {
    return this.http
      .get<AgentPackageOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.packageOperations}/${encodeURIComponent(operationId)}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getRepairPreview(operation: RepairOperationKind, snapshotId?: string, idempotencyKey = this.createPreviewIdempotencyKey()): Observable<RepairPreview> {
    const suffix = snapshotId ? `/${encodeURIComponent(snapshotId)}` : '';
    const body: RepairPreviewRequest = { idempotencyKey };
    return this.http
      .post<RepairPreview>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.repairPreview}/${encodeURIComponent(operation)}${suffix}`, body, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  startRepairOperation(request: RepairExecuteRequest, mutationToken: string): Observable<RepairOperation> {
    return this.http
      .post<RepairOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.repairOperations}`, request, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getRepairOperation(operationId: string): Observable<RepairOperation> {
    return this.http
      .get<RepairOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.repairOperations}/${encodeURIComponent(operationId)}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getGuidedRepairPreview(snapshotId?: string, idempotencyKey = this.createPreviewIdempotencyKey()): Observable<GuidedRepair> {
    const suffix = snapshotId ? `/${encodeURIComponent(snapshotId)}` : '';
    const body: GuidedRepairPreviewRequest = { idempotencyKey };
    return this.http
      .post<GuidedRepair>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.guidedRepairPreview}${suffix}`, body, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getGuidedRepair(guidedRepairId: string): Observable<GuidedRepair> {
    return this.http
      .get<GuidedRepair>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.guidedRepair}/${encodeURIComponent(guidedRepairId)}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  advanceGuidedRepair(request: GuidedRepairStepRequest, mutationToken: string): Observable<GuidedRepair> {
    return this.http
      .post<GuidedRepair>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.guidedRepairSteps}`, request, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getSession(): Observable<SessionInfo> {
    return this.http
      .get<SessionInfo>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.session}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDeviceIdentity(): Observable<DeviceIdentity> {
    return this.http
      .get<DeviceIdentity>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.deviceIdentity}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDeviceConnectivity(): Observable<DeviceConnectivity> {
    return this.http
      .get<DeviceConnectivity>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.deviceConnectivity}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDeviceCapabilities(): Observable<DeviceCapabilities> {
    return this.http
      .get<DeviceCapabilities>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.deviceCapabilities}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getConfiguration(): Observable<RedactedConfiguration> {
    return this.http
      .get<RedactedConfiguration>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.configuration}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getServices(): Observable<ServiceSummary[]> {
    return this.http
      .get<ServiceSummary[]>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.services}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getRmsDiagnostics(): Observable<RmsDiagnostics> {
    return this.http
      .get<RmsDiagnostics>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.rmsDiagnostics}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getServiceFailureAnalysis(serviceId: string): Observable<ServiceFailureAnalysis> {
    return this.http
      .get<ServiceFailureAnalysis>(
        `${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.serviceFailureAnalysis}/${encodeURIComponent(serviceId)}/failure`,
        { headers: this.jsonHeaders, withCredentials: true }
      )
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getIncidentTimeline(): Observable<IncidentTimeline> {
    return this.http
      .get<IncidentTimeline>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.incidentTimeline}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  generateSupportBundle(mutationToken: string): Observable<SupportBundle> {
    return this.http
      .post<SupportBundle>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.supportBundles}`, null, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getRmsDatabaseWorkspace(target: RmsDatabaseTarget): Observable<RmsDatabaseWorkspace> {
    return this.http
      .get<RmsDatabaseWorkspace>(this.rmsDatabasePath(target), {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  backupRmsDatabase(
    target: RmsDatabaseTarget,
    idempotencyKey: string,
    mutationToken: string
  ): Observable<RmsDatabaseOperation> {
    const body: RmsDatabaseBackupRequest = { idempotencyKey };
    return this.http
      .post<RmsDatabaseOperation>(`${this.rmsDatabasePath(target)}/backup`, body, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  restoreRmsDatabase(
    target: RmsDatabaseTarget,
    backupArtifactId: string,
    confirmationText: string,
    idempotencyKey: string,
    mutationToken: string
  ): Observable<RmsDatabaseOperation> {
    const body: RmsDatabaseRestoreRequest = {
      backupArtifactId,
      confirmationText,
      idempotencyKey
    };
    return this.http
      .post<RmsDatabaseOperation>(`${this.rmsDatabasePath(target)}/restore`, body, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getRmsDatabaseOperation(target: RmsDatabaseTarget, operationId: string): Observable<RmsDatabaseOperation> {
    return this.http
      .get<RmsDatabaseOperation>(
        `${this.rmsDatabasePath(target)}/operations/${encodeURIComponent(operationId)}`,
        { headers: this.jsonHeaders, withCredentials: true }
      )
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  issueMutationToken(operationId: string, targetId?: string): Observable<MutationTokenIssueResponse> {
    const body: MutationTokenIssueRequest = targetId ? { operationId, targetId } : { operationId };
    return this.http
      .post<MutationTokenIssueResponse>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.mutationToken}`, body, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  controlService(
    serviceId: string,
    action: ServiceActionKind,
    idempotencyKey: string,
    mutationToken: string
  ): Observable<ServiceActionResponse> {
    const body: ServiceActionRequest = { action, idempotencyKey };
    const headers = this.jsonHeaders
      .set('Content-Type', 'application/json')
      .set(POS_AGENT_MUTATION_TOKEN_HEADER, mutationToken);

    return this.http
      .post<ServiceActionResponse>(
        `${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.services}/${encodeURIComponent(serviceId)}/actions`,
        body,
        { headers, withCredentials: true }
      )
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDownloaderBranches(): Observable<BranchCatalogEntry[]> {
    return this.http
      .get<BranchCatalogEntry[]>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.downloaderBranches}`, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  triggerDownloaderBatch(
    branchCodes: string[],
    idempotencyKey: string,
    mutationToken: string
  ): Observable<DownloaderOperation> {
    const body: TriggerBatchRequest = { branchCodes, idempotencyKey };
    return this.http
      .post<DownloaderOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.downloaderBatches}`, body, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getDownloaderOperation(operationId: string): Observable<DownloaderOperation> {
    return this.http
      .get<DownloaderOperation>(
        `${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.downloaderOperations}/${encodeURIComponent(operationId)}`,
        { headers: this.jsonHeaders, withCredentials: true }
      )
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  previewCleanup(): Observable<CleanupPreview> {
    return this.http
      .post<CleanupPreview>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.maintenanceCleanupPreview}`, null, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  executeCleanup(
    challengeId: string,
    typedConfirmation: string,
    idempotencyKey: string,
    mutationToken: string
  ): Observable<MaintenanceOperation> {
    const body: CleanupExecuteRequest = { challengeId, typedConfirmation, idempotencyKey };
    return this.http
      .post<MaintenanceOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.maintenanceCleanupExecute}`, body, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  previewBranchReset(): Observable<BranchResetPreview> {
    return this.http
      .post<BranchResetPreview>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.maintenanceBranchResetPreview}`, null, {
        headers: this.jsonHeaders,
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  executeBranchReset(
    challengeId: string,
    typedConfirmation: string,
    idempotencyKey: string,
    mutationToken: string
  ): Observable<MaintenanceOperation> {
    const body: BranchResetExecuteRequest = { challengeId, typedConfirmation, idempotencyKey };
    return this.http
      .post<MaintenanceOperation>(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.maintenanceBranchResetExecute}`, body, {
        headers: this.mutationHeaders(mutationToken),
        withCredentials: true
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  getMaintenanceOperation(operationId: string): Observable<MaintenanceOperation> {
    return this.http
      .get<MaintenanceOperation>(
        `${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.maintenanceOperations}/${encodeURIComponent(operationId)}`,
        { headers: this.jsonHeaders, withCredentials: true }
      )
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  downloadArtifact(artifactId: string): Observable<Blob> {
    return this.http
      .get(`${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.artifacts}/${encodeURIComponent(artifactId)}`, {
        headers: this.jsonHeaders,
        withCredentials: true,
        responseType: 'blob'
      })
      .pipe(catchError(error => throwError(() => classifyPosAgentError(error))));
  }

  private rmsDatabasePath(target: RmsDatabaseTarget): string {
    return `${POS_AGENT_ORIGIN}${POS_AGENT_PATHS.rmsDatabases}/${encodeURIComponent(target)}`;
  }

  private mutationHeaders(mutationToken: string): HttpHeaders {
    return this.jsonHeaders
      .set('Content-Type', 'application/json')
      .set(POS_AGENT_MUTATION_TOKEN_HEADER, mutationToken);
  }

  private createPreviewIdempotencyKey(): string {
    const randomId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    return `preview-${randomId}`.slice(0, 128);
  }
}
