import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { routes } from '../../app.routes';
import { BuildIdentityService } from '../../core/services/build-identity.service';
import { ToastService } from '../../core/services/toast.service';
import { PosAgentTransportService } from '../../core/pos-agent/pos-agent-transport.service';
import { ConfirmDialogComponent, EmptyStateComponent, PageHeaderComponent, SkeletonComponent, UiButtonComponent, UiCardComponent } from '../../shared/ui';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PosMaintenanceComponent } from './pos-maintenance.component';

@Component({
  standalone: true,
  selector: 'app-navbar',
  template: ''
})
class StubNavbarComponent { }

const FRONTEND_BUILD_SUMMARY = 'Testing · commit 0123456 · build b8e8f1a2c3d4 · 42 assets built 2026-08-15T09:30:00Z';

describe('PosMaintenanceComponent', () => {
  let transport: Record<string, ReturnType<typeof vi.fn>>;
  let toast: Record<string, ReturnType<typeof vi.fn>>;
  let buildIdentity: Record<string, ReturnType<typeof vi.fn>>;
  let fixtures: ComponentFixture<PosMaintenanceComponent>[] = [];

  afterEach(() => {
    for (const fixture of fixtures) fixture.destroy();
    fixtures = [];
    TestBed.resetTestingModule();
  });

  function createFixture(): ComponentFixture<PosMaintenanceComponent> {
    const fixture = TestBed.createComponent(PosMaintenanceComponent);
    fixtures.push(fixture);
    return fixture;
  }

  beforeEach(async () => {
    transport = {
      getLive: vi.fn(() => of({ status: 'live' })),
      getSession: vi.fn(() => of({
        principalName: 'TESTDOMAIN\\admin-user',
        isAuthorized: true,
        agentVersion: '1.0.0',
        apiVersion: '1.0',
        supportedApiVersions: ['1.0']
      })),
      getDeviceIdentity: vi.fn(() => of({
        branchCode: 'BR-001',
        posNumber: 'POS-01',
        productRelease: '2026.08',
        clientName: 'RMS+'
      })),
      getDeviceConnectivity: vi.fn(() => of({
        localSql: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'SQL endpoint is reachable; database health was not queried.' },
        mainServer: { freshness: 'stale', lastCheckedUtc: '2026-08-13T09:59:00Z', detail: 'Main-server TCP endpoint is unreachable.' }
      })),
      getDeviceCapabilities: vi.fn(() => of({
        agentVersion: '1.0.0',
        operatingSystem: 'Windows',
        browseRoots: []
      })),
      getConfiguration: vi.fn(() => of({
        sqlInstance: 'localhost',
        sqlUser: 'support-reader',
        hasSqlPassword: true,
        branchCode: 'BR-001',
        posNumber: 'POS-01',
        release: '2026.08',
        clientName: 'RMS+',
        apiBaseUrl: 'https://rms-api.test',
        databases: ['RmsBranchSrv'],
        services: ['RMS.BranchService'],
        downloader: {
          apiUrl: 'https://rms-downloader.test',
          rdbServerIp: '192.0.2.10',
          rdbUsername: 'rdb-reader',
          hasRdbPassword: true,
          knownBranchCodes: ['BR-001'],
          pollIntervalSeconds: 5,
          timeoutSeconds: 1800
        },
        version: 4
      })),
      getServices: vi.fn(() => of([
        {
          serviceId: 'svc-0123456789abcdef',
          displayName: 'RMS Branch Service',
          installed: true,
          state: 'running',
          lastChecked: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'Windows service is running.' },
          allowedActions: ['stop', 'restart'],
          lastOutcome: null
        }
      ])),
      getRmsDiagnostics: vi.fn(() => of({
        installation: {
          installed: true,
          branchInstalled: true,
          cashierInstalled: true,
          branchCode: 'BR-001',
          posNumber: 'POS-01',
          installationGuid: 'integration-installation-guid',
          mainServerBranchId: '1',
          mainServerPosId: '1',
          mainServerUrl: 'main.integration.test:8443',
          branchServerAddress: 'localhost',
          installationMode: 'Branch + Cashier',
          clientName: 'RMS+',
          versions: {
            branchServerBuildNumber: '2026.08',
            cashierServerBuildNumber: '2026.08',
            cashierUiBuildNumber: '2026.08'
          },
          consistency: {
            branchCode: 'consistent',
            posIdentity: 'consistent',
            mainServerBranchId: 'consistent',
            mainServerPosId: 'consistent',
            version: 'consistent',
            warnings: []
          }
        },
        connectivity: {
          mainServer: {
            configured: true,
            endpoint: 'main.integration.test:8443',
            reachability: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'Main server reachable.' }
          },
          branchServer: {
            configured: true,
            endpoint: 'localhost:5100',
            reachability: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'Branch server reachable.' }
          }
        },
        branchDatabase: {
          expectedDatabase: 'RmsBranchSrv',
          configuredDatabase: 'RmsBranchSrv',
          serverDisplay: 'sql.integration.test:1433',
          configured: true,
          databaseNameMatches: true,
          connectivityStatus: 'reachable',
          evidence: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'Branch database reachable.' }
        },
        cashierDatabase: {
          expectedDatabase: 'RmsCashierSrv',
          configuredDatabase: 'RmsCashierSrv',
          serverDisplay: 'sql.integration.test:1433',
          configured: true,
          databaseNameMatches: true,
          connectivityStatus: 'reachable',
          evidence: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'Cashier database reachable.' }
        },
        services: []
      })),
      getRmsOperationalHealth: vi.fn(() => of({
        fixedRoots: [{
          rootId: 'rms-setup',
          displayName: 'RMS setup',
          state: 'healthy',
          exists: true,
          accessible: true,
          fileCount: 12,
          totalBytes: 4096,
          oldestFileUtc: '2026-08-13T09:00:00Z',
          newestFileUtc: '2026-08-13T10:00:00Z',
          freeBytes: 100000,
          totalCapacityBytes: 1000000,
          detail: 'The fixed RMS setup root is readable within bounded limits.'
        }],
        updates: {
          setupRoot: {} as never,
          downloadsRoot: {} as never,
          releaseRepositoryRoot: {} as never,
          productRelease: '2026.08',
          releaseFileAvailable: true,
          packageState: 'notInstalled',
          detail: 'No server-approved update package is staged.'
        },
        insuranceAttachments: {
          root: {} as never,
          attachmentCount: 0,
          totalBytes: 0,
          oldestAttachmentUtc: null,
          newestAttachmentUtc: null,
          detail: 'No attachment content or identity was returned.'
        },
        checkedAtUtc: '2026-08-13T10:00:00Z'
      })),
      getHealthCheck: vi.fn(() => of({
        overallState: 'healthy',
        summary: 'POS health checks are healthy.',
        checkedAtUtc: '2026-08-13T10:00:00Z',
        checks: [
          { code: 'main-server', state: 'healthy', summary: 'Main Server endpoint is reachable.', checkedAtUtc: '2026-08-13T10:00:00Z', remediation: null },
          { code: 'configuration-consistency', state: 'healthy', summary: 'Configuration is consistent.', checkedAtUtc: '2026-08-13T10:00:00Z', remediation: null }
        ]
      })),
      getMainServerProfiles: vi.fn(() => of({
        profiles: [{
          profileId: 'main-server-testing',
          environment: 'testing',
          enabled: true,
          binding: 'unavailable',
          clientName: 'RMS+',
          allowedReadOperations: ['branchStatus', 'installedBranch', 'installedPos'],
          detail: 'Testing Main Server profile is available; discovery did not produce a safe bound endpoint.'
        }],
        activeProfileId: 'main-server-testing',
        activeBinding: 'unavailable',
        detail: 'Testing Main Server profile is available; no state read was attempted.'
      })),
      getMainServerState: vi.fn(() => of({
        profileId: 'main-server-testing',
        environment: 'testing',
        binding: 'unavailable',
        outcome: 'notAttempted',
        branchCode: null,
        posNumber: null,
        clientName: null,
        branchState: null,
        posState: null,
        detail: 'Main Server state is unavailable until safe endpoint binding succeeds.',
        checkedAtUtc: '2026-08-13T10:00:00Z',
        correlationId: 'test-correlation'
      })),
      getSafetySnapshotPreview: vi.fn(() => of({
        snapshotType: 'pos-agent-safety',
        ready: false,
        evidenceState: 'unknown',
        includedEvidence: ['safe identity', 'service state'],
        excludedEvidence: ['credentials', 'raw configuration'],
        blockers: ['Fresh verified evidence is not available.'],
        retentionMinutes: 30,
        expiresAtUtc: '2026-08-13T10:30:00Z'
      })),
      getAgentPackageStatus: vi.fn(() => of({
        installedVersion: null,
        previousVersion: null,
        verification: 'unknown',
        state: 'notAttempted',
        manifest: null,
        detail: 'No verified server-owned package is staged.'
      })),
      getGuidedRepairPreview: vi.fn(() => of({
        guidedRepairId: '',
        state: 'preview',
        steps: [],
        detail: 'Guided Repair requires a fresh verified Safety Snapshot.',
        expiresAtUtc: '2026-08-13T10:30:00Z'
      })),
      getServiceFailureAnalysis: vi.fn(() => of({
        serviceId: 'svc-0123456789abcdef',
        serviceDisplayName: 'RMS Branch Service',
        category: 'none',
        severity: 'informational',
        confidence: 'low',
        summary: 'No bounded failure evidence was found while the service was running.',
        checkedAtUtc: '2026-08-13T10:00:00Z',
        evidence: [],
        unknownReasons: [],
        recommendations: []
      })),
      getIncidentTimeline: vi.fn(() => of({ generatedAtUtc: '2026-08-13T10:00:00Z', events: [], unknownReasons: [] })),
      generateSupportBundle: vi.fn(() => of({
        artifact: { artifactId: '0123456789abcdef0123456789abcdef', displayName: 'rms-support-bundle.zip', sizeBytes: 4096, sha256Checksum: '0'.repeat(64), createdAtUtc: '2026-08-13T10:00:00Z', expiresAtUtc: '2026-08-14T10:00:00Z' },
        createdAtUtc: '2026-08-13T10:00:00Z',
        correlationId: 'test-correlation',
        includedSections: ['health', 'installation']
      })),
      getRmsDatabaseWorkspace: vi.fn((target: 'branch' | 'cashier') => of({
        target,
        databaseDisplayName: target === 'branch' ? 'Branch Database' : 'Cashier Database',
        restoreConfirmationText: target === 'branch' ? 'RESTORE BRANCH DATABASE' : 'RESTORE CASHIER DATABASE',
        approvedBackups: [],
        latestOperation: null
      })),
      getDownloaderBranches: vi.fn(() => of([{ branchCode: 'BR-001', isSelected: true }])),
      triggerDownloaderBatch: vi.fn(() => of({
        operationId: 'downloader-operation',
        state: 'completed',
        outcome: 'completed',
        progressPercent: 100,
        stage: 'completed',
        detail: 'The downloader completed.',
        startedAtUtc: '2026-08-13T10:00:00Z',
        completedAtUtc: '2026-08-13T10:00:02Z',
        downloaderOutcome: {
          branches: [{ branchCode: 'BR-001', state: 'completed', progressPercent: 100, failureCode: null, artifactId: '0123456789abcdef0123456789abcdef' }],
          serial: null,
          triggerState: 'accepted',
          operatorGuidance: null,
          triggerAccepted: true
        },
        errorCode: null,
        correlationId: 'test-correlation'
      })),
      getDownloaderOperation: vi.fn(),
      previewCleanup: vi.fn(() => of({
        challengeId: 'cleanup-challenge',
        servicesToStop: [],
        pathsToDelete: [],
        confirmationPhrase: 'CLEANUP POS',
        expiresAtUtc: '2026-08-13T10:05:00Z',
        ready: true
      })),
      executeCleanup: vi.fn(),
      previewBranchReset: vi.fn(() => of({
        challengeId: 'reset-challenge',
        branchCode: 'BR-001',
        affectedTables: [],
        confirmationPhrase: 'RESET BRANCH',
        expiresAtUtc: '2026-08-13T10:05:00Z',
        ready: true
      })),
      executeBranchReset: vi.fn(),
      getMaintenanceOperation: vi.fn(),
      downloadArtifact: vi.fn(() => of(new Blob(['artifact'], { type: 'application/zip' }))),
      issueMutationToken: vi.fn(() => of({ token: 'opaque-token', expiresAtUtc: '2026-08-13T10:05:00Z' })),
      controlService: vi.fn(() => of({
        outcome: 'accepted',
        code: 'service_action_accepted',
        detail: 'The Agent acknowledged the service action.',
        correlationId: 'test-correlation'
      })),
      backupRmsDatabase: vi.fn(() => of({
        operationId: 'database-operation',
        target: 'branch',
        databaseDisplayName: 'Branch Database',
        operation: 'backup',
        state: 'completed',
        outcome: 'completed',
        progressPercent: 100,
        stage: 'completed',
        detail: 'The Branch database backup completed.',
        startedAtUtc: '2026-08-13T10:00:00Z',
        completedAtUtc: '2026-08-13T10:00:02Z',
        artifact: null,
        destructiveAttempted: false,
        recoveryRequired: false,
        warnings: [],
        errorCode: null,
        correlationId: 'test-correlation'
      })),
      restoreRmsDatabase: vi.fn(),
      getRmsDatabaseOperation: vi.fn(() => of({
        operationId: 'database-operation',
        target: 'branch',
        databaseDisplayName: 'Branch Database',
        operation: 'backup',
        state: 'completed',
        outcome: 'completed',
        progressPercent: 100,
        stage: 'completed',
        detail: 'The Branch database backup completed.',
        startedAtUtc: '2026-08-13T10:00:00Z',
        completedAtUtc: '2026-08-13T10:00:02Z',
        artifact: null,
        destructiveAttempted: false,
        recoveryRequired: false,
        warnings: [],
        errorCode: null,
        correlationId: 'test-correlation'
      }))
    };
    toast = {
      showSuccess: vi.fn(),
      showError: vi.fn(),
      showWarning: vi.fn(),
      showInfo: vi.fn()
    };
    buildIdentity = {
      load: vi.fn(() => Promise.resolve(null)),
      summary: vi.fn(() => FRONTEND_BUILD_SUMMARY)
    };

    await TestBed.configureTestingModule({
      imports: [PosMaintenanceComponent],
      providers: [
        provideRouter([]),
        { provide: PosAgentTransportService, useValue: transport },
        { provide: ToastService, useValue: toast },
        { provide: BuildIdentityService, useValue: buildIdentity }
      ]
    }).overrideComponent(PosMaintenanceComponent, {
      set: {
        imports: [
          StubNavbarComponent,
          PageHeaderComponent,
          StatusBadgeComponent,
          UiButtonComponent,
          UiCardComponent,
          EmptyStateComponent,
          SkeletonComponent,
          ConfirmDialogComponent
        ]
      }
    }).compileComponents();
  });

  it('renders direct Agent data with authorized state-valid service controls', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelectorAll('h1')).toHaveLength(1);
    expect(page.querySelector('h1')?.textContent).toContain('POS Maintenance');
    expect(page.textContent).toContain('Reachable');
    expect(page.textContent).toContain('Windows authenticated');
    expect(page.textContent).toContain('Local Administrator authorized');
    expect(page.textContent).toContain('BR-001');
    expect(page.textContent).toContain('POS-01');
    expect(page.textContent).toContain('Product Release');
    expect(page.textContent).toContain('RMS Branch Service');
    expect(page.textContent).toContain('RMS services');
    expect(page.textContent).toContain('Stop');
    expect(page.textContent).toContain('Restart');
    expect(Array.from(page.querySelectorAll('button')).some(button => button.textContent?.trim() === 'Start')).toBe(false);
    expect(page.querySelectorAll('button').length).toBeGreaterThan(8);
  });

  it('requires confirmation before issuing a one-use token and submitting a service action', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    const stopButton = Array.from(page.querySelectorAll('button')).find(button => button.textContent?.includes('Stop'));
    expect(stopButton).toBeTruthy();
    stopButton?.click();
    fixture.detectChanges();

    expect(page.querySelector('[role="alertdialog"]')).toBeTruthy();
    expect(transport['issueMutationToken']).not.toHaveBeenCalled();
    expect(transport['controlService']).not.toHaveBeenCalled();

    page.querySelector<HTMLButtonElement>('.btn-confirm')?.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(transport['issueMutationToken']).toHaveBeenCalledWith('services.control', 'svc-0123456789abcdef');
    expect(transport['controlService']).toHaveBeenCalledWith(
      'svc-0123456789abcdef',
      'stop',
      expect.stringMatching(/^support-/),
      'opaque-token'
    );
    expect(page.textContent).toContain('Accepted');
    expect(toast['showSuccess']).toHaveBeenCalled();
  });

  it('hides service controls when the Agent says the session is not authorized', async () => {
    transport['getSession'].mockReturnValue(of({
      principalName: 'TESTDOMAIN\\standard-user',
      isAuthorized: false,
      agentVersion: '1.0.0',
      apiVersion: '1.0',
      supportedApiVersions: ['1.0']
    }));

    const fixture = createFixture();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelectorAll('button').length).toBeGreaterThan(4);
    expect(Array.from(page.querySelectorAll('button')).some(button => button.textContent?.trim() === 'Stop')).toBe(false);
    expect(Array.from(page.querySelectorAll('button')).some(button => button.textContent?.trim() === 'Restart')).toBe(false);
  });

  it('keeps partial transport failures safe and explains the read boundary', async () => {
    transport['getLive'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    transport['getSession'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    transport['getDeviceIdentity'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getDeviceConnectivity'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    transport['getDeviceCapabilities'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getConfiguration'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getServices'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getRmsDiagnostics'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getRmsDatabaseWorkspace'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));

    const fixture = createFixture();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Some direct Agent reads are unavailable.');
    expect(page.textContent).toContain('Authentication required');
    expect(page.textContent).toContain('The fixed POS Agent endpoint could not be reached');
    expect(page.textContent).toContain('The signed-in Windows account is not authorized');
    expect(page.textContent).not.toContain('HttpErrorResponse');
    expect(page.textContent).not.toContain('HttpErrorResponse');
  });

  it('shows the served frontend build identity in Advanced Diagnostics without a filesystem path', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    const evidence = page.querySelector('[data-testid="frontend-build-identity"]');

    expect(buildIdentity['load']).toHaveBeenCalled();
    expect(evidence?.textContent?.trim()).toBe(FRONTEND_BUILD_SUMMARY);
    expect(evidence?.textContent).not.toMatch(/[A-Za-z]:\\|ProgramData|wwwroot/);
  });

  it('routes the POS workspace lazily with a service-control registry status', () => {
    const route = routes.find(candidate => candidate.path === 'tools/pos-maintenance');

    expect(route?.loadComponent).toBeTruthy();
    expect(route?.data?.['status']).toBe('available');
  });

  it('keeps one labelled main landmark and a visible refresh action', async () => {
    const fixture = createFixture();
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('main[aria-label="POS Maintenance service control and evidence"]')).toBeTruthy();
    expect(page.querySelector('main h1')).toBeTruthy();
    expect(page.querySelector('button')?.textContent).toContain('Refresh');
  });
});
