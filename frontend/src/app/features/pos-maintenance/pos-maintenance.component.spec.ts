import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { routes } from '../../app.routes';
import { PosAgentTransportService } from '../../core/pos-agent/pos-agent-transport.service';
import { EmptyStateComponent, PageHeaderComponent, SkeletonComponent, UiButtonComponent, UiCardComponent } from '../../shared/ui';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { PosMaintenanceComponent } from './pos-maintenance.component';

@Component({
  standalone: true,
  selector: 'app-navbar',
  template: ''
})
class StubNavbarComponent { }

describe('PosMaintenanceComponent', () => {
  let transport: Record<string, ReturnType<typeof vi.fn>>;

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
        release: '2026.08',
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
          serviceId: 'svc-example',
          displayName: 'RMS.BranchService',
          state: 'running',
          lastChecked: { freshness: 'fresh', lastCheckedUtc: '2026-08-13T10:00:00Z', detail: 'Windows service is running.' },
          allowedActions: [],
          lastOutcome: null
        }
      ]))
    };

    await TestBed.configureTestingModule({
      imports: [PosMaintenanceComponent],
      providers: [
        provideRouter([]),
        { provide: PosAgentTransportService, useValue: transport }
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
          SkeletonComponent
        ]
      }
    }).compileComponents();
  });

  it('renders direct Agent data with access status and no mutation controls', async () => {
    const fixture = TestBed.createComponent(PosMaintenanceComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelectorAll('h1')).toHaveLength(1);
    expect(page.querySelector('h1')?.textContent).toContain('Read-only first release');
    expect(page.textContent).toContain('Reachable');
    expect(page.textContent).toContain('Windows authenticated');
    expect(page.textContent).toContain('Local Administrator authorized');
    expect(page.textContent).toContain('BR-001');
    expect(page.textContent).toContain('POS-01');
    expect(page.textContent).toContain('Present (value hidden)');
    expect(page.textContent).toContain('RMS.BranchService');
    expect(page.textContent).toContain('Read-only support evidence only');
    expect(page.querySelectorAll('button')).toHaveLength(1);
    expect(page.textContent).not.toContain('Start service');
    expect(page.textContent).not.toContain('Stop service');
    expect(page.textContent).not.toContain('Restart service');
  });

  it('keeps partial transport failures safe and explains the read boundary', async () => {
    transport['getLive'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 0 })));
    transport['getSession'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));
    transport['getDeviceIdentity'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getDeviceConnectivity'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    transport['getDeviceCapabilities'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getConfiguration'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    transport['getServices'].mockReturnValue(throwError(() => new HttpErrorResponse({ status: 403 })));

    const fixture = TestBed.createComponent(PosMaintenanceComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('Some POS Agent reads are unavailable.');
    expect(page.textContent).toContain('Authentication required');
    expect(page.textContent).toContain('The fixed POS Agent endpoint could not be reached');
    expect(page.textContent).toContain('The signed-in Windows account is not authorized');
    expect(page.textContent).not.toContain('HttpErrorResponse');
    expect(page.textContent).not.toContain('stack');
  });

  it('routes the POS workspace lazily with a read-only registry status', () => {
    const route = routes.find(candidate => candidate.path === 'tools/pos-maintenance');

    expect(route?.loadComponent).toBeTruthy();
    expect(route?.data?.['status']).toBe('read-only');
  });

  it('keeps one labelled main landmark and a visible refresh action', async () => {
    const fixture = TestBed.createComponent(PosMaintenanceComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('main[aria-label="POS Maintenance read-only first release"]')).toBeTruthy();
    expect(page.querySelector('main h1')).toBeTruthy();
    expect(page.querySelector('button')?.textContent).toContain('Refresh reads');
  });
});
