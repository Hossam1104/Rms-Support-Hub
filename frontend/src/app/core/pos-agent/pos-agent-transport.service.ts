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
}
